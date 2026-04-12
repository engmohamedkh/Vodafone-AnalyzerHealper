using AnalyzerHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;

namespace AnalyzerHelper
{
    /// <summary>
    /// Scans every &lt;Catch&gt; block in a XAML file and ensures the last activity
    /// inside its ActivityAction body is a &lt;Rethrow&gt;.
    ///
    /// Rules:
    ///  - If the Catch's inner Sequence already ends with &lt;Rethrow ...&gt; → nothing to do.
    ///  - If it does NOT end with Rethrow → append one just before &lt;/Sequence&gt;.
    ///  - The new Rethrow gets a unique WorkflowViewState.IdRef ("Rethrow_N") where N
    ///    continues from the highest existing Rethrow_N counter found in the whole file,
    ///    so existing counters are never disturbed.
    ///  - A minimal HintSize attribute is added (matching the style seen in the sample files).
    /// </summary>
    public class EnsureCatchRethrowAction : IFixerAction
    {
        public string Name => "Ensure Catch Blocks End with Rethrow";

        // Rule codes shown as badges next to the action name in the UI.
        // ST-NMG-009 = "Catch block must rethrow the exception"
        public IReadOnlyList<string> RuleCodes => new[] { "ST-NMG-009" };

        public string Description =>
            "Checks every <Catch> block in the XAML. If the inner Sequence does not " +
            "already end with a <Rethrow> activity, one is appended automatically. " +
            "\nThe Rethrow IdRef counter (Rethrow_N) continues from the highest N already " +
            "present in the file so no existing references are disturbed.";

        // ── Patterns ──────────────────────────────────────────────────────────

        // Finds all existing Rethrow_N counters in the file (any attribute position)
        private static readonly Regex _rethrowIdPattern =
            new Regex(@"Rethrow_(\d+)", RegexOptions.Compiled);

        // Matches a complete <Catch ...> ... </Catch> block (non-greedy, dotall)
        // We use a simple string approach rather than pure regex because XML nesting
        // makes a pure regex fragile – see ProcessCatches() below.

        // Matches the closing </Sequence> that is the DIRECT child body of an
        // ActivityAction inside a Catch.  We locate the right one by walking the text.

        // Detects whether the content just before </Sequence> is already a Rethrow tag.
        private static readonly Regex _endsWithRethrow =
            new Regex(@"<Rethrow\b[^/]*/>\s*$", RegexOptions.Compiled);

        // ─────────────────────────────────────────────────────────────────────
        public bool Run(string filePath)
        {
            string original = File.ReadAllText(filePath);

            // Quick bail-out: no Catch blocks at all
            if (!original.Contains("<Catch ") && !original.Contains("<Catch>"))
                return false;

            // Find the highest existing Rethrow_N so we can continue the sequence
            int nextId = HighestRethrowId(original) + 1;

            string result = ProcessCatches(original, ref nextId);

            if (result == original)
                return false;

            File.WriteAllText(filePath, result);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Scan for Rethrow_N and return highest N (or 0)
        // ─────────────────────────────────────────────────────────────────────
        private static int HighestRethrowId(string xml)
        {
            int max = 0;
            foreach (Match m in _rethrowIdPattern.Matches(xml))
            {
                if (int.TryParse(m.Groups[1].Value, out int n) && n > max)
                    max = n;
            }
            return max;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Walk through all <Catch …> blocks and patch each one
        // ─────────────────────────────────────────────────────────────────────
        private static string ProcessCatches(string xml, ref int nextId)
        {
            // We process the string from left to right, rebuilding it.
            var sb = new System.Text.StringBuilder();
            int cursor = 0;

            while (true)
            {
                // Find the next opening <Catch (could be <Catch> or <Catch x:TypeArguments=...)
                int catchStart = FindTag(xml, cursor, "<Catch");
                if (catchStart < 0)
                {
                    sb.Append(xml, cursor, xml.Length - cursor);
                    break;
                }

                // Append everything up to (and including) the <Catch ...> opening tag
                int catchTagEnd = xml.IndexOf('>', catchStart) + 1;
                sb.Append(xml, cursor, catchTagEnd - cursor);
                cursor = catchTagEnd;

                // Now find the matching </Catch> by counting depth of nested XML
                int catchBodyEnd = FindClosingTag(xml, cursor, "Catch");
                if (catchBodyEnd < 0)
                {
                    // Malformed – leave it alone and move on
                    continue;
                }

                // Extract the body of this Catch block
                string catchBody = xml.Substring(cursor, catchBodyEnd - cursor);

                // Patch the body: find the inner Sequence inside the ActivityAction
                // and ensure it ends with Rethrow
                string patchedBody = PatchCatchBody(catchBody, ref nextId);

                sb.Append(patchedBody);

                // Append </Catch>
                int closingTagEnd = xml.IndexOf('>', catchBodyEnd) + 1;
                sb.Append(xml, catchBodyEnd, closingTagEnd - catchBodyEnd);
                cursor = closingTagEnd;
            }

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Within one Catch body, find each inner Sequence and patch it
        // ─────────────────────────────────────────────────────────────────────
        private static string PatchCatchBody(string catchBody, ref int nextId)
        {
            // A Catch contains:
            //   <ActivityAction …>
            //     <ActivityAction.Argument> … </ActivityAction.Argument>
            //     <Sequence DisplayName="…"> … </Sequence>
            //   </ActivityAction>
            //
            // We need to find the Sequence(s) that are DIRECT children of ActivityAction
            // (not nested deeper) and make sure each ends with Rethrow.

            var sb = new System.Text.StringBuilder();
            int cursor = 0;

            while (true)
            {
                int seqStart = FindTag(catchBody, cursor, "<Sequence");
                if (seqStart < 0)
                {
                    sb.Append(catchBody, cursor, catchBody.Length - cursor);
                    break;
                }

                // Append up to and including <Sequence …>
                int seqTagEnd = catchBody.IndexOf('>', seqStart) + 1;
                sb.Append(catchBody, cursor, seqTagEnd - cursor);
                cursor = seqTagEnd;

                // Find matching </Sequence>
                int seqBodyEnd = FindClosingTag(catchBody, cursor, "Sequence");
                if (seqBodyEnd < 0)
                {
                    continue;
                }

                string seqBody = catchBody.Substring(cursor, seqBodyEnd - cursor);

                // Check: does this sequence already end with a Rethrow?
                if (!EndsWithRethrow(seqBody))
                {
                    // Build indent: match whatever whitespace precedes the last tag
                    string indent = DetectIndent(seqBody);
                    string rethrowTag = BuildRethrowTag(nextId, indent);
                    nextId++;
                    seqBody = seqBody.TrimEnd() + Environment.NewLine + rethrowTag + Environment.NewLine;
                }

                sb.Append(seqBody);

                // Append </Sequence>
                int closingTagEnd = catchBody.IndexOf('>', seqBodyEnd) + 1;
                sb.Append(catchBody, seqBodyEnd, closingTagEnd - seqBodyEnd);
                cursor = closingTagEnd;

                // Only patch the FIRST (direct-child) Sequence – deeper ones
                // are not catch handlers.
                break;
            }

            // Append remainder
            if (cursor < catchBody.Length)
                sb.Append(catchBody, cursor, catchBody.Length - cursor);

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Returns true if the sequence body ends with a Rethrow tag (ignoring whitespace).</summary>
        private static bool EndsWithRethrow(string seqBody)
        {
            // Strip trailing whitespace and look for a self-closing Rethrow
            string trimmed = seqBody.TrimEnd();
            return trimmed.EndsWith("/>") &&
                   _endsWithRethrow.IsMatch(trimmed);
        }

        /// <summary>
        /// Detect the indentation of the last non-empty line in the sequence body.
        /// This makes the inserted Rethrow align with its siblings.
        /// </summary>
        private static string DetectIndent(string seqBody)
        {
            var lines = seqBody.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                int spaces = 0;
                foreach (char c in line)
                {
                    if (c == ' ') { spaces++; continue; }
                    if (c == '\t') { spaces += 4; continue; }
                    break;
                }
                // Return the raw leading whitespace
                int len = line.Length - line.TrimStart().Length;
                return line.Substring(0, len);
            }
            return "              "; // fallback
        }

        /// <summary>Builds a Rethrow self-closing tag with the sample file's attribute style.</summary>
        private static string BuildRethrowTag(int id, string indent)
        {
            // Sample from file:
            // <Rethrow DisplayName="Rethrow - SE"
            //          sap:VirtualizedContainerService.HintSize="338,25"
            //          sap2010:WorkflowViewState.IdRef="Rethrow_3" />
            return $"{indent}<Rethrow DisplayName=\"Rethrow - SE\" " +
                   $"sap:VirtualizedContainerService.HintSize=\"338,25\" " +
                   $"sap2010:WorkflowViewState.IdRef=\"Rethrow_{id}\" />";
        }

        // ── Low-level string search helpers ──────────────────────────────────

        /// <summary>Find the start index of the next occurrence of tagPrefix from pos.</summary>
        private static int FindTag(string xml, int from, string tagPrefix)
        {
            int idx = xml.IndexOf(tagPrefix, from, StringComparison.Ordinal);
            if (idx < 0) return -1;
            // Make sure the character after the prefix is either '>' , ' ', '\r', '\n', '\t'
            // (so "<CatchSomethingElse" doesn't match "<Catch")
            if (idx + tagPrefix.Length < xml.Length)
            {
                char next = xml[idx + tagPrefix.Length];
                if (next != '>' && next != ' ' && next != '\r' && next != '\n' && next != '\t' && next != '/')
                    return FindTag(xml, idx + 1, tagPrefix);
            }
            return idx;
        }

        /// <summary>
        /// Starting at <paramref name="from"/>, find the position of the closing
        /// &lt;/tagName&gt; that matches the nesting depth (i.e. accounts for nested
        /// open/close pairs of the same tag name).
        /// Returns the index of the '&lt;' of the closing tag, or -1 if not found.
        /// </summary>
        private static int FindClosingTag(string xml, int from, string tagName)
        {
            string open = "<" + tagName;
            string close = "</" + tagName;
            int depth = 1;
            int cursor = from;

            while (depth > 0 && cursor < xml.Length)
            {
                int nextOpen = FindTag(xml, cursor, open);
                int nextClose = xml.IndexOf(close, cursor, StringComparison.Ordinal);

                if (nextClose < 0) return -1; // malformed

                if (nextOpen >= 0 && nextOpen < nextClose)
                {
                    // The open tag we found – is it self-closing?
                    int gtPos = xml.IndexOf('>', nextOpen);
                    if (gtPos > 0 && xml[gtPos - 1] == '/')
                    {
                        // Self-closing → doesn't increase depth
                        cursor = gtPos + 1;
                    }
                    else
                    {
                        depth++;
                        cursor = nextOpen + open.Length;
                    }
                }
                else
                {
                    depth--;
                    if (depth == 0)
                        return nextClose;
                    cursor = nextClose + close.Length;
                }
            }
            return -1;
        }
    }
}