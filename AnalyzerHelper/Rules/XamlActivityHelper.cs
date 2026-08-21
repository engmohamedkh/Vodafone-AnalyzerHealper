using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace AnalyzerHelper.Rules
{
    /// <summary>Shared helpers for scanning UiPath XAML activity trees (validate-only rules).</summary>
    internal static class XamlActivityHelper
    {
        private static readonly HashSet<string> StructuralLocalNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Activity", "Sequence", "Flowchart", "StateMachine", "State", "Transition",
            "FlowStep", "FlowDecision", "FlowSwitch", "TryCatch", "Catch", "Catches",
            "Variable", "VariableList", "InArgument", "OutArgument", "InOutArgument",
            "ActivityAction", "ActivityFunc", "DelegateInArgument", "DelegateOutArgument",
            "VisualBasicSettings", "Collection", "Dictionary", "ViewStateDictionary",
            "Array", "Property", "TextExpression", "Literal", "VisualBasicValue",
            "VisualBasicReference", "Reference", "Null", "xNull"
        };

        public static bool TryParse(string content, out XDocument? doc)
        {
            doc = null;
            if (string.IsNullOrWhiteSpace(content)) return false;
            try
            {
                doc = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
                return doc.Root != null;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPropertyElement(XElement e) =>
            e != null && e.Name.LocalName.Contains('.');

        public static bool IsStructural(XElement e)
        {
            if (e == null) return true;
            string ln = e.Name.LocalName;
            if (IsPropertyElement(e)) return true;
            if (StructuralLocalNames.Contains(ln)) return true;
            if (ln.StartsWith("WorkflowViewState", StringComparison.OrdinalIgnoreCase)) return true;
            if (ln.StartsWith("ViewStateService", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Activity-like elements (excludes property wrappers and common containers).</summary>
        public static IEnumerable<XElement> EnumerateActivities(XDocument doc) =>
            doc.Descendants().Where(e => !IsStructural(e));

        public static string GetDisplayName(XElement el)
        {
            var a = el.Attributes().FirstOrDefault(x =>
                string.Equals(x.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase));
            return string.IsNullOrWhiteSpace(a?.Value) ? el.Name.LocalName : a!.Value.Trim();
        }

        public static string GetLocalName(XElement el) => el?.Name.LocalName ?? "";

        public static string GetTypeHint(XElement el)
        {
            // Type may appear as x:TypeArguments or full type in attributes; LocalName is primary.
            var typeAttr = el.Attributes().FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, "TypeArguments", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a.Name.LocalName, "Type", StringComparison.OrdinalIgnoreCase));
            return typeAttr?.Value ?? "";
        }

        public static bool NameContains(XElement el, string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            string ln = GetLocalName(el);
            if (ln.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string type = GetTypeHint(el);
            if (type.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // Full name with namespace: {ns}WriteLine
            string full = el.Name.ToString();
            return full.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string? GetAttribute(XElement el, string localName)
        {
            return el.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }

        /// <summary>Direct non-property child elements (activity body children).</summary>
        public static IEnumerable<XElement> ChildActivities(XElement el) =>
            el.Elements().Where(c => !IsPropertyElement(c));

        /// <summary>All descendant activity-like elements under this node (not including self).</summary>
        public static IEnumerable<XElement> DescendantActivities(XElement el) =>
            el.Descendants().Where(e => e != el && !IsStructural(e));
    }
}
