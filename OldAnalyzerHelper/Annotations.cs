using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

namespace AnalyzerHelper
{
    public static class Annotations
    {
        private static readonly string[] Fields = { "Component Name", "Description", "Pre Condition", "Post Condition", "PDD Section" };

        public static bool UpdateAnnotations(string filePath)
        {
            try
            {
                XDocument xdoc = XDocument.Load(filePath);
                XNamespace sap2010 = "http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation";

                var firstSeq = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Sequence");
                if (firstSeq == null) return false;

                firstSeq.SetAttributeValue("DisplayName", "Main");

                var annAttr = firstSeq.Attribute(sap2010 + "Annotation.AnnotationText");
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                var dict = Fields.ToDictionary(f => f, f => f == "Component Name" ? fileName : "#NA", StringComparer.OrdinalIgnoreCase);

                if (annAttr != null && !string.IsNullOrWhiteSpace(annAttr.Value))
                {
                    string currentKey = null;
                    var lines = annAttr.Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        string matchedKey = Fields.FirstOrDefault(k => trimmed.StartsWith(k.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                        if (matchedKey != null && trimmed.Contains(":"))
                        {
                            int idx = trimmed.IndexOf(':');
                            dict[matchedKey] = trimmed.Substring(idx + 1).Trim();
                            currentKey = matchedKey;
                        }
                        else if (currentKey != null)
                        {
                            dict[currentKey] += "\n" + trimmed;
                        }
                    }
                }

                string newAnn = string.Join("\n", Fields.Select(f => $"{f}: {dict[f]}"));

                if (annAttr == null || annAttr.Value != newAnn)
                    firstSeq.SetAttributeValue(sap2010 + "Annotation.AnnotationText", newAnn);
                else
                    return false;

                xdoc.Save(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
