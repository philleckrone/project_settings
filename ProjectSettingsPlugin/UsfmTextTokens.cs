using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectSettings
{
    public enum TextTokType
    {
        /// <summary>Verse text, headings and other publishable text (Paratext's Verse, Section, Other).</summary>
        Main,
        Note,
        ChapterNumber,
        VerseNumber
    }

    /// <summary>One piece of text as Paratext's checks see it (ParatextTextToken).</summary>
    public sealed class TextTok
    {
        public string Text = "";
        public TextTokType Type;
        public bool ParaStart;
        public bool NoteStart;
        public bool CellStart;
        public bool IsFigure;
    }

    /// <summary>
    /// Splits a book's USFM into the text tokens Paratext's inventories read, after
    /// ScrParser.GetTextTokens (ParatextData, decompiled 2026-10-30):
    /// - only publishable text: a paragraph or character style with nonpublishable in its
    ///   \TextProperties (\id, \rem...) is skipped;
    /// - note text is a separate token type; note callers, chapter and verse numbers are not text;
    /// - a paragraph, note or table cell start is marked (an empty token when no text follows);
    /// - \fig gives its caption and its ref as tokens (USFM 2: fields 6 and 7 of |-separated).
    /// - an unknown marker starts a paragraph, except inside a note (UsfmParser.DetermineUnknownTokenType).
    /// USFM 3 attributes (|... before a closing marker) and milestones are not text. Paratext-free.
    /// </summary>
    public static class UsfmTextTokens
    {
        private static readonly Regex Marker = new Regex(@"\\(\+?)([A-Za-z0-9_-]*)(\*?)");
        private static readonly Regex Cell = new Regex(@"^t[hc]r?c?\d+(-\d+)?$");

        public static List<TextTok> Tokenize(string usfm, StyleSheets styles)
        {
            var tokens = new List<TextTok>();
            if (string.IsNullOrEmpty(usfm)) return tokens;
            string s = usfm.Replace("\r\n", "\n").Replace('\r', '\n');

            string para = null;                       // current paragraph marker
            string note = null;                       // current note marker
            var chars = new List<string>();           // open character markers (verse text)
            var noteChars = new List<string>();       // open character markers inside the note
            int pos = 0;

            while (pos < s.Length)
            {
                int bs = s.IndexOf('\\', pos);
                if (bs < 0) bs = s.Length;
                if (bs > pos)
                {
                    string run = s.Substring(pos, bs - pos);
                    // USFM 3 attributes: from '|' up to the closing marker of a character style.
                    if ((chars.Count > 0 || noteChars.Count > 0) && bs < s.Length && IsEndMarkerAt(s, bs))
                    {
                        int bar = run.IndexOf('|');
                        if (bar >= 0) run = run.Substring(0, bar);
                    }
                    if (Publishable(styles, para, chars, noteChars))
                        tokens.Add(new TextTok { Text = run, Type = note != null ? TextTokType.Note : TextTokType.Main });
                }
                if (bs >= s.Length) break;

                Match m = Marker.Match(s, bs);
                string name = m.Groups[2].Value;
                bool isEnd = m.Groups[3].Value == "*";
                pos = bs + m.Length;
                if (name.Length == 0)
                {
                    if (!isEnd) pos = bs + 1;          // a lone backslash: skip it
                    continue;                          // \* closes a milestone
                }
                if (isEnd)
                {
                    if (name == note) { note = null; noteChars.Clear(); }
                    else RemoveLast(note != null ? noteChars : chars, name);
                    continue;
                }
                pos = SkipSeparator(s, pos);

                if (name == "c" || name == "v")
                {
                    string number = ReadWord(s, ref pos);
                    chars.Clear();
                    if (name == "c") { note = null; noteChars.Clear(); }
                    tokens.Add(new TextTok { Text = number, Type = name == "c" ? TextTokType.ChapterNumber : TextTokType.VerseNumber });
                    continue;
                }

                string type = styles.StyleType(name);
                if (name.EndsWith("-s", StringComparison.Ordinal) || name.EndsWith("-e", StringComparison.Ordinal) || type == "milestone")
                {
                    pos = SkipMilestoneAttributes(s, pos);
                    continue;
                }
                if (Cell.IsMatch(name))
                {
                    tokens.Add(new TextTok { Type = TextTokType.Main, CellStart = true });
                    continue;
                }
                // An unknown marker is a paragraph outside notes (UsfmParser.DetermineUnknownTokenType).
                if (type == "paragraph" || name == "id" || (type == null && note == null))
                {
                    para = name;
                    note = null;
                    chars.Clear();
                    noteChars.Clear();
                    if (Publishable(styles, para, chars, noteChars))
                        tokens.Add(new TextTok { Type = TextTokType.Main, ParaStart = true });
                    continue;
                }
                if (type == "note")
                {
                    note = name;
                    noteChars.Clear();
                    ReadWord(s, ref pos);              // the caller (+, -, a letter) is not text
                    pos = SkipSeparator(s, pos);
                    if (Publishable(styles, para, chars, noteChars))
                        tokens.Add(new TextTok { Type = TextTokType.Note, NoteStart = true });
                    continue;
                }
                if (name == "fig")
                {
                    pos = AddFigure(s, pos, note != null, tokens);
                    continue;
                }
                // A character style (or an unknown marker inside a note).
                if (note != null)
                {
                    // In notes, \fr \ft \fq... often have no closing marker: the next one replaces it.
                    if (!HasEndMarkerAhead(s, pos, name)) noteChars.Clear();
                    noteChars.Add(name);
                }
                else if (type == "character" || HasEndMarkerAhead(s, pos, name))
                {
                    chars.Add(name);
                }
            }
            return tokens;
        }

        private static bool Publishable(StyleSheets styles, string para, List<string> chars, List<string> noteChars)
        {
            if (para != null && styles.IsNonpublishable(para)) return false;
            foreach (string c in chars) if (styles.IsNonpublishable(c)) return false;
            foreach (string c in noteChars) if (styles.IsNonpublishable(c)) return false;
            return true;
        }

        private static bool IsEndMarkerAt(string s, int bs)
        {
            Match m = Marker.Match(s, bs);
            return m.Success && m.Index == bs && m.Groups[3].Value == "*" && m.Groups[2].Value.Length > 0;
        }

        /// <summary>Whether \name* comes before the next paragraph marker line (a cheap look ahead).</summary>
        private static bool HasEndMarkerAhead(string s, int pos, string name)
        {
            // Search only up to the next marker line: searching the rest of the book for every
            // \ft or \fr without a closing marker made long books take minutes.
            int nextLine = s.IndexOf("\n\\", pos, StringComparison.Ordinal);
            int limit = nextLine < 0 ? s.Length : nextLine;
            return s.IndexOf("\\" + name + "*", pos, limit - pos, StringComparison.Ordinal) >= 0;
        }

        private static void RemoveLast(List<string> list, string name)
        {
            int i = list.LastIndexOf(name);
            if (i >= 0) list.RemoveRange(i, list.Count - i);
        }

        /// <summary>One space (or line break) after a marker belongs to the marker.</summary>
        private static int SkipSeparator(string s, int pos)
        {
            return pos < s.Length && (s[pos] == ' ' || s[pos] == '\n' || s[pos] == '\t') ? pos + 1 : pos;
        }

        private static string ReadWord(string s, ref int pos)
        {
            int start = pos;
            while (pos < s.Length && !char.IsWhiteSpace(s[pos]) && s[pos] != '\\') pos++;
            string word = s.Substring(start, pos - start);
            pos = SkipSeparator(s, pos);
            return word;
        }

        /// <summary>\qt-s |sid="x"\* : the attributes up to \* are not text.</summary>
        private static int SkipMilestoneAttributes(string s, int pos)
        {
            int bs = s.IndexOf('\\', pos);
            if (bs >= 0 && bs + 1 < s.Length && s[bs + 1] == '*') return bs + 2;
            return pos;
        }

        /// <summary>
        /// \fig ...\fig*: USFM 3 is caption|attributes (ref="..."); USFM 2 is
        /// DESC|FILE|SIZE|LOC|COPY|CAP|REF. The caption and the ref become figure tokens.
        /// </summary>
        private static int AddFigure(string s, int pos, bool inNote, List<TextTok> tokens)
        {
            int end = s.IndexOf("\\fig*", pos, StringComparison.Ordinal);
            if (end < 0) end = s.Length;
            string body = s.Substring(pos, end - pos);
            string caption, reference = "";
            string[] fields = body.Split('|');
            if (fields.Length == 7)
            {
                caption = fields[5];
                reference = fields[6];
            }
            else
            {
                caption = fields[0];
                if (fields.Length > 1)
                {
                    Match r = Regex.Match(body.Substring(body.IndexOf('|')), "\\bref=\"([^\"]*)\"");
                    if (r.Success) reference = r.Groups[1].Value;
                }
            }
            TextTokType type = inNote ? TextTokType.Note : TextTokType.Main;
            if (caption.Length > 0) tokens.Add(new TextTok { Text = caption, Type = type, IsFigure = true });
            if (reference.Length > 0) tokens.Add(new TextTok { Text = reference, Type = type, IsFigure = true });
            return Math.Min(s.Length, end + 5);
        }

        /// <summary>The text of the tokens, with a line break wherever Paratext would start a new token run.</summary>
        public static string Text(List<TextTok> tokens)
        {
            var sb = new StringBuilder();
            foreach (TextTok t in tokens)
            {
                if (t.Type == TextTokType.ChapterNumber || t.Type == TextTokType.VerseNumber || t.ParaStart || t.NoteStart || t.CellStart || t.IsFigure)
                    sb.Append('\n');
                if (t.Type == TextTokType.Main || t.Type == TextTokType.Note) sb.Append(t.Text);
                if (t.IsFigure) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
