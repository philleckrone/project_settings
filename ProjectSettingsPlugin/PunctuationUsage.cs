using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProjectSettings
{
    /// <summary>
    /// The project's settings that decide how Paratext's punctuation inventory cuts the text.
    /// </summary>
    public sealed class PunctuationRules
    {
        /// <summary>Whole sequences (AdvancedView = True) rather than single characters in context.</summary>
        public bool Advanced;

        /// <summary>PunctuationShowWhitespaceBetweenQuotes: keep "_" between two quotation marks of the same kind.</summary>
        public bool ShowWhitespaceBetweenQuotes;

        /// <summary>Opening and closing quotation marks of the three levels (with alternates); null = quotes not set up.</summary>
        public HashSet<string> BeginQuotes, EndQuotes;

        /// <summary>Characters of the Alphabetic characters list (CharacterCategorizer word-forming characters).</summary>
        public readonly HashSet<int> WordForming = new HashSet<int>();

        /// <summary>Word medial punctuation (.ldml wordFormingPunctuation; "-" without language settings).</summary>
        public readonly HashSet<int> Medial = new HashSet<int>();

        /// <summary>Extra diacritics (.ldml diacritics) that are not Unicode diacritics.</summary>
        public readonly HashSet<int> Diacritics = new HashSet<int>();

        /// <summary>
        /// The rules from Settings.xml and the language settings rows (LdmlFile.Read), as
        /// ScrLanguage.CreateCharacterCategorizer and QuotationMarkInfo build them.
        /// </summary>
        public static PunctuationRules From(List<SettingItem> settings, List<SettingItem> language)
        {
            var r = new PunctuationRules();
            r.Advanced = SettingsFile.ValueOf(settings, "AdvancedView") == "True";
            string ws = SettingsFile.ValueOf(settings, "PunctuationShowWhitespaceBetweenQuotes") ?? "";
            r.ShowWhitespaceBetweenQuotes = ws == "True" || ws == "T";

            var begin = new HashSet<string>(StringComparer.Ordinal);
            var end = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in new[] { "Quotes", "InnerQuotes", "InnerInnerQuotes" })
            {
                string[] marks = (SettingsFile.ValueOf(settings, name) ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (marks.Length > 0) begin.Add(marks[0]);
                if (marks.Length > 1) end.Add(marks[1]);
                if (marks.Length > 2) begin.Add(marks[2]);
                if (marks.Length > 3) end.Add(marks[3]);
            }
            if (begin.Count > 0 || end.Count > 0)
            {
                r.BeginQuotes = begin;
                r.EndQuotes = end;
            }

            string simple = language != null ? SettingsFile.ValueOf(language, "sil:simple") : null;
            string medial = language != null ? SettingsFile.ValueOf(language, "sil:exemplarCharacters[wordFormingPunctuation]") : null;
            string diacritics = language != null ? SettingsFile.ValueOf(language, "sil:exemplarCharacters[diacritics]") : null;
            if (string.IsNullOrEmpty(simple) && string.IsNullOrEmpty(medial))
            {
                r.Medial.Add('-');   // CharacterCategorizer's default
            }
            else
            {
                foreach (int cp in CodePoints(simple ?? ""))
                {
                    if (cp == '/' || char.IsWhiteSpace(char.ConvertFromUtf32(cp), 0)) continue;
                    UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
                    if (cat > UnicodeCategory.OtherLetter || cat == UnicodeCategory.ModifierLetter) r.WordForming.Add(cp);
                }
                foreach (string item in SafeSet(medial))
                    foreach (int cp in CodePoints(item))
                        if (!char.IsWhiteSpace(char.ConvertFromUtf32(cp), 0)) r.Medial.Add(cp);
                foreach (string item in SafeSet(diacritics))
                    foreach (int cp in CodePoints(item))
                    {
                        UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
                        bool unicodeDiacritic = cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark;
                        if (!unicodeDiacritic && !char.IsWhiteSpace(char.ConvertFromUtf32(cp), 0)) r.Diacritics.Add(cp);
                    }
            }
            return r;
        }

        private static IEnumerable<string> SafeSet(string set)
        {
            if (string.IsNullOrEmpty(set)) return new string[0];
            try
            {
                return LdmlFile.ParseSet(set);
            }
            catch (FormatException)
            {
                return new string[0];
            }
        }

        internal static IEnumerable<int> CodePoints(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    yield return char.ConvertToUtf32(s[i], s[i + 1]);
                    i++;
                }
                else
                {
                    yield return s[i];
                }
            }
        }

        /// <summary>CharacterCategorizer.IsWordFormingCharacter (for one UTF-16 unit, as the check passes them).</summary>
        public bool IsWordForming(char c)
        {
            if (c == '\u200D' || c == '\u200C') return true;
            UnicodeCategory cat = char.GetUnicodeCategory(c);
            if (cat <= UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.PrivateUse || cat == UnicodeCategory.OtherNotAssigned) return true;
            if (WordForming.Contains(c) || Diacritics.Contains(c)) return true;
            return Medial.Contains(c);
        }

        /// <summary>CharacterCategorizer.IsPunctuation.</summary>
        public bool IsPunctuation(char c)
        {
            if (Medial.Contains(c)) return true;
            UnicodeCategory cat = char.GetUnicodeCategory(c);
            if (cat >= UnicodeCategory.ConnectorPunctuation && cat <= UnicodeCategory.OtherSymbol) return !IsWordForming(c);
            return false;
        }
    }

    /// <summary>
    /// How often each punctuation-inventory item (_( ._ ,_\u00AB ...) occurs in the text: a port of
    /// Paratext's PunctuationCheck.GetReferencesInternal and ProcessPunctuationTokens (ParatextChecks,
    /// decompiled 2026-10-30). Verse text, headings and other text go through one processor, notes
    /// and figure captions in notes through another; a sequence ends at a paragraph, table cell,
    /// figure, chapter or verse number, a letter or a digit. Paratext-free; never writes.
    /// </summary>
    public sealed class PunctuationUsage
    {
        private readonly Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Up to three samples of the text where each item was found (for the tests).</summary>
        public readonly Dictionary<string, List<string>> Examples = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>How many samples to keep per item (the tests raise it).</summary>
        public static int ExampleLimit = 3;

        public int BookCount { get; private set; }

        /// <summary>Every item found, with its count (for the tests).</summary>
        public IDictionary<string, int> Items
        {
            get { return counts; }
        }

        public PunctuationUsage(IEnumerable<List<TextTok>> books, PunctuationRules rules)
        {
            foreach (List<TextTok> tokens in books)
            {
                if (tokens == null || tokens.Count == 0) continue;
                BookCount++;
                var main = new Processor(rules, counts, Examples);
                var notes = new Processor(rules, counts, Examples);
                foreach (TextTok tok in tokens)
                {
                    if (tok.Type == TextTokType.Note)
                    {
                        if (tok.NoteStart) notes.Finalize(true);
                        notes.Process(tok);
                    }
                    else
                    {
                        notes.Finalize(true);
                        main.Process(tok);
                    }
                }
                notes.Finalize(true);
                main.Finalize(true);
            }
        }

        public int Count(string item)
        {
            int n;
            return item != null && counts.TryGetValue(item, out n) ? n : 0;
        }

        private enum Kind
        {
            Whitespace,
            Punctuation,
            Number
        }

        private sealed class Piece
        {
            public Kind Kind;
            public string Text;
            public bool IsInitial, IsFinal;

            public override string ToString()
            {
                return Kind == Kind.Whitespace ? "_" : Kind == Kind.Number ? "" : Text;
            }
        }

        /// <summary>ProcessPunctuationTokens.</summary>
        private sealed class Processor
        {
            private readonly List<Piece> puncts = new List<Piece>();
            private readonly PunctuationRules rules;
            private readonly Dictionary<string, int> counts;
            private readonly Dictionary<string, List<string>> examples;
            private bool finalizedWithNumber;
            private string current = "", previous = "";
            private int at;

            public Processor(PunctuationRules rules, Dictionary<string, int> counts, Dictionary<string, List<string>> examples)
            {
                this.rules = rules;
                this.counts = counts;
                this.examples = examples;
            }

            public void Process(TextTok tok)
            {
                bool number = tok.Type == TextTokType.ChapterNumber || tok.Type == TextTokType.VerseNumber;
                if (tok.ParaStart || tok.CellStart || tok.IsFigure || number) Finalize(true);
                if (tok.ParaStart || tok.NoteStart || tok.CellStart || tok.IsFigure || number) AddWhitespace();
                if (number) return;
                string text = tok.Text;
                if (text.Length > 0)
                {
                    previous = current;
                    current = text;
                }
                at = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    at = i;
                    char c = text[i];
                    if (c == '\u200F') continue;
                    if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                    {
                        AddWhitespace(c);
                        i++;
                    }
                    else if (rules.IsPunctuation(c))
                    {
                        AddPunctuation(c.ToString());
                    }
                    else if (char.IsDigit(c))
                    {
                        if (finalizedWithNumber && puncts.Count == 1 && puncts[0].Kind == Kind.Punctuation)
                        {
                            puncts.Clear();
                            continue;
                        }
                        puncts.Add(new Piece { Kind = Kind.Number });
                        Finalize(false);
                    }
                    else if (char.IsWhiteSpace(c))
                    {
                        AddWhitespace(c);
                    }
                    else
                    {
                        Finalize(false);
                    }
                }
            }

            private void AddPunctuation(string mark)
            {
                bool initial = rules.BeginQuotes != null && rules.BeginQuotes.Contains(mark);
                bool final = rules.EndQuotes != null && rules.EndQuotes.Contains(mark);
                puncts.Add(new Piece { Kind = Kind.Punctuation, Text = mark, IsInitial = initial, IsFinal = final });
                int n = puncts.Count;
                // Two quotation marks of the same kind with a space between: the space is dropped.
                if ((!rules.Advanced || !rules.ShowWhitespaceBetweenQuotes) && n >= 3
                    && puncts[n - 2].Kind == Kind.Whitespace && puncts[n - 3].Kind == Kind.Punctuation
                    && ((puncts[n - 3].IsInitial && puncts[n - 1].IsInitial) || (puncts[n - 3].IsFinal && puncts[n - 1].IsFinal)))
                    puncts.RemoveAt(n - 2);
            }

            /// <summary>The spaces U+2000 to U+200B do not count as white space here.</summary>
            private void AddWhitespace(char c)
            {
                if (c < '\u2000' || c > '\u200B') AddWhitespace();
            }

            private void AddWhitespace()
            {
                if (puncts.Count == 0 || puncts[puncts.Count - 1].Kind != Kind.Whitespace)
                    puncts.Add(new Piece { Kind = Kind.Whitespace });
            }

            public void Finalize(bool addWhitespace)
            {
                finalizedWithNumber = puncts.Count > 0 && puncts[puncts.Count - 1].Kind == Kind.Number;
                if (puncts.Count == 0) return;   // the usual case (every letter ends a sequence): nothing to do
                if (!puncts.Any(p => p.Kind == Kind.Punctuation))
                {
                    puncts.Clear();
                    return;
                }
                if (addWhitespace) AddWhitespace();
                if (rules.Advanced) Add(string.Concat(puncts.Select(p => p.ToString())));
                else BasicFinalize();
                puncts.Clear();
            }

            /// <summary>Each punctuation mark with what is before and after it (a run of dots stays together).</summary>
            private void BasicFinalize()
            {
                for (int i = 0; i < puncts.Count; i++)
                {
                    if (puncts[i].Kind != Kind.Punctuation) continue;
                    int last = i;
                    while (puncts[i].ToString() == "." && last + 1 < puncts.Count && puncts[last + 1].ToString() == ".") last++;
                    string text = Prefix(i);
                    for (int j = i; j <= last; j++) text += puncts[j].ToString();
                    text += Suffix(last);
                    Add(text);
                    i = last;
                }
            }

            private string Prefix(int index)
            {
                for (int i = index - 1; i >= 0; i--)
                    if (puncts[i].Kind != Kind.Punctuation) return puncts[i].ToString();
                return "";
            }

            private string Suffix(int index)
            {
                for (int i = index; i < puncts.Count; i++)
                    if (puncts[i].Kind != Kind.Punctuation) return puncts[i].ToString();
                return "";
            }

            private void Add(string item)
            {
                int n;
                counts.TryGetValue(item, out n);
                counts[item] = n + 1;
                List<string> list;
                if (!examples.TryGetValue(item, out list)) examples[item] = list = new List<string>();
                if (list.Count < ExampleLimit)
                {
                    // The end of the previous token too: a sequence often starts there.
                    int from = Math.Max(0, at - 20);
                    string before = at < 20 ? previous.Substring(Math.Max(0, previous.Length - (20 - at))) + "\u2016" : "";
                    list.Add((before + current.Substring(from, Math.Min(current.Length, at + 10) - from)).Replace('\n', ' '));
                }
            }
        }
    }
}
