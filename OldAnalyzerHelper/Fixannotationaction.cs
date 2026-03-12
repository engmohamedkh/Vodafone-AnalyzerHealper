using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace AnalyzerHelper
{
    /// <summary>
    /// Fixes the sap2010:Annotation.AnnotationText on the root Sequence/Flowchart
    /// of a UiPath XAML component file.
    ///
    /// The annotation has five sections, each on its own line:
    ///
    ///   Component Name: {value}
    ///   Description:    {value}
    ///   Pre Condition:  {value}
    ///   Post Condition: {value}
    ///   PDD Section:    {value}
    ///
    /// Rules applied per section:
    ///
    ///   Component Name  → Always replaced with the file's base name (no extension).
    ///
    ///   Description     → If the current value is the default placeholder
    ///                     ("Narrative of what tasks the component will perform")
    ///                     OR empty → kept as the default placeholder.
    ///                     If it contains custom text → kept unchanged.
    ///
    ///   Pre Condition   → Extracted from the StrMessage of the first i:Info_Log
    ///                     found inside the If.Then branch of the "Pre Condition"
    ///                     sequence. If none found or message is null/{x:Null}/empty
    ///                     → set to "#NA".
    ///
    ///   Post Condition  → Same logic, from the "Post Condition" sequence.
    ///
    ///   PDD Section     → If the section key is missing from the annotation
    ///                     entirely → added as "#NA".
    ///                     If it already exists (any value including "#NA") → kept.
    ///
    /// If the annotation attribute is missing from the root activity entirely,
    /// a fresh one is created with all five sections.
    /// </summary>
    public class FixAnnotationAction : IFixerAction
    {
        public string Name => "Fix Workflows Annotation";

        public string Description =>
            "Ensures the root Sequence/Flowchart annotation contains all five required " +
            "sections (Component Name, Description, Pre Condition, Post Condition, " +
            "PDD Section). Component Name is always set to the file name. " +
            "Pre/Post Condition text is read from the Info_Log inside the condition " +
            "sequences. Description and PDD Section are kept if already customised.";

        // ── Constants ─────────────────────────────────────────────────────────
        private const string DefaultDescription =
            "Narrative of what tasks the component will perform";
        private const string NA = "#NA";

        // Annotation attribute lives in this namespace
        private static readonly XNamespace _sap2010 =
            "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation";

        // The attribute name (with namespace)
        private static readonly XName _annotAttrName =
            XName.Get("Annotation.AnnotationText",
                "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation");

        // IAP_Logging namespace (the i: prefix in XAML)
        private const string IapLoggingNs = "clr-namespace:IAP_Logging;assembly=IAP.Logging";

        // ─────────────────────────────────────────────────────────────────────
        public bool Run(string filePath)
        {
            string original = File.ReadAllText(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);

            XDocument doc;
            try { doc = XDocument.Parse(original, LoadOptions.PreserveWhitespace); }
            catch { return false; }

            // ── Find the root activity element (Sequence or Flowchart) ────────
            XElement root = FindRootActivity(doc);
            if (root == null) return false;

            // ── Read existing annotation (if any) ────────────────────────────
            XAttribute annotAttr = root.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == "Annotation.AnnotationText");

            AnnotationSections current = annotAttr != null
                ? AnnotationSections.Parse(DecodeAnnotation(annotAttr.Value))
                : new AnnotationSections();

            // ── Build updated sections ────────────────────────────────────────
            var updated = new AnnotationSections
            {
                ComponentName = baseName,
                Description = ResolveDescription(current.Description),
                PreCondition = ResolveCondition(doc, "Pre Condition", current.PreCondition),
                PostCondition = ResolveCondition(doc, "Post Condition", current.PostCondition),
                PddSection = ResolvePddSection(current.PddSection),
            };

            // ── Check whether anything actually changed ───────────────────────
            if (current.Equals(updated)) return false;

            // ── Write back ───────────────────────────────────────────────────
            string newAnnotationText = EncodeAnnotation(updated.ToString());

            if (annotAttr != null)
                annotAttr.Value = newAnnotationText;
            else
                root.Add(new XAttribute(_annotAttrName, newAnnotationText));

            string result = Serialise(doc, original);
            if (result == original) return false;

            File.WriteAllText(filePath, result);
            return true;
        }

        // =====================================================================
        //  Section resolution
        // =====================================================================

        private static string ResolveDescription(string current)
        {
            // Keep custom text; reset to default if empty or already the placeholder
            if (string.IsNullOrWhiteSpace(current) ||
                current.Trim() == DefaultDescription)
                return DefaultDescription;
            return current.Trim();
        }

        /// <summary>
        /// Extracts the condition text from the Info_Log inside the If.Then branch
        /// of the named condition sequence ("Pre Condition" / "Post Condition").
        /// Falls back to <paramref name="existingValue"/> if non-trivial, else #NA.
        /// </summary>
        private static string ResolveCondition(
            XDocument doc, string sequenceName, string existingValue)
        {
            string fromLog = ExtractConditionFromLog(doc, sequenceName);

            if (!string.IsNullOrWhiteSpace(fromLog))
                return fromLog.Trim();

            // No log message found — keep existing if it's real, else #NA
            if (!string.IsNullOrWhiteSpace(existingValue) &&
                existingValue.Trim() != NA)
                return existingValue.Trim();

            return NA;
        }

        private static string ResolvePddSection(string current)
        {
            // If section was completely absent (null) or empty → #NA
            // If it has any value (including "#NA") → keep it
            if (current == null) return NA;
            return string.IsNullOrWhiteSpace(current) ? NA : current.Trim();
        }

        // =====================================================================
        //  Info_Log extraction
        // =====================================================================

        /// <summary>
        /// Finds the Sequence whose DisplayName == <paramref name="seqName"/>,
        /// then looks for an &lt;If&gt; inside it, then reads the StrMessage from
        /// the first Info_Log inside the If.Then branch.
        /// Returns null if not found or message is empty/{x:Null}.
        /// </summary>
        private static string ExtractConditionFromLog(XDocument doc, string seqName)
        {
            // Find the named sequence anywhere in the document
            var condSeq = doc.Descendants()
                .FirstOrDefault(e =>
                    e.Name.LocalName == "Sequence" &&
                    (string)e.Attribute("DisplayName") == seqName);

            if (condSeq == null) return null;

            // Find the first <If> inside it (direct or shallow child)
            var ifEl = condSeq.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "If");

            if (ifEl == null) return null;

            // Find If.Then child element
            var thenEl = ifEl.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "If.Then");

            if (thenEl == null) return null;

            // Find any Info_Log inside Then (local name ends with Info_Log)
            var infoLog = thenEl.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Info_Log");

            if (infoLog == null) return null;

            // Read StrMessage attribute
            string msg = infoLog.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == "StrMessage")?.Value;

            if (string.IsNullOrWhiteSpace(msg) || msg == "{x:Null}")
                return null;

            // Strip surrounding [...] if it's a VB expression
            msg = msg.Trim();
            if (msg.StartsWith("[") && msg.EndsWith("]"))
                msg = msg.Substring(1, msg.Length - 2).Trim('"');

            return msg;
        }

        // =====================================================================
        //  Root activity finder
        // =====================================================================

        private static XElement FindRootActivity(XDocument doc)
        {
            // The root activity is the first Sequence or Flowchart that is a
            // direct child of the root Activity element
            return doc.Root?.Elements()
                .FirstOrDefault(e =>
                    e.Name.LocalName == "Sequence" ||
                    e.Name.LocalName == "Flowchart");
        }

        // =====================================================================
        //  Annotation encode / decode
        // =====================================================================

        /// <summary>
        /// Decodes the XML-attribute-encoded annotation text back to plain text.
        /// The newline is stored as &amp;#xA; (literal 5-char sequence in the attribute).
        /// </summary>
        private static string DecodeAnnotation(string attrValue)
        {
            // XML parser already decoded &lt; &gt; &amp; etc. into real chars.
            // The &#xA; newlines are stored as literal \n after XML parsing.
            return attrValue.Replace("\n", "\n"); // already decoded by XDocument
        }

        /// <summary>
        /// Re-encodes the plain-text annotation for storage as an XML attribute value.
        /// XDocument will handle &lt;/&gt; etc. automatically when we set .Value;
        /// we just need to convert real newlines to &#xA; for the attribute.
        /// </summary>
        private static string EncodeAnnotation(string plain)
        {
            // XDocument stores attribute newlines as &#xA; automatically —
            // we return plain text and let the serialiser handle encoding.
            return plain;
        }

        // =====================================================================
        //  Serialisation
        // =====================================================================

        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");
            var sb = new System.Text.StringBuilder();

            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "  ",
                NewLineChars = crlf ? "\r\n" : "\n",
                NewLineHandling = NewLineHandling.Replace,
            };

            using (var sw = new StringWriter(sb))
            using (var xw = XmlWriter.Create(sw, settings))
                doc.Save(xw);

            string body = sb.ToString();
            if (original.TrimStart().StartsWith("<?xml"))
            {
                int end = original.IndexOf("?>") + 2;
                body = original.Substring(0, end) +
                       (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }

        // =====================================================================
        //  Annotation sections model
        // =====================================================================

        private class AnnotationSections
        {
            public string ComponentName { get; set; }
            public string Description { get; set; }
            public string PreCondition { get; set; }
            public string PostCondition { get; set; }
            // null means the key was completely absent from the annotation
            public string PddSection { get; set; }

            /// <summary>
            /// Parses a decoded annotation string into its five sections.
            /// Uses typo-tolerant prefix matching so misspellings like
            /// "Componenet Name", "Descritpion", "Pre-Condition", "PDD Seciton"
            /// are all correctly identified.
            /// </summary>
            public static AnnotationSections Parse(string text)
            {
                var result = new AnnotationSections();
                if (string.IsNullOrWhiteSpace(text)) return result;

                foreach (var line in text.Split('\n'))
                {
                    // Split on first colon to separate key from value
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;

                    string rawKey = line.Substring(0, colon).TrimEnd();
                    string value = line.Substring(colon + 1).Trim();

                    string canonical = MatchKey(rawKey);
                    if (canonical == null) continue;

                    switch (canonical)
                    {
                        case "Component Name": result.ComponentName = value; break;
                        case "Description": result.Description = value; break;
                        case "Pre Condition": result.PreCondition = value; break;
                        case "Post Condition": result.PostCondition = value; break;
                        case "PDD Section": result.PddSection = value; break;
                    }
                }
                return result;
            }

            /// <summary>
            /// Maps any variant/misspelling of a section key to its canonical name
            /// using prefix matching on a normalised (lowercase, no spaces/hyphens)
            /// version of the key.
            ///
            /// Unique prefixes:
            ///   comp → Component Name
            ///   desc → Description
            ///   pre  → Pre Condition   (never confused with "post" — different 3rd char)
            ///   post → Post Condition
            ///   pdd  → PDD Section
            /// </summary>
            private static string MatchKey(string rawKey)
            {
                // Normalise: strip spaces, hyphens, underscores; lowercase
                string n = Regex.Replace(rawKey, @"[\s\-_]+", "").ToLowerInvariant();

                if (n.StartsWith("comp")) return "Component Name";
                if (n.StartsWith("desc")) return "Description";
                if (n.StartsWith("pre")) return "Pre Condition";
                if (n.StartsWith("post")) return "Post Condition";
                if (n.StartsWith("pdd")) return "PDD Section";

                return null; // unrecognised line
            }

            /// <summary>Serialises back to the canonical five-line format.</summary>
            public override string ToString() =>
                $"Component Name: {ComponentName ?? NA}\n" +
                $"Description: {Description ?? DefaultDescription}\n" +
                $"Pre Condition: {PreCondition ?? NA}\n" +
                $"Post Condition: {PostCondition ?? NA}\n" +
                $"PDD Section: {PddSection ?? NA}";

            public bool Equals(AnnotationSections other) =>
                other != null &&
                ComponentName == other.ComponentName &&
                Description == other.Description &&
                PreCondition == other.PreCondition &&
                PostCondition == other.PostCondition &&
                PddSection == other.PddSection;
        }
    }
}