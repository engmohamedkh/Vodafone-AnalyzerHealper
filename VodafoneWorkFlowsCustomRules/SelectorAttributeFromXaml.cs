using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UiPath.Studio.Activities.Api;
using UiPath.Studio.Activities.Api.Analyzer;
using UiPath.Studio.Activities.Api.Analyzer.Rules;
using UiPath.Studio.Analyzer.Models;

namespace SelectorAttributeFromXamlSpace
{
	/// <summary>
	/// Workflow-level rule: reads the workflow XAML file and validates all Selector="..." values
	/// for unstable attributes (ctrlid, idx, etc.). Use when activity-level selector read returns null.
	/// </summary>
	public class SelectorAttributeFromXaml : IRegisterAnalyzerConfiguration
	{
		const string RuleID = "VF-056";
		const string RuleName = "Selector Attribute";
		const string WFAnalyzerVersion = "WorkflowAnalyzerV4";

		static readonly string[] UnstableAttributes = new[]
		{
			"ctrlid", "idx",
			"sessionid", "session_id", "processid", "process_id",
			"dynamicid", "dynamic_id", "runtimeid", "runtime_id"
		};

		static readonly string RecommendedAttributes = "automationId, name, role, class, aaname, title";

		/// <summary>Matches Selector="...value..." where value can contain ' and escaped " as &quot;</summary>
		static readonly Regex SelectorDoubleQuoteRegex = new Regex(
			@"Selector\s*=\s*""((?:[^""]|&quot;)*)""",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		/// <summary>Matches Selector='...value...' where value can contain " and escaped ' as &apos;</summary>
		static readonly Regex SelectorSingleQuoteRegex = new Regex(
			@"Selector\s*=\s*'((?:[^']|&apos;)*)'",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		static readonly Regex AttributeNameRegex = new Regex(
			@"\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		static readonly Regex AttributePairRegex = new Regex(
			@"\s+([a-zA-Z][a-zA-Z0-9_]*)\s*=\s*(['""])(.*?)\2",
			RegexOptions.Compiled | RegexOptions.CultureInvariant);

		public void Initialize(IAnalyzerConfigurationService workflowAnalyzerConfigService)
		{
			if (!workflowAnalyzerConfigService.HasFeature(WFAnalyzerVersion))
				return;
			var newRule = new Rule<IWorkflowModel>(RuleName, RuleID, Evaluate)
			{
				ErrorLevel = System.Diagnostics.TraceLevel.Warning,
				RecommendationMessage = "Use stable selector attributes: " + RecommendedAttributes + ". Avoid: ctrlid, idx, dynamic aaname, session-based IDs."
			};
			workflowAnalyzerConfigService.AddRule<IWorkflowModel>(newRule);
		}

		private static List<string> FindUnstableAttributesInSelector(string selectorXml)
		{
			if (string.IsNullOrWhiteSpace(selectorXml))
				return new List<string>();
			string decoded = selectorXml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
			var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match m in AttributeNameRegex.Matches(decoded))
			{
				if (m.Success && m.Groups.Count >= 2)
				{
					string attr = m.Groups[1].Value.Trim();
					if (UnstableAttributes.Any(u => string.Equals(attr, u, StringComparison.OrdinalIgnoreCase)))
						found.Add(attr);
					if (attr.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0 &&
						attr.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
						found.Add(attr);
				}
			}
			return found.ToList();
		}

		private static List<string> FindUnstableAttributesWithValuesInSelector(string selectorXml)
		{
			if (string.IsNullOrWhiteSpace(selectorXml))
				return new List<string>();

			string decoded = selectorXml.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
			var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (Match m in AttributePairRegex.Matches(decoded))
			{
				if (!m.Success || m.Groups.Count < 4)
					continue;

				string attr = m.Groups[1].Value.Trim();
				string value = m.Groups[3].Value.Trim();
				bool isExplicitUnstable = UnstableAttributes.Any(u => string.Equals(attr, u, StringComparison.OrdinalIgnoreCase));
				bool isSessionLikeId = attr.IndexOf("session", StringComparison.OrdinalIgnoreCase) >= 0
					&& attr.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0;

				if (isExplicitUnstable || isSessionLikeId)
					found.Add(string.Format("{0}='{1}'", attr, value));
			}

			return found.ToList();
		}

		/// <summary>Get workflow XAML file path: try Directory + RelativePath with \ and /.</summary>
		private static string GetWorkflowFilePath(IWorkflowModel workflow)
		{
			string dir = workflow.Project?.Directory?.ToString()?.TrimEnd('\\', '/') ?? "";
			string rel = workflow.RelativePath?.Replace('/', Path.DirectorySeparatorChar).TrimStart('\\', '/') ?? "";
			if (string.IsNullOrEmpty(dir)) return null;
			string path = Path.Combine(dir, rel);
			return File.Exists(path) ? path : null;
		}

		/// <summary>UI activity tag names in XAML that contain a Selector (e.g. ui:Click, ui:GetValue).</summary>
		static readonly string[] UiActivityTagNames = new[]
		{
			"Click", "GetValue", "TypeInto", "Hover", "SetValue", "SelectItem", "ElementExists", "Activate",
			"FindElement", "WaitForElement", "AttachWindow", "AttachBrowser", "DoubleClick", "RightClick",
			"InputMethod", "SendHotkey", "GetValue"
		};

		/// <summary>Decode selector value from XAML attribute (XML parser decodes when reading, so we compare decoded.)</summary>
		private static string DecodeSelectorValue(string raw)
		{
			if (string.IsNullOrEmpty(raw)) return raw;
			return raw.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"");
		}

		/// <summary>Normalize selector for comparison (collapse whitespace).</summary>
		private static string NormalizeSelectorForCompare(string decoded)
		{
			if (string.IsNullOrEmpty(decoded)) return decoded;
			return Regex.Replace(decoded.Trim(), @"\s+", " ");
		}

		/// <summary>Get activity DisplayName for this selector using XDocument: find Target with matching Selector, then first ancestor with DisplayName.</summary>
		private static string GetActivityDisplayNameFromSelector(string xamlContent, string selectorValue)
		{
			if (string.IsNullOrWhiteSpace(xamlContent) || string.IsNullOrWhiteSpace(selectorValue))
				return null;
			try
			{
				string decodedSelector = DecodeSelectorValue(selectorValue);
				string normalizedSelector = NormalizeSelectorForCompare(decodedSelector);
				var doc = XDocument.Parse(xamlContent);

				foreach (var target in doc.Descendants())
				{
					if (target.Name.LocalName != "Target")
						continue;
					var selectorAttr = target.Attributes().FirstOrDefault(a =>
						string.Equals(a.Name.LocalName, "Selector", StringComparison.OrdinalIgnoreCase));
					if (selectorAttr == null)
						continue;
					string targetSelectorDecoded = selectorAttr.Value;
					if (string.IsNullOrEmpty(targetSelectorDecoded))
						continue;
					string targetNormalized = NormalizeSelectorForCompare(targetSelectorDecoded);
					if (targetNormalized != normalizedSelector)
						continue;

					foreach (var parent in target.Ancestors())
					{
						var displayAttr = parent.Attributes().FirstOrDefault(a =>
							string.Equals(a.Name.LocalName, "DisplayName", StringComparison.OrdinalIgnoreCase));
						if (displayAttr != null && !string.IsNullOrWhiteSpace(displayAttr.Value))
						{
							string localName = parent.Name.LocalName ?? "";
							if (localName == "Target")
								continue;
							if (localName.EndsWith(".Target", StringComparison.OrdinalIgnoreCase))
								continue;
							return displayAttr.Value.Trim();
						}
					}
				}
			}
			catch
			{
				// XAML parse or traversal failed; fall back to regex
			}
			return null;
		}

		/// <summary>Fallback: find activity DisplayName by scanning backward in raw XAML for nearest ui:XXX tag with DisplayName.</summary>
		private static string GetActivityDisplayNameForSelectorPosition(string xamlContent, int selectorPosition)
		{
			if (string.IsNullOrEmpty(xamlContent) || selectorPosition <= 0) return null;
			string textBefore = xamlContent.Substring(0, selectorPosition);
			int lastTagIndex = -1;
			foreach (string tag in UiActivityTagNames)
			{
				string openTag = "<ui:" + tag;
				int idx = textBefore.LastIndexOf(openTag, StringComparison.OrdinalIgnoreCase);
				if (idx > lastTagIndex)
					lastTagIndex = idx;
			}
			if (lastTagIndex < 0) return null;
			int chunkEnd = Math.Min(lastTagIndex + 1200, textBefore.Length);
			string tagChunk = textBefore.Substring(lastTagIndex, chunkEnd - lastTagIndex);
			var displayNameMatch = Regex.Match(tagChunk, @"DisplayName\s*=\s*""((?:[^""]|&quot;)*)""", RegexOptions.CultureInvariant);
			if (displayNameMatch.Success && displayNameMatch.Groups.Count >= 2)
				return displayNameMatch.Groups[1].Value.Replace("&quot;", "\"").Trim();
			return null;
		}

		/// <summary>Extract all Selector attribute values from XAML string. Handles Selector="...value..." where value can contain quotes.</summary>
		private static IEnumerable<(string selectorValue, int position)> EnumerateSelectorsInXaml(string xamlContent)
		{
			if (string.IsNullOrEmpty(xamlContent)) yield break;
			foreach (Match m in SelectorDoubleQuoteRegex.Matches(xamlContent))
			{
				if (m.Success && m.Groups.Count >= 2)
				{
					string val = m.Groups[1].Value;
					if (!string.IsNullOrWhiteSpace(val))
						yield return (val, m.Index);
				}
			}
			foreach (Match m in SelectorSingleQuoteRegex.Matches(xamlContent))
			{
				if (m.Success && m.Groups.Count >= 2)
				{
					string val = m.Groups[1].Value;
					if (!string.IsNullOrWhiteSpace(val))
						yield return (val, m.Index);
				}
			}
		}

		private InspectionResult Evaluate(IWorkflowModel workflow, Rule theNewRule)
		{
			var messageList = new List<string>();
			try
			{
				string filePath = GetWorkflowFilePath(workflow);
				if (string.IsNullOrEmpty(filePath))
					return new InspectionResult() { HasErrors = false };

				string xamlContent = File.ReadAllText(filePath);
				foreach (var (selectorValue, position) in EnumerateSelectorsInXaml(xamlContent))
				{
					List<string> unstable = FindUnstableAttributesInSelector(selectorValue);
					if (unstable.Count > 0)
					{
						List<string> unstableWithValues = FindUnstableAttributesWithValuesInSelector(selectorValue);
						string activityName = GetActivityDisplayNameFromSelector(xamlContent, selectorValue)
							?? GetActivityDisplayNameForSelectorPosition(xamlContent, position);
						string activityLabel = !string.IsNullOrWhiteSpace(activityName)
							? string.Format("Activity '{0}'", activityName)
							: "Selector";
						string unstableText = unstableWithValues.Count > 0
							? string.Join(", ", unstableWithValues)
							: string.Join(", ", unstable);

						messageList.Add(string.Format("{0} uses unstable attribute(s): {1}.",
							activityLabel, unstableText));
					}
				}
			}
			catch (Exception)
			{
				return new InspectionResult() { HasErrors = false };
			}

			if (messageList.Count == 0)
				return new InspectionResult() { HasErrors = false };

			return new InspectionResult()
			{
				HasErrors = true,
				Messages = messageList,
				RecommendationMessage = "Use stable selector attributes: " + RecommendedAttributes + ". Avoid: ctrlid, idx, dynamic aaname, session-based IDs.",
				ErrorLevel = theNewRule.DefaultErrorLevel
			};
		}
	}
}
