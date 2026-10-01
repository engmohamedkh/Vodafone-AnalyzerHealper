using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using AnalyzerHelper.Interfaces;
using AnalyzerHelper.Models;

namespace AnalyzerHelper.Rules
{
    /// <summary>VF-010 — checks SimulateClick / SimulateType / SendWindowMessages properties on UI activities.</summary>
    public sealed class SimulateAndSendWindowMessage : IAnalyzerRule
    {
        public string RuleId => "VF-010";
        public string RuleName => "SimulateAndSendWindowMessage";
        public string DefaultRecommendation =>
            "Check proper property or add annotation for the reason why it's not checked.";
        public bool RequiresUserInteraction => false;

        public IReadOnlyList<RuleCheckResult> Check(string filePath, string content)
        {
            var results = new List<RuleCheckResult>();
            if (!XamlActivityHelper.TryParse(content, out var doc) || doc == null) return results;

            foreach (var el in XamlActivityHelper.EnumerateActivities(doc))
            {
                string localName = XamlActivityHelper.GetLocalName(el);

                // Exclude image-based activities (e.g. ClickImage, HoverImage) as they do not support simulate/window messages
                if (localName.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                bool isTypeActivity = XamlActivityHelper.NameContains(el, "TypeInto") ||
                                     XamlActivityHelper.NameContains(el, "TypeSecureText");

                bool isClickActivity = !isTypeActivity && (
                    XamlActivityHelper.NameContains(el, "Click") ||
                    XamlActivityHelper.NameContains(el, "Hover"));

                bool isHotkeyActivity = XamlActivityHelper.NameContains(el, "SendHotkey") ||
                                       XamlActivityHelper.NameContains(el, "Hotkey");

                bool hasSimulate = false;
                bool hasSendWindowMessages = false;
                bool isSimulateUsed = false;
                bool isSendWindowMessagesUsed = false;

                if (isTypeActivity || HasAttributeOrElement(el, "SimulateType"))
                {
                    // Type and secure type activities have both SendWindowMessages and SimulateType
                    hasSimulate = true;
                    hasSendWindowMessages = true;
                    isSimulateUsed = IsPropertyTrue(el, "SimulateType");
                    isSendWindowMessagesUsed = IsPropertyTrue(el, "SendWindowMessages");
                }
                else if (isClickActivity || HasAttributeOrElement(el, "SimulateClick") || HasAttributeOrElement(el, "SimulateHover"))
                {
                    // Click and other UI activities have both SendWindowMessages and SimulateClick/SimulateHover
                    hasSimulate = true;
                    hasSendWindowMessages = true;
                    isSimulateUsed = IsPropertyTrue(el, "SimulateClick") || IsPropertyTrue(el, "SimulateHover");
                    isSendWindowMessagesUsed = IsPropertyTrue(el, "SendWindowMessages");
                }
                else if (isHotkeyActivity)
                {
                    // Hotkey activities have SendWindowMessages
                    hasSimulate = false;
                    hasSendWindowMessages = true;
                    isSendWindowMessagesUsed = IsPropertyTrue(el, "SendWindowMessages");
                }
                else if (HasAttributeOrElement(el, "SendWindowMessages"))
                {
                    hasSendWindowMessages = true;
                    hasSimulate = HasAttributeOrElement(el, "Simulate") ||
                                  HasAttributeOrElement(el, "SimulateClick") ||
                                  HasAttributeOrElement(el, "SimulateType");

                    isSendWindowMessagesUsed = IsPropertyTrue(el, "SendWindowMessages");
                    isSimulateUsed = IsPropertyTrue(el, "SimulateClick") ||
                                     IsPropertyTrue(el, "SimulateType") ||
                                     IsPropertyTrue(el, "Simulate");
                }
                else
                {
                    continue;
                }

                string display = XamlActivityHelper.GetDisplayName(el);

                // Validation matches original SimulateAndSendWindowMessage logic:
                if (hasSimulate && hasSendWindowMessages)
                {
                    if (!isSimulateUsed && !isSendWindowMessagesUsed)
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"The following activity: {display} has simulate and sendwindowmessages properties but not checked.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation
                        });
                    }
                }
                else if (hasSimulate)
                {
                    if (!isSimulateUsed)
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"The following activity: {display} has simulate property but not checked.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation
                        });
                    }
                }
                else if (hasSendWindowMessages)
                {
                    if (!isSendWindowMessagesUsed)
                    {
                        results.Add(new RuleCheckResult
                        {
                            RuleId = RuleId,
                            RuleName = RuleName,
                            Level = RuleLevel.Warning,
                            Message = $"The following activity: {display} has sendwindowmessages property but not checked.",
                            FilePath = filePath,
                            Recommendation = DefaultRecommendation
                        });
                    }
                }
            }

            return results;
        }

        private static bool HasAttributeOrElement(XElement el, string propertyName)
        {
            return el.Attributes().Any(a =>
                string.Equals(a.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase))
                || el.Elements().Any(e =>
                    string.Equals(e.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase) ||
                    e.Name.LocalName.EndsWith("." + propertyName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsPropertyTrue(XElement el, string propertyName)
        {
            // 1. Check XML attribute on activity element
            var attr = el.Attributes().FirstOrDefault(a =>
                string.Equals(a.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase));
            if (attr != null)
            {
                string val = attr.Value?.Trim() ?? "";
                if (string.Equals(val, "True", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(val, "[True]", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // 2. Check child property element (e.g. <ui:TypeInto.SimulateType>True</ui:TypeInto.SimulateType>)
            var propEl = el.Elements().FirstOrDefault(e =>
                string.Equals(e.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase) ||
                e.Name.LocalName.EndsWith("." + propertyName, StringComparison.OrdinalIgnoreCase));
            if (propEl != null)
            {
                string text = string.Concat(propEl.DescendantNodes().OfType<XText>().Select(t => t.Value)).Trim();
                if (string.Equals(text, "True", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(text, "[True]", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var valAttr = propEl.DescendantsAndSelf().SelectMany(x => x.Attributes()).FirstOrDefault(a =>
                    string.Equals(a.Name.LocalName, "Value", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.Name.LocalName, "ExpressionText", StringComparison.OrdinalIgnoreCase));
                if (valAttr != null)
                {
                    string v = valAttr.Value?.Trim() ?? "";
                    if (string.Equals(v, "True", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(v, "[True]", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
