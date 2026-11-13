using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectSettings
{
    /// <summary>
    /// Turns a setting's stored value into the HTML of the report's Value column: a readable form
    /// where one helps (decoder from settings.txt), and the stored text under it when they differ.
    /// Everything taken from the file is HTML-encoded.
    /// </summary>
    public sealed class ValueFormatter
    {
        /// <summary>Paratext's canonical book order: the position in BooksPresent and similar strings.</summary>
        public static readonly string[] BookCodes =
        {
            "GEN","EXO","LEV","NUM","DEU","JOS","JDG","RUT","1SA","2SA","1KI","2KI","1CH","2CH","EZR","NEH",
            "EST","JOB","PSA","PRO","ECC","SNG","ISA","JER","LAM","EZK","DAN","HOS","JOL","AMO","OBA","JON",
            "MIC","NAM","HAB","ZEP","HAG","ZEC","MAL",
            "MAT","MRK","LUK","JHN","ACT","ROM","1CO","2CO","GAL","EPH","PHP","COL","1TH","2TH","1TI","2TI",
            "TIT","PHM","HEB","JAS","1PE","2PE","1JN","2JN","3JN","JUD","REV",
            "TOB","JDT","ESG","WIS","SIR","BAR","LJE","S3Y","SUS","BEL","1MA","2MA","3MA","4MA","1ES","2ES",
            "MAN","PS2","ODA","PSS","JSA","JDB","TBS","SST","DNT","BLT",
            "XXA","XXB","XXC","XXD","XXE","XXF","XXG",
            "FRT","BAK","OTH","3ES","EZA","5EZ","6EZ","INT","CNC","GLO","TDX","NDX","DAG","PS3","2BA","LBA",
            "JUB","ENO","1MQ","2MQ","3MQ","REP","4BA","LAO"
        };

        private readonly Strings strings;
        private readonly Func<string, bool> fontInstalled;

        /// <param name="fontInstalled">Tells whether a font is installed (null: don't say).</param>
        public ValueFormatter(Strings strings, Func<string, bool> fontInstalled)
        {
            this.strings = strings;
            this.fontInstalled = fontInstalled;
        }

        public string Format(Described d)
        {
            SettingItem item = d.Item;
            string value = item.Value ?? "";
            var html = new StringBuilder();

            if (IsEmptyValue(value) && d.Decoder != "naming")
            {
                html.Append("<span class=\"empty\">").Append(Enc(strings.Empty)).Append("</span>");
                if (value.Length > 0) AppendStored(html, value);
            }
            else
            {
                switch (d.Decoder)
                {
                    case "bool": FormatBool(html, value); break;
                    case "books": FormatBooks(html, value, item.Name == "BooksPresent"); break;
                    case "vrs": FormatVersification(html, value); break;
                    case "encoding": FormatEncoding(html, value); break;
                    case "langcode": FormatLanguageCode(html, value); break;
                    case "projtype": FormatProjectType(html, value); break;
                    case "html": FormatHtml(html, value); break;
                    case "font": FormatFont(html, value); break;
                    case "list": FormatList(html, value, null); break;
                    case "chars": FormatList(html, value, null, d.Counts); break;
                    case "punct": FormatList(html, value, "punct", d.Counts); break;
                    case "pairs": FormatList(html, value, "pairs"); break;
                    case "bar": FormatBar(html, value); break;
                    case "naming": FormatNaming(html, item); break;
                    // Language settings (.ldml) rows
                    case "uset": FormatSet(html, value); break;
                    case "lines": FormatLines(html, value); break;
                    case "quotes": FormatQuoteLevels(html, value); break;
                    case "fonts": FormatFonts(html, value); break;
                    case "code": html.Append("<pre class=\"rules\">").Append(Enc(value)).Append("</pre>"); break;
                    default:
                        html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
                        // A short value such as a continuer or a separator: say which character it is.
                        string codes = value.Trim().Length <= 3 ? LookAlikes.Codes(value) : "";
                        if (codes.Length > 0)
                            html.Append(" <span class=\"cp inline\">").Append(Enc(codes)).Append("</span>");
                        break;
                }
            }

            if (item.Attributes.Count > 0 && d.Decoder != "naming")
                AppendAttributes(html, item);
            return html.ToString();
        }

        /// <summary>Empty, white space only between elements, or Paratext's "NullValue".</summary>
        private static bool IsEmptyValue(string value)
        {
            return value.Length == 0 || value == "NullValue";
        }

        // ------------------------------------------------------------ decoders

        private void FormatBool(StringBuilder html, string value)
        {
            string v = value.Trim();
            string shown = null;
            if (v == "T" || v.Equals("True", StringComparison.OrdinalIgnoreCase) || v.Equals("Yes", StringComparison.OrdinalIgnoreCase)) shown = strings.Yes;
            else if (v == "F" || v.Equals("False", StringComparison.OrdinalIgnoreCase) || v.Equals("No", StringComparison.OrdinalIgnoreCase)) shown = strings.No;
            if (shown == null)
            {
                html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
                return;
            }
            html.Append("<b>").Append(Enc(shown)).Append("</b>");
            if (shown != value) AppendStored(html, value);
        }

        /// <summary>GEN..REV are the first 66 positions of a book string.</summary>
        private const int BibleBookCount = 66;

        /// <param name="listMissing">Also list the books from GEN to REV that are not there (BooksPresent).</param>
        private void FormatBooks(StringBuilder html, string value, bool listMissing)
        {
            string bits = value.Trim();
            if (bits.Any(c => c != '0' && c != '1'))
            {
                html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
                return;
            }
            var codes = new List<string>();
            for (int i = 0; i < bits.Length && i < BookCodes.Length; i++)
                if (bits[i] == '1') codes.Add(BookCodes[i]);
            if (codes.Count == 0)
            {
                html.Append(Enc(strings.BooksNone));
            }
            else
            {
                html.Append("<div>").Append(Enc(string.Format(strings.BooksCount, codes.Count))).Append("</div><div class=\"chips\">");
                foreach (string code in codes)
                    html.Append("<span class=\"chip book\">").Append(code).Append("</span>");
                html.Append("</div>");
            }
            if (listMissing)
            {
                // A string shorter than 66 digits leaves the rest of the Bible missing.
                var missing = new List<string>();
                for (int i = 0; i < BibleBookCount; i++)
                    if (i >= bits.Length || bits[i] != '1') missing.Add(BookCodes[i]);
                if (missing.Count == 0)
                {
                    html.Append("<div class=\"missing\">").Append(Enc(strings.BooksNoneMissing)).Append("</div>");
                }
                else
                {
                    html.Append("<div class=\"missing\">").Append(Enc(string.Format(strings.BooksMissing, missing.Count)))
                        .Append("</div><div class=\"chips\">");
                    foreach (string code in missing)
                        html.Append("<span class=\"chip book absent\">").Append(code).Append("</span>");
                    html.Append("</div>");
                }
            }
            AppendStored(html, value);
        }

        private void FormatVersification(StringBuilder html, string value)
        {
            int n;
            string[] names = strings.VersificationNames;
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n >= 0 && n < names.Length)
            {
                html.Append("<b>").Append(Enc(names[n])).Append("</b>");
                AppendStored(html, value);
            }
            else
            {
                html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
            }
        }

        private void FormatEncoding(StringBuilder html, string value)
        {
            int codePage;
            string name = null;
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out codePage))
            {
                if (codePage == 65001) name = "UTF-8 (Unicode)";
                else
                {
                    try
                    {
                        Encoding e = Encoding.GetEncoding(codePage);
                        name = e.EncodingName + " (" + e.WebName + ")";
                    }
                    catch (Exception) { }
                }
            }
            if (name == null)
            {
                html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
                return;
            }
            html.Append("<b>").Append(Enc(name)).Append("</b>");
            AppendStored(html, value);
        }

        private void FormatLanguageCode(StringBuilder html, string value)
        {
            string[] parts = value.Trim().Split(':');
            string[] labels = { strings.LangLanguage, strings.LangScript, strings.LangRegion, strings.LangVariant };
            html.Append("<table class=\"parts\">");
            for (int i = 0; i < labels.Length; i++)
            {
                string part = i < parts.Length ? parts[i] : "";
                if (part.Length == 0) continue;   // only the parts that are given
                html.Append("<tr><td>").Append(Enc(labels[i])).Append("</td><td><b>").Append(Enc(part)).Append("</b></td></tr>");
            }
            html.Append("</table>");
            AppendStored(html, value);
        }

        /// <summary>TranslationInfo: type:base name:base ID.</summary>
        private void FormatProjectType(StringBuilder html, string value)
        {
            string[] parts = value.Split(':');
            html.Append("<table class=\"parts\"><tr><td>").Append(Enc(strings.ProjType)).Append("</td><td><b>")
                .Append(Enc(parts[0])).Append("</b></td></tr>");
            string baseName = parts.Length > 1 ? parts[1] : "";
            string baseId = parts.Length > 2 ? string.Join(":", parts.Skip(2).ToArray()) : "";
            if (baseName.Length > 0 || baseId.Length > 0)
            {
                html.Append("<tr><td>").Append(Enc(strings.ProjBasedOn)).Append("</td><td><b>").Append(Enc(baseName)).Append("</b>");
                if (baseId.Length > 0) html.Append(" <span class=\"small\">").Append(Enc(baseId)).Append("</span>");
                html.Append("</td></tr>");
            }
            html.Append("</table>");
            AppendStored(html, value);
        }

        /// <summary>The copyright is HTML: show its text (never its markup), then the stored HTML.</summary>
        private void FormatHtml(StringBuilder html, string value)
        {
            string text = Regex.Replace(value, @"<\s*(br|/p|/div|/li)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, "<[^>]*>", "");
            text = WebUtility.HtmlDecode(text).Trim();
            html.Append("<span class=\"v\">").Append(Enc(text).Replace("\n", "<br>")).Append("</span>");
            if (text != value) AppendStored(html, value);
        }

        private void FormatFont(StringBuilder html, string value)
        {
            html.Append("<span class=\"v\" style=\"font-family:'").Append(CssString(value.Trim())).Append("'\">")
                .Append(Enc(value)).Append("</span>");
            if (fontInstalled != null && !fontInstalled(value.Trim()))
                html.Append(" <span class=\"warn\">").Append(Enc(strings.FontMissing)).Append("</span>");
        }

        /// <summary>
        /// A space-separated list, one chip per item. Only ASCII white space separates items. With
        /// counts (characters used in the text), each chip shows how often its item occurs.
        /// </summary>
        private void FormatList(StringBuilder html, string value, string kind, Dictionary<string, int> counts = null)
        {
            string[] items = value.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (items.Length == 0)
            {
                html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
                return;
            }
            if (kind == "punct")
                html.Append("<div class=\"note\">").Append(Enc(strings.PunctNote)).Append("</div>");
            html.Append("<div class=\"chips v\">");
            foreach (string item in items)
            {
                html.Append("<span class=\"chip\"");
                if (kind == "punct") html.Append(" title=\"").Append(Enc(PunctuationPlace(item))).Append("\"");
                html.Append(">");
                if (kind == "pairs" && item.Length >= 3 && item.IndexOf('/') > 0)
                {
                    int slash = item.IndexOf('/');
                    html.Append(TokenHtml(item.Substring(0, slash))).Append("<span class=\"dim\"> \u2026 </span>")
                        .Append(TokenHtml(item.Substring(slash + 1)));
                }
                else
                {
                    html.Append(TokenHtml(item));
                }
                int count;
                if (counts != null && counts.TryGetValue(item, out count) && count > 0)
                    html.Append("<sup class=\"n\">").Append(count.ToString("#,0", CultureInfo.InvariantCulture)).Append("</sup>");
                // In punctuation items _ is Paratext's notation for a space or a word edge, not a
                // character of the text, so it gets no Unicode value (unless the item is only _).
                string codesOf = kind == "punct" && item.Trim('_').Length > 0 ? item.Replace("_", "") : item;
                html.Append(CodesHtml(codesOf)).Append("</span>");
            }
            html.Append("</div>");
        }

        /// <summary>The Unicode values of the look-alike characters in an item, shown under it; "" for none.</summary>
        internal static string CodesHtml(string item)
        {
            string codes = LookAlikes.Codes(item);
            return codes.Length > 0 ? "<span class=\"cp\">" + Enc(codes) + "</span>" : "";
        }

        private string PunctuationPlace(string item)
        {
            bool before = item.StartsWith("_", StringComparison.Ordinal) && item.Length > 1;
            bool after = item.EndsWith("_", StringComparison.Ordinal) && item.Length > 1;
            if (before && after) return strings.PunctIsolated;
            if (before) return strings.PunctInitial;
            if (after) return strings.PunctFinal;
            return strings.PunctMedial;
        }

        private void FormatBar(StringBuilder html, string value)
        {
            string[] items = value.Split('|');
            html.Append("<ul class=\"bar v\">");
            foreach (string item in items)
                html.Append("<li>").Append(TextHtml(item)).Append("</li>");
            html.Append("</ul>");
        }

        /// <summary>Naming: PrePart, PostPart and BookNameForm attributes, shown with Matthew's file name.</summary>
        private void FormatNaming(StringBuilder html, SettingItem item)
        {
            string pre = item.Attribute("PrePart") ?? "";
            string post = item.Attribute("PostPart") ?? "";
            string form = item.Attribute("BookNameForm") ?? "";
            if (form.Length > 0 || post.Length > 0)
                html.Append("<div><b>").Append(Enc(string.Format(strings.MatthewFile, pre + form + post))).Append("</b></div>");
            if (!IsEmptyValue(item.Value ?? ""))
                html.Append("<div class=\"v\">").Append(TextHtml(item.Value)).Append("</div>");
            AppendAttributes(html, item);
        }

        // ------------------------------------------------------------ language settings (.ldml)

        /// <summary>
        /// An ICU set such as [a-z {ch}]: one chip per character or string. The stored set is not
        /// shown: its \u escapes say nothing more than the chips.
        /// </summary>
        private void FormatSet(StringBuilder html, string value)
        {
            List<string> items;
            try
            {
                items = LdmlFile.ParseSet(value);
            }
            catch (Exception)
            {
                html.Append("<span class=\"v\">").Append(TextHtml(value)).Append("</span>");
                return;
            }
            html.Append("<div class=\"chips v\">");
            foreach (string item in items)
            {
                html.Append("<span class=\"chip\">");
                if (item.Trim().Length == 0) html.Append(Badge(strings.Space));
                else html.Append(TokenHtml(item)).Append(CodesHtml(item));
                html.Append("</span>");
            }
            html.Append("</div>");
        }

        /// <summary>One line per entry; space-separated items on a line become chips (a/A \u00E1/\u00C1).</summary>
        private void FormatLines(StringBuilder html, string value)
        {
            html.Append("<ol class=\"lines v\">");
            foreach (string line in value.Split('\n'))
            {
                html.Append("<li>");
                foreach (string part in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    html.Append("<span class=\"chip\">").Append(TokenHtml(part)).Append(CodesHtml(part)).Append("</span> ");
                html.Append("</li>");
            }
            html.Append("</ol>");
        }

        /// <summary>sil:quotation lines: level|open|close|continue|type.</summary>
        private void FormatQuoteLevels(StringBuilder html, string value)
        {
            html.Append("<ul class=\"bar v\">");
            foreach (string line in value.Split('\n'))
            {
                string[] f = line.Split('|');
                if (f.Length < 5) continue;
                var parts = new List<string>();
                if (f[1].Length > 0 || f[2].Length > 0)
                    parts.Add(string.Format(strings.QuoteOpenClose, TextHtml(f[1]), TextHtml(f[2])));
                if (f[3].Length > 0) parts.Add(Enc(string.Format(strings.QuoteContinue, "\u0001")).Replace("\u0001", TextHtml(f[3])));
                html.Append("<li><b>").Append(Enc(string.Format(strings.QuoteLevel, f[0]))).Append("</b>");
                if (f[4].Length > 0)
                    html.Append(" (").Append(Enc(f[4] == "narrative" ? strings.QuoteNarrative : f[4])).Append(")");
                html.Append(": ").Append(string.Join("; ", parts.ToArray())).Append("</li>");
            }
            html.Append("</ul>");
        }

        /// <summary>sil:font lines: name|types|size|features. The size is relative to normal text.</summary>
        private void FormatFonts(StringBuilder html, string value)
        {
            html.Append("<ul class=\"bar\">");
            foreach (string line in value.Split('\n'))
            {
                string[] f = line.Split('|');
                if (f.Length < 4 || f[0].Length == 0) continue;
                html.Append("<li><span class=\"v\" style=\"font-family:'").Append(CssString(f[0])).Append("'\">").Append(Enc(f[0])).Append("</span>");
                var extra = new List<string>();
                if (f[1].Length > 0) extra.Add(f[1]);
                if (f[2].Length > 0) extra.Add(string.Format(strings.FontSize, f[2]));
                if (f[3].Length > 0) extra.Add(string.Format(strings.FontFeatureList, f[3]));
                if (extra.Count > 0) html.Append(" <span class=\"dim\">(").Append(Enc(string.Join(", ", extra.ToArray()))).Append(")</span>");
                if (fontInstalled != null && !fontInstalled(f[0]))
                    html.Append(" <span class=\"warn\">").Append(Enc(strings.FontMissing)).Append("</span>");
                html.Append("</li>");
            }
            html.Append("</ul>");
        }

        // ------------------------------------------------------------ pieces

        private void AppendStored(StringBuilder html, string value)
        {
            html.Append("<div class=\"stored\">").Append(Enc(strings.StoredAs)).Append(" <code>")
                .Append(TextHtml(value)).Append("</code></div>");
        }

        private void AppendAttributes(StringBuilder html, SettingItem item)
        {
            html.Append("<div class=\"stored\">").Append(Enc(strings.Attributes));
            foreach (KeyValuePair<string, string> a in item.Attributes)
                html.Append(" <code>").Append(Enc(a.Key)).Append("=\"").Append(TextHtml(a.Value)).Append("\"</code>");
            html.Append("</div>");
        }

        /// <summary>
        /// A plain value: invisible characters and special spaces become badges, as do spaces at
        /// the start or end and a value made only of spaces.
        /// </summary>
        internal string TextHtml(string value)
        {
            if (value.Length == 0) return "";
            int start = 0, end = value.Length;
            while (start < end && value[start] == ' ') start++;
            while (end > start && value[end - 1] == ' ') end--;
            var sb = new StringBuilder();
            for (int i = 0; i < start; i++) sb.Append(Badge(strings.Space));
            sb.Append(TokenHtml(value.Substring(start, end - start)));
            for (int i = end; i < value.Length; i++) sb.Append(Badge(strings.Space));
            return sb.ToString().Replace("\r\n", "\n").Replace("\n", "<br>");
        }

        /// <summary>One item: special characters as badges; a leading combining mark gets a dotted circle.</summary>
        internal string TokenHtml(string token)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < token.Length; i++)
            {
                char c = token[i];
                if (char.IsHighSurrogate(c) && i + 1 < token.Length && char.IsLowSurrogate(token[i + 1]))
                {
                    sb.Append(token, i, 2);
                    i++;
                    continue;
                }
                string name = SpecialName(c);
                if (name != null)
                {
                    sb.Append(Badge(name + " U+" + ((int)c).ToString("X4")));
                    continue;
                }
                UnicodeCategory cat = char.GetUnicodeCategory(c);
                bool combining = cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark;
                if (combining && (i == 0 || SpecialName(token[i - 1]) != null))
                    sb.Append('\u25CC');
                if (c == '\n' || c == '\r') sb.Append(c);
                else sb.Append(Enc(c.ToString()));
            }
            return sb.ToString();
        }

        /// <summary>A short name for characters that cannot be seen, or null for ordinary ones.</summary>
        private static string SpecialName(char c)
        {
            switch (c)
            {
                case '\u00A0': return "NBSP";
                case '\u00AD': return "SHY";
                case '\u2002': return "EN SP";
                case '\u2003': return "EM SP";
                case '\u2009': return "THIN SP";
                case '\u200A': return "HAIR SP";
                case '\u200B': return "ZWSP";
                case '\u200C': return "ZWNJ";
                case '\u200D': return "ZWJ";
                case '\u200E': return "LRM";
                case '\u200F': return "RLM";
                case '\u202F': return "NNBSP";
                case '\u2060': return "WJ";
                case '\uFEFF': return "ZWNBSP";
                case '\t': return "TAB";
            }
            if (c == '\n' || c == '\r' || c == ' ') return null;
            UnicodeCategory cat = char.GetUnicodeCategory(c);
            if (cat == UnicodeCategory.SpaceSeparator) return "SPACE";
            if (cat == UnicodeCategory.Format || cat == UnicodeCategory.Control) return "CTRL";
            return null;
        }

        private static string Badge(string text)
        {
            return "<span class=\"badge\">" + Enc(text) + "</span>";
        }

        internal static string Enc(string s)
        {
            return WebUtility.HtmlEncode(s ?? "");
        }

        /// <summary>A font name inside a single-quoted CSS string in a double-quoted attribute.</summary>
        internal static string CssString(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_') sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
