using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjectSettings
{
    /// <summary>
    /// How often characters occur in the text of the project's books, so the report can tell the
    /// approved characters that are used from those that are not. It counts the text tokens Paratext's
    /// checks read (UsfmTextTokens): publishable text and notes, without markers, attributes, note
    /// callers or chapter and verse numbers. Paratext-free; never writes.
    /// </summary>
    public sealed class CharacterUsage
    {
        private readonly StringBuilder text = new StringBuilder();
        private readonly Dictionary<string, int> codePoints = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> elements = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> searched = new Dictionary<string, int>(StringComparer.Ordinal);
        // Combining marks not attached to a letter (or to another mark on a letter).
        private readonly Dictionary<string, int> loneMarks = new Dictionary<string, int>(StringComparer.Ordinal);
        private string joined;

        /// <summary>Number of books whose text was counted.</summary>
        public int BookCount { get; private set; }

        /// <summary>Counts the text tokens of each book (UsfmTextTokens.Tokenize). Empty books are skipped.</summary>
        public CharacterUsage(IEnumerable<List<TextTok>> books)
        {
            foreach (List<TextTok> tokens in books)
            {
                if (tokens == null || tokens.Count == 0) continue;
                string clean = UsfmTextTokens.Text(tokens).Normalize(NormalizationForm.FormC);
                text.Append(clean).Append('\n');
                CountOne(clean);
                BookCount++;
            }
        }

        private void CountOne(string s)
        {
            bool afterLetter = false;   // the previous code point is a letter, or a mark on one
            for (int i = 0; i < s.Length; i++)
            {
                int len = char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? 2 : 1;
                string cp = s.Substring(i, len);
                Add(codePoints, cp);
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(cp, 0);
                if (IsMark(cat))
                {
                    if (!afterLetter) Add(loneMarks, cp);
                }
                else
                {
                    afterLetter = char.IsLetter(cp, 0);
                }
                if (len == 2) i++;
            }
            TextElementEnumerator e = StringInfo.GetTextElementEnumerator(s);
            while (e.MoveNext())
                Add(elements, e.GetTextElement());
        }

        private static void Add(Dictionary<string, int> counts, string key)
        {
            int n;
            counts.TryGetValue(key, out n);
            counts[key] = n + 1;
        }

        private static bool IsMark(UnicodeCategory cat)
        {
            return cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark;
        }

        /// <summary>
        /// How often an item (a character, a letter with its marks, or a combination such as ch)
        /// occurs in the text. A single code point is counted everywhere, also inside a longer
        /// combination (the e of ie), except a combining mark, which counts only where it is not on
        /// a letter (Paratext's character inventory takes a letter and its marks as one character);
        /// a single letter-plus-marks only where it stands as one unit; a combination by a search
        /// of the text that respects character boundaries (marks stay with their letter).
        /// </summary>
        public int Count(string item)
        {
            if (string.IsNullOrEmpty(item)) return 0;
            string key = item.Normalize(NormalizationForm.FormC);
            int n;
            if (IsOneCodePoint(key) && IsMark(CharUnicodeInfo.GetUnicodeCategory(key, 0)))
                return loneMarks.TryGetValue(key, out n) ? n : 0;
            if (codePoints.ContainsKey(key) || IsOneCodePoint(key))
                return codePoints.TryGetValue(key, out n) ? n : 0;
            if (new StringInfo(key).LengthInTextElements == 1)
                return elements.TryGetValue(key, out n) ? n : 0;
            if (searched.TryGetValue(key, out n)) return n;
            if (joined == null) joined = text.ToString();
            n = 0;
            for (int at = joined.IndexOf(key, StringComparison.Ordinal); at >= 0; at = joined.IndexOf(key, at + 1, StringComparison.Ordinal))
                if (OnCharacterBoundaries(joined, at, key.Length)) n++;
            searched[key] = n;
            return n;
        }

        /// <summary>
        /// Whether text[start, start+length) neither starts with a mark that belongs to the letter
        /// before it (YUI's "\u0289\u0303g": the tilde is on \u0289, so "\u25CC\u0303g" is not there) nor ends just before a
        /// mark that belongs to its last letter.
        /// </summary>
        private static bool OnCharacterBoundaries(string text, int start, int length)
        {
            if (IsMark(CharUnicodeInfo.GetUnicodeCategory(text, start)) && start > 0)
            {
                int prev = char.IsLowSurrogate(text[start - 1]) && start > 1 ? start - 2 : start - 1;
                UnicodeCategory before = CharUnicodeInfo.GetUnicodeCategory(text, prev);
                if (char.IsLetter(text, prev) || IsMark(before)) return false;
            }
            int end = start + length;
            return end >= text.Length || !IsMark(CharUnicodeInfo.GetUnicodeCategory(text, end));
        }

        private static bool IsOneCodePoint(string s)
        {
            return s.Length == 1 || (s.Length == 2 && char.IsHighSurrogate(s[0]) && char.IsLowSurrogate(s[1]));
        }
    }
}
