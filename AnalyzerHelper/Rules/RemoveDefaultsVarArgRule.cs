using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>
    /// Removes default values from Variable and Argument declarations (except exempt names).
    /// Handles: Variable Default attribute, Variable.Default child, companion arg elements, Activity attribute defaults.
    /// </summary>
    public sealed class RemoveDefaultsVarArgRule : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-013";
        public string RuleName => "Remove Default Values (Variables & Arguments)";
        public string DefaultRecommendation =>
            "Remove default values from Variable and Argument declarations. " +
            "Exempt: strComponentName, strWorkflowName, strProcessIdentifier, dctmailTemplates, " +
            "crddctroboCred, dctselector, booldctroboBool, intdctroboInt, dctroboText.";
        public bool RequiresUserInteraction => false;

        private static readonly string[] ExemptKeywords =
        {
            "strcomponentname",
            "strworkflowname",
            "strprocessidentifier",
            "dctmailtemplates",
            "crddctrobocred",
            "dctselector",
            "booldctrobobool",
            "intdctroboint",
            "dctrobotext",
        };

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (string.IsNullOrWhiteSpace(content)) return results;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            }
            catch
            {
                return results;
            }

            bool hasRemovableVariableDefaults = HasRemovableVariableDefaultAttributes(doc)
                || HasRemovableVariableDefaultChildren(doc);
            bool hasRemovableArgumentCompanions = HasRemovableArgumentCompanions(doc);
            bool hasRemovableActivityAttributeDefaults = HasRemovableActivityAttributeDefaults(doc);

            if (!hasRemovableVariableDefaults && !hasRemovableArgumentCompanions && !hasRemovableActivityAttributeDefaults)
                return results;

            var parts = new List<string>();
            if (hasRemovableVariableDefaults || hasRemovableActivityAttributeDefaults)
                parts.Add("Variable");
            if (hasRemovableArgumentCompanions)
                parts.Add("Argument");

            string target = string.Join(" and ", parts);
            string rec = parts.Count == 1 && parts[0] == "Variable"
                ? "Remove default values from Variable declarations to keep the workflow clean."
                : parts.Count == 1 && parts[0] == "Argument"
                ? "Remove default values from Argument declarations to keep the workflow clean."
                : "Remove default values from Variable and Argument declarations to keep the workflow clean.";

            results.Add(new RuleCheckResult
            {
                RuleId = RuleId,
                RuleName = RuleName,
                Level = RuleLevel.Warning,
                Message = $"{target} default values found (non-exempt). Remove defaults for consistency.",
                FilePath = filePath,
                Recommendation = rec,
                RequiresUserInteraction = RequiresUserInteraction
            });

            return results;
        }

        public bool DefineAndFix(string filePath, string content, out string newContent)
        {
            newContent = content;
            if (string.IsNullOrWhiteSpace(content)) return false;

            XDocument doc;
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            }
            catch
            {
                return false;
            }

            bool changed = false;
            changed |= RemoveVariableDefaultAttributes(doc);
            changed |= RemoveVariableDefaultChildren(doc);
            changed |= RemoveArgumentCompanions(doc);
            changed |= RemoveActivityAttributeDefaults(doc);

            if (!changed) return false;

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        // ── Check helpers (detect only, no mutation) ─────────────────────────

        private static bool HasRemovableVariableDefaultAttributes(XDocument doc)
        {
            return doc.Descendants()
                .Where(e => e.Name.LocalName == "Variable")
                .Any(varEl =>
                {
                    string? name = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
                    if (IsExempt(name)) return false;
                    return varEl.Attributes().Any(a => a.Name.LocalName == "Default");
                });
        }

        private static bool HasRemovableVariableDefaultChildren(XDocument doc)
        {
            return doc.Descendants()
                .Where(e => e.Name.LocalName == "Variable")
                .Any(varEl =>
                {
                    string? name = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
                    if (IsExempt(name)) return false;
                    return varEl.Elements().Any(e => e.Name.LocalName == "Variable.Default");
                });
        }

        private static bool HasRemovableArgumentCompanions(XDocument doc)
        {
            if (doc.Root == null) return false;
            return doc.Root.Elements()
                .Any(child =>
                {
                    string localName = child.Name.LocalName;
                    int dot = localName.LastIndexOf('.');
                    if (dot < 0) return false;
                    string argName = localName.Substring(dot + 1);
                    if (IsExempt(argName)) return false;
                    return HasRealDefault(child);
                });
        }

        private static bool HasRemovableActivityAttributeDefaults(XDocument doc)
        {
            if (doc.Root == null) return false;
            const string thisNs = "clr-namespace:";
            return doc.Root.Attributes()
                .Where(a => a.Name.NamespaceName == thisNs && a.Name.LocalName.Contains('.'))
                .Select(a =>
                {
                    int dot = a.Name.LocalName.LastIndexOf('.');
                    return a.Name.LocalName.Substring(dot + 1);
                })
                .Any(argName => !IsExempt(argName));
        }

        // ── Fix passes (same logic as OldAnalyzerHelper.RemoveVarArgDefaultsAction) ──

        private static bool RemoveVariableDefaultAttributes(XDocument doc)
        {
            bool changed = false;
            foreach (var varEl in doc.Descendants().Where(e => e.Name.LocalName == "Variable"))
            {
                string? name = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
                if (IsExempt(name)) continue;
                var defaultAttr = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Default");
                if (defaultAttr == null) continue;
                defaultAttr.Remove();
                changed = true;
            }
            return changed;
        }

        private static bool RemoveVariableDefaultChildren(XDocument doc)
        {
            bool changed = false;
            foreach (var varEl in doc.Descendants().Where(e => e.Name.LocalName == "Variable"))
            {
                string? name = varEl.Attributes().FirstOrDefault(a => a.Name.LocalName == "Name")?.Value;
                if (IsExempt(name)) continue;
                var defaultChild = varEl.Elements().FirstOrDefault(e => e.Name.LocalName == "Variable.Default");
                if (defaultChild == null) continue;
                RemoveClean(defaultChild);
                if (!varEl.HasElements)
                {
                    foreach (var t in varEl.Nodes().OfType<XText>()
                        .Where(t => string.IsNullOrWhiteSpace(t.Value)).ToList())
                        t.Remove();
                }
                changed = true;
            }
            return changed;
        }

        private static bool RemoveArgumentCompanions(XDocument doc)
        {
            if (doc.Root == null) return false;
            bool changed = false;
            var toRemove = new List<XElement>();
            foreach (var child in doc.Root.Elements())
            {
                string localName = child.Name.LocalName;
                int dot = localName.LastIndexOf('.');
                if (dot < 0) continue;
                string argName = localName.Substring(dot + 1);
                if (IsExempt(argName)) continue;
                if (!HasRealDefault(child)) continue;
                toRemove.Add(child);
            }
            foreach (var el in toRemove)
            {
                RemoveClean(el);
                changed = true;
            }
            return changed;
        }

        private static bool RemoveActivityAttributeDefaults(XDocument doc)
        {
            if (doc.Root == null) return false;
            const string thisNs = "clr-namespace:";
            var toRemove = doc.Root.Attributes()
                .Where(a => a.Name.NamespaceName == thisNs && a.Name.LocalName.Contains('.'))
                .Select(a =>
                {
                    int dot = a.Name.LocalName.LastIndexOf('.');
                    string arg = a.Name.LocalName.Substring(dot + 1);
                    return (attr: a, argName: arg);
                })
                .Where(x => !IsExempt(x.argName))
                .Select(x => x.attr)
                .ToList();
            foreach (var attr in toRemove)
                attr.Remove();
            return toRemove.Count > 0;
        }

        private static bool HasRealDefault(XElement companionEl)
        {
            var argEl = companionEl.Elements().FirstOrDefault(e =>
                e.Name.LocalName == "InArgument" || e.Name.LocalName == "OutArgument");
            if (argEl == null) return false;
            return argEl.HasElements || !string.IsNullOrWhiteSpace(argEl.Value);
        }

        private static bool IsExempt(string? varName)
        {
            if (string.IsNullOrEmpty(varName)) return false;
            string lower = varName.ToLowerInvariant();
            return ExemptKeywords.Any(keyword => lower.Contains(keyword));
        }

        private static void RemoveClean(XElement el)
        {
            var prev = el.PreviousNode as XText;
            if (prev != null && string.IsNullOrWhiteSpace(prev.Value))
                prev.Remove();
            el.Remove();
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
            if (original.TrimStart().StartsWith("<?xml"))
            {
                int end = original.IndexOf("?>", StringComparison.Ordinal) + 2;
                body = original.Substring(0, end) + (crlf ? "\r\n" : "\n") + body;
            }
            return body;
        }
    }
}
