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
    /// VF-028 — checks for hardcoded default values in workflow arguments and static invoke arguments,
    /// and provides an automated fix to delete hardcoded values specifically for InArgument, OutArgument, and InOutArgument.
    /// </summary>
    public sealed class HardcodedArguments : IAnalyzerRuleWithFix
    {
        public string RuleId => "VF-028";
        public string RuleName => "HardcodedArguments";
        public string DefaultRecommendation =>
            "Please remove any default value existed in the workflow or its invokes.";
        public bool RequiresUserInteraction => false;

        private static readonly string[] ExemptKeywords =
        {
            "strcomponentname",
            "strworkflowname",
            "strstatename"
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

            if (doc.Root == null) return results;

            string workflowName = Path.GetFileNameWithoutExtension(filePath);
            var declaredArgs = GetDeclaredArgumentNames(doc);

            // 1. Check workflow argument default values (companion elements on root <Activity> that contain InArgument/OutArgument/InOutArgument)
            foreach (var child in doc.Root.Elements())
            {
                string localName = child.Name.LocalName;
                int dot = localName.LastIndexOf('.');
                if (dot < 0) continue;

                string argName = localName.Substring(dot + 1);
                if (IsExempt(argName)) continue;

                if (HasRealArgumentDefault(child))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following workflow: {workflowName} has argument with default value. argument name: {argName}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            // 2. Check workflow argument default values (attributes on root <Activity> strictly for declared In/Out/InOut arguments)
            foreach (var attr in doc.Root.Attributes())
            {
                string localName = attr.Name.LocalName;
                int dot = localName.LastIndexOf('.');
                if (dot < 0) continue;

                string argName = localName.Substring(dot + 1);
                if (!IsArgumentName(argName, declaredArgs)) continue;
                if (IsExempt(argName)) continue;

                if (!string.IsNullOrWhiteSpace(attr.Value))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following workflow: {workflowName} has argument with default value. argument name: {argName}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            // 3. Check <x:Property> declarations for default values (strictly InArgument, OutArgument, InOutArgument)
            foreach (var prop in doc.Descendants().Where(e => e.Name.LocalName == "Property"))
            {
                string? type = prop.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!IsArgumentType(type)) continue;

                string? name = prop.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))?.Value;
                if (IsExempt(name)) continue;

                var defaultAttr = prop.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase));
                if (defaultAttr != null && !string.IsNullOrWhiteSpace(defaultAttr.Value))
                {
                    results.Add(new RuleCheckResult
                    {
                        RuleId = RuleId,
                        RuleName = RuleName,
                        Level = RuleLevel.Warning,
                        Message = $"The following workflow: {workflowName} has argument with default value. argument name: {name ?? "(unnamed)"}.",
                        FilePath = filePath,
                        Recommendation = DefaultRecommendation,
                        RequiresUserInteraction = RequiresUserInteraction
                    });
                }
            }

            // 4. Check <ui:InvokeWorkflowFile.Arguments> strictly for InArgument, OutArgument, or InOutArgument containing hardcoded/literal values
            foreach (var invokeArgs in doc.Descendants().Where(e => IsInvokeArgumentsContainer(e)))
            {
                foreach (var argEl in invokeArgs.Elements().Where(IsArgumentElement))
                {
                    string? key = argEl.Attributes().FirstOrDefault(a =>
                        string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase))?.Value;

                    if (IsExempt(key)) continue;

                    if (HasHardcodedLiteral(argEl))
                    {
                        string argNameDisplay = string.IsNullOrWhiteSpace(key) ? "(unnamed)" : key;
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"Invoked Workflows {workflowName} Contains invoked sequances has a static arguments. argument name: {argNameDisplay}.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation,
                            RequiresUserInteraction = RequiresUserInteraction
                        });
                    }
                }
            }

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

            if (doc.Root == null) return false;

            bool changed = false;
            var declaredArgs = GetDeclaredArgumentNames(doc);

            // 1. Remove hardcoded literal values strictly from InArgument, OutArgument, InOutArgument inside <ui:InvokeWorkflowFile.Arguments>
            foreach (var invokeArgs in doc.Descendants().Where(e => IsInvokeArgumentsContainer(e)))
            {
                foreach (var argEl in invokeArgs.Elements().Where(IsArgumentElement))
                {
                    string? key = argEl.Attributes().FirstOrDefault(a =>
                        string.Equals(a.Name.LocalName, "Key", StringComparison.OrdinalIgnoreCase))?.Value;

                    if (IsExempt(key)) continue;

                    if (HasHardcodedLiteral(argEl))
                    {
                        // Remove all child nodes (<Literal ...>, text) leaving the clean empty argument tag
                        argEl.RemoveNodes();

                        // Remove Value attribute if present
                        var valAttr = argEl.Attributes().FirstOrDefault(a =>
                            string.Equals(a.Name.LocalName, "Value", StringComparison.OrdinalIgnoreCase));
                        valAttr?.Remove();

                        changed = true;
                    }
                }
            }

            // 2. Remove companion argument default elements on the root <Activity> (only if containing InArgument, OutArgument, InOutArgument)
            var companionsToRemove = new List<XElement>();
            foreach (var child in doc.Root.Elements())
            {
                string localName = child.Name.LocalName;
                int dot = localName.LastIndexOf('.');
                if (dot < 0) continue;

                string argName = localName.Substring(dot + 1);
                if (IsExempt(argName)) continue;

                if (HasRealArgumentDefault(child))
                {
                    companionsToRemove.Add(child);
                }
            }

            foreach (var el in companionsToRemove)
            {
                RemoveClean(el);
                changed = true;
            }

            // 3. Remove root attributes defining argument defaults strictly for In/Out/InOut arguments
            var attrsToRemove = new List<XAttribute>();
            foreach (var attr in doc.Root.Attributes())
            {
                string localName = attr.Name.LocalName;
                int dot = localName.LastIndexOf('.');
                if (dot < 0) continue;

                string argName = localName.Substring(dot + 1);
                if (!IsArgumentName(argName, declaredArgs)) continue;
                if (IsExempt(argName)) continue;

                attrsToRemove.Add(attr);
            }

            foreach (var attr in attrsToRemove)
            {
                attr.Remove();
                changed = true;
            }

            // 4. Remove Default attribute from <x:Property> elements (only if Type is InArgument, OutArgument, InOutArgument)
            foreach (var prop in doc.Descendants().Where(e => e.Name.LocalName == "Property"))
            {
                string? type = prop.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase))?.Value;
                if (!IsArgumentType(type)) continue;

                string? name = prop.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))?.Value;
                if (IsExempt(name)) continue;

                var defaultAttr = prop.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Default", StringComparison.OrdinalIgnoreCase));
                if (defaultAttr != null)
                {
                    defaultAttr.Remove();
                    changed = true;
                }
            }

            if (!changed) return false;

            newContent = Serialise(doc, content);
            return newContent != content;
        }

        /// <summary>Checks if an element is strictly an InArgument, OutArgument, or InOutArgument.</summary>
        private static bool IsArgumentElement(XElement el)
        {
            string ln = el.Name.LocalName;
            return string.Equals(ln, "InArgument", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ln, "OutArgument", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(ln, "InOutArgument", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Checks if a property type string represents an InArgument, OutArgument, or InOutArgument.</summary>
        private static bool IsArgumentType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return false;
            return type.StartsWith("InArgument", StringComparison.OrdinalIgnoreCase) ||
                   type.StartsWith("OutArgument", StringComparison.OrdinalIgnoreCase) ||
                   type.StartsWith("InOutArgument", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsArgumentName(string name, HashSet<string> declaredArgs)
        {
            if (declaredArgs.Contains(name)) return true;
            return name.StartsWith("in_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("out_", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("io_", StringComparison.OrdinalIgnoreCase);
        }

        private static HashSet<string> GetDeclaredArgumentNames(XDocument doc)
        {
            return new HashSet<string>(doc.Descendants()
                .Where(e => e.Name.LocalName == "Property")
                .Where(e => IsArgumentType(e.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase))?.Value))
                .Select(e => e.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase))?.Value)
                .Where(n => !string.IsNullOrEmpty(n))!,
                StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsInvokeArgumentsContainer(XElement e)
        {
            string ln = e.Name.LocalName;
            if (string.Equals(ln, "InvokeWorkflowFile.Arguments", StringComparison.OrdinalIgnoreCase))
                return true;

            if (ln.EndsWith(".Arguments", StringComparison.OrdinalIgnoreCase) || string.Equals(ln, "Arguments", StringComparison.OrdinalIgnoreCase))
            {
                var parent = e.Parent;
                if (parent != null && parent.Name.LocalName.IndexOf("Invoke", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static bool HasHardcodedLiteral(XElement argEl)
        {
            // 1. Contains <Literal> element anywhere in descendants
            if (argEl.Descendants().Any(d => string.Equals(d.Name.LocalName, "Literal", StringComparison.OrdinalIgnoreCase)))
                return true;

            // 2. Direct text content that is not empty and not a variable expression
            if (!argEl.HasElements && !string.IsNullOrWhiteSpace(argEl.Value))
            {
                string val = argEl.Value.Trim();
                if (!val.StartsWith("[") || !val.EndsWith("]"))
                    return true;
            }

            // 3. Literal in Value attribute
            var valAttr = argEl.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, "Value", StringComparison.OrdinalIgnoreCase));
            if (valAttr != null && !string.IsNullOrWhiteSpace(valAttr.Value))
            {
                string v = valAttr.Value.Trim();
                if (!v.StartsWith("[") || !v.EndsWith("]"))
                    return true;
            }

            return false;
        }

        private static bool HasRealArgumentDefault(XElement companionEl)
        {
            // Must contain InArgument, OutArgument, or InOutArgument
            var argEl = companionEl.Elements().FirstOrDefault(IsArgumentElement);
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
