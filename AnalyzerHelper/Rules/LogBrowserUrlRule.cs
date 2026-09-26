using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// VF-008 — Open Browser must contain a URL log (Info_Log / Message_Log / ui:LogMessage)
    /// whose message includes the browser URL expression.
    /// Autofix inserts Info_Log at the start of the OpenBrowser body Sequence with StrMessage = URL.
    /// Also runs from Validate Only / reporting (Check).
    /// </summary>
    public sealed class LogBrowserUrlRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-008";
        public string RuleName => "Log Browser URL";
        public string DefaultRecommendation =>
            "Add Info_Log or Log Message inside Open Browser whose message includes the Url value.";
        public bool RequiresUserInteraction => false;

        private static readonly XNamespace XamlNs = "http://schemas.microsoft.com/winfx/2006/xaml";
        private const string IapLoggingNamespace = "clr-namespace:IAP_Logging;assembly=IAP.Logging";

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;
            if (content.IndexOf("OpenBrowser", StringComparison.OrdinalIgnoreCase) < 0)
                return results;

            if (!TryLoad(content, out var doc) || doc == null)
                return results;

            foreach (var openBrowser in EnumerateOpenBrowsers(doc))
            {
                string? urlExpr = GetUrlExpression(openBrowser);
                if (string.IsNullOrWhiteSpace(urlExpr))
                    continue;

                if (HasUrlInfoLog(openBrowser, urlExpr!))
                    continue;

                string display = GetDisplayName(openBrowser);
                results.Add(new RuleCheckResult
                {
                    RuleId = RuleId,
                    RuleName = RuleName,
                    Level = RuleLevel.Warning,
                    Message = $"URL logging activity is not existed in the following activity: {display}.",
                    FilePath = filePath,
                    Recommendation = "Please make sure to add Info_Log or Log Message inside the Open Browser activity with the URL in the message.",
                    RequiresUserInteraction = RequiresUserInteraction
                });
            }

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;
            if (content.IndexOf("OpenBrowser", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            if (!TryLoad(content, out var doc) || doc == null)
                return false;

            bool changed = false;
            string logPrefix = DetectInfoLogPrefix(doc, content);

            foreach (var openBrowser in EnumerateOpenBrowsers(doc).ToList())
            {
                string? urlExpr = GetUrlExpression(openBrowser);
                if (string.IsNullOrWhiteSpace(urlExpr))
                    continue;

                if (HasUrlInfoLog(openBrowser, urlExpr!))
                    continue;

                if (TryInsertUrlInfoLog(openBrowser, urlExpr!, logPrefix, doc))
                    changed = true;
            }

            if (!changed)
                return false;

            if (!HasIapLoggingNamespace(doc))
                EnsureIapLoggingNamespace(doc, ref logPrefix);

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        private static IEnumerable<XElement> EnumerateOpenBrowsers(XDocument doc) =>
            doc.Descendants().Where(e =>
                e.Name.LocalName.IndexOf("OpenBrowser", StringComparison.OrdinalIgnoreCase) >= 0
                && !e.Name.LocalName.Contains('.'));

        private static string GetDisplayName(XElement el)
        {
            var a = el.Attributes().FirstOrDefault(x =>
                string.Equals(x.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(a?.Value) ? el.Name.LocalName : a!.Value.Trim();
        }

        private static string? GetUrlExpression(XElement openBrowser)
        {
            var urlAttr = openBrowser.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, "Url", StringComparison.OrdinalIgnoreCase));
            if (urlAttr != null && !string.IsNullOrWhiteSpace(urlAttr.Value))
                return urlAttr.Value.Trim();

            var urlProp = openBrowser.Elements()
                .FirstOrDefault(e => e.Name.LocalName.EndsWith(".Url", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(e.Name.LocalName, "Url", StringComparison.OrdinalIgnoreCase));
            if (urlProp == null) return null;

            var inArg = urlProp.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "InArgument" || e.Name.LocalName == "Literal");
            if (inArg != null && !string.IsNullOrWhiteSpace(inArg.Value))
                return inArg.Value.Trim();

            string text = urlProp.Value?.Trim() ?? "";
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        /// <summary>
        /// True when a log under OpenBrowser has a message that contains the URL expression
        /// (Info_Log, Message_Log, or ui:LogMessage — e.g. Message="[&quot;…&quot; + in_strACMEUrl]").
        /// </summary>
        private static bool HasUrlInfoLog(XElement openBrowser, string urlExpr)
        {
            string urlNorm = NormalizeExpr(urlExpr);
            if (string.IsNullOrEmpty(urlNorm)) return false;

            foreach (var log in FindUrlLogsInBrowser(openBrowser))
            {
                string msg = GetLogMessage(log);
                if (string.IsNullOrWhiteSpace(msg)) continue;
                if (NormalizeExpr(msg).Contains(urlNorm, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static IEnumerable<XElement> FindUrlLogsInBrowser(XElement openBrowser)
        {
            // Prefer body contents (ActivityAction / Sequence)
            var body = openBrowser.Elements()
                .FirstOrDefault(e => e.Name.LocalName.EndsWith(".Body", StringComparison.OrdinalIgnoreCase));

            IEnumerable<XElement> scope = body != null ? body.Descendants() : openBrowser.Descendants();
            return scope.Where(IsUrlLogActivity);
        }

        private static bool IsUrlLogActivity(XElement e)
        {
            string ln = e.Name.LocalName;
            return ln.Equals("Info_Log", StringComparison.OrdinalIgnoreCase)
                || ln.Equals("Message_Log", StringComparison.OrdinalIgnoreCase)
                || ln.Equals("LogMessage", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLogMessage(XElement log)
        {
            var attr = log.Attributes().FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, "StrMessage", StringComparison.OrdinalIgnoreCase)
                || string.Equals(a.Name.LocalName, "Message", StringComparison.OrdinalIgnoreCase));
            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
                return attr.Value;

            var prop = log.Elements().FirstOrDefault(e =>
                e.Name.LocalName.EndsWith(".StrMessage", StringComparison.OrdinalIgnoreCase)
                || e.Name.LocalName.EndsWith(".Message", StringComparison.OrdinalIgnoreCase));
            return prop?.Value?.Trim() ?? "";
        }

        /// <summary>
        /// Normalize for contains-check: strip quotes and outer [ ] so
        /// Url="[in_strACMEUrl]" matches Message='["Chrome is open on: " + in_strACMEUrl]'.
        /// </summary>
        private static string NormalizeExpr(string expr)
        {
            string s = (expr ?? "")
                .Trim()
                .Replace("\"", "", StringComparison.Ordinal)
                .Replace("&quot;", "", StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();

            if (s.Length >= 2 && s[0] == '[' && s[^1] == ']')
                s = s.Substring(1, s.Length - 2).Trim();

            return s;
        }

        private static bool TryInsertUrlInfoLog(XElement openBrowser, string urlExpr, string logPrefix, XDocument doc)
        {
            XElement? sequence = FindOrCreateBodySequence(openBrowser);
            if (sequence == null) return false;

            string prefix = logPrefix;
            if (string.IsNullOrEmpty(prefix))
                prefix = "i";

            // Keep expression form used on Url (e.g. [strUrl] or literal); XAttribute escapes XML
            XNamespace iapNs = ResolveOrAddIapNamespace(doc, prefix);
            var infoLog = new XElement(iapNs + "Info_Log",
                new XAttribute("DisplayName", "Info Log"),
                new XAttribute("StrMessage", urlExpr),
                new XAttribute("StrTag", "info"));

            // Insert as first real activity (after Variables / ViewState property elements)
            XElement? insertBefore = sequence.Elements()
                .FirstOrDefault(e => !e.Name.LocalName.Contains('.')
                                     && !e.Name.LocalName.Equals("Variable", StringComparison.OrdinalIgnoreCase));

            if (insertBefore != null)
                insertBefore.AddBeforeSelf(infoLog);
            else
                sequence.Add(infoLog);

            return true;
        }

        private static XElement? FindOrCreateBodySequence(XElement openBrowser)
        {
            var body = openBrowser.Elements()
                .FirstOrDefault(e => e.Name.LocalName.EndsWith(".Body", StringComparison.OrdinalIgnoreCase));

            if (body == null)
            {
                // Create Body + ActivityAction + Sequence
                XNamespace ns = openBrowser.Name.Namespace;
                string local = openBrowser.Name.LocalName;
                body = new XElement(ns + (local + ".Body"));
                var action = new XElement("ActivityAction",
                    new XAttribute(XamlNs + "TypeArguments", "x:Object"));
                var seq = new XElement("Sequence", new XAttribute("DisplayName", "Do"));
                action.Add(seq);
                body.Add(action);
                openBrowser.Add(body);
                return seq;
            }

            var existingSeq = body.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Sequence");
            if (existingSeq != null)
                return existingSeq;

            var actionEl = body.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ActivityAction");
            if (actionEl == null)
            {
                actionEl = new XElement("ActivityAction",
                    new XAttribute(XamlNs + "TypeArguments", "x:Object"));
                body.Add(actionEl);
            }

            var newSeq = new XElement("Sequence", new XAttribute("DisplayName", "Do"));
            actionEl.Add(newSeq);
            return newSeq;
        }

        private static string DetectInfoLogPrefix(XDocument doc, string content)
        {
            var existing = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("Info_Log", StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                string? pref = existing.GetPrefixOfNamespace(existing.Name.Namespace);
                if (!string.IsNullOrEmpty(pref)) return pref;
            }

            var m = Regex.Match(content, @"xmlns:(\w+)\s*=\s*[""']clr-namespace:IAP_Logging", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;

            var m2 = Regex.Match(content, @"<(\w+):Info_Log\b", RegexOptions.IgnoreCase);
            if (m2.Success) return m2.Groups[1].Value;

            return "i";
        }

        private static bool HasIapLoggingNamespace(XDocument doc)
        {
            var root = doc.Root;
            if (root == null) return false;
            return root.Attributes()
                .Any(a => a.IsNamespaceDeclaration
                          && (a.Value?.IndexOf("IAP_Logging", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
        }

        private static void EnsureIapLoggingNamespace(XDocument doc, ref string logPrefix)
        {
            var root = doc.Root;
            if (root == null) return;
            if (string.IsNullOrEmpty(logPrefix)) logPrefix = "i";

            if (HasIapLoggingNamespace(doc)) return;

            root.SetAttributeValue(XNamespace.Xmlns + logPrefix, IapLoggingNamespace);
        }

        private static XNamespace ResolveOrAddIapNamespace(XDocument doc, string prefix)
        {
            var root = doc.Root;
            if (root != null)
            {
                var existing = root.Attributes()
                    .FirstOrDefault(a => a.IsNamespaceDeclaration
                                         && (a.Value?.IndexOf("IAP_Logging", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
                if (existing != null)
                    return (XNamespace)existing.Value;

                string p = string.IsNullOrEmpty(prefix) ? "i" : prefix;
                root.SetAttributeValue(XNamespace.Xmlns + p, IapLoggingNamespace);
                return IapLoggingNamespace;
            }

            return IapLoggingNamespace;
        }

        private static bool TryLoad(string content, out XDocument? doc)
        {
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                return true;
            }
            catch
            {
                doc = null;
                return false;
            }
        }

        private static string Serialise(XDocument doc, string original)
        {
            bool crlf = original.Contains("\r\n");
            var sb = new StringBuilder();
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
            if (original.TrimStart().StartsWith("<?xml", StringComparison.Ordinal))
            {
                int end = original.IndexOf("?>", StringComparison.Ordinal) + 2;
                if (end >= 2)
                    body = original.Substring(0, end) + (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }
    }
}
