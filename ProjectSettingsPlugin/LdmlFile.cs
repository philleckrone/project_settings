using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace ProjectSettings
{
    /// <summary>
    /// The project's language settings: the .ldml file named after Settings.xml's LanguageIsoCode
    /// (es::: -> es.ldml, es:::x-taller -> es-x-taller.ldml). It is turned into rows like the
    /// settings, with names taken from the LDML elements. Never writes.
    /// </summary>
    public static class LdmlFile
    {
        private const string SilNamespace = "urn://www.sil.org/ldml/0.1";

        /// <summary>Name of the first row, which gives the file used.</summary>
        public const string FileRow = "ldml";

        /// <summary>The LDML file name for a LanguageIsoCode: its non-empty parts joined by '-', plus .ldml.</summary>
        public static string FileNameFor(string languageIsoCode)
        {
            if (string.IsNullOrEmpty(languageIsoCode)) return null;
            string tag = string.Join("-", languageIsoCode.Split(':').Where(p => p.Trim().Length > 0).Select(p => p.Trim()).ToArray());
            return tag.Length > 0 ? tag + ".ldml" : null;
        }

        /// <summary>
        /// The project's .ldml files, and the one that matches the language code (null when none
        /// does). The match ignores case, as Windows file names do.
        /// </summary>
        public static string Find(string projectDir, string languageIsoCode, out List<string> allFiles)
        {
            allFiles = new List<string>();
            try
            {
                allFiles = Directory.GetFiles(projectDir, "*.ldml").Select(Path.GetFileName)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception) { }
            string wanted = FileNameFor(languageIsoCode);
            if (wanted == null) return null;
            return allFiles.FirstOrDefault(f => string.Equals(f, wanted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The rows of the language settings section. The first row names the file (empty when no
        /// file matches the code); the others come from the file. Empty LDML values give empty rows.
        /// </summary>
        public static List<SettingItem> Read(string projectDir, string languageIsoCode)
        {
            List<string> all;
            string file = Find(projectDir, languageIsoCode, out all);
            var rows = new List<SettingItem>();
            rows.Add(Row(FileRow, file ?? ""));
            rows.Add(Row("ldml-others", string.Join(" ", all.Where(f => f != file).ToArray())));
            if (file == null) return rows;

            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.XmlResolver = null;
            var readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Ignore;
            readerSettings.XmlResolver = null;
            using (var stream = new FileStream(Path.Combine(projectDir, file), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (XmlReader reader = XmlReader.Create(stream, readerSettings))
                doc.Load(reader);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("sil", SilNamespace);
            XmlElement root = doc.DocumentElement;
            if (root == null) return rows;

            // Identity: language:script:territory:variant, like LanguageIsoCode.
            rows.Add(Row("identity", string.Join(":", new[]
            {
                Attr(root, "identity/language", "type"), Attr(root, "identity/script", "type"),
                Attr(root, "identity/territory", "type"), Attr(root, "identity/variant", "type")
            })));

            // Characters
            rows.Add(Row("exemplarCharacters", Set(Text(root, "characters/exemplarCharacters[not(@type)]", ns))));
            foreach (string type in new[] { "auxiliary", "index", "punctuation" })
                rows.Add(Row("exemplarCharacters[" + type + "]", Set(Text(root, "characters/exemplarCharacters[@type='" + type + "']", ns))));
            foreach (string type in new[] { "wordFormingPunctuation", "footnotes", "crossrefs", "verseSegments", "diacritics", "wordBreaks", "numbers" })
                rows.Add(Row("sil:exemplarCharacters[" + type + "]", Set(Text(root, "characters/special/sil:exemplarCharacters[@type='" + type + "']", ns))));

            // Alphabetic characters (the sort order), from the default collation.
            string defaultCollation = Text(root, "collations/defaultCollation", ns);
            string collationType = string.IsNullOrEmpty(defaultCollation) ? "standard" : defaultCollation.Trim();
            var collation = root.SelectSingleNode("collations/collation[@type='" + collationType + "']", ns) as XmlElement;
            string simple = collation != null ? Text(collation, "special/sil:simple", ns) : null;
            string rules = collation != null ? Text(collation, "cr", ns) : null;
            // The ICU rules are shown only when there is no simple list: Paratext writes the rules
            // from the list, so with a list they say the same thing less readably.
            rows.Add(Row("sil:simple", Lines(simple)));
            if (string.IsNullOrEmpty(Lines(simple)))
                rows.Add(Row("collation[" + collationType + "]", (rules ?? "").Trim()));

            // Matched pairs and punctuation patterns
            var pairs = root.SelectNodes("delimiters/special/sil:matched-pairs/sil:matched-pair", ns).Cast<XmlElement>().ToList();
            rows.Add(Row("sil:matched-pairs", string.Join(" ", pairs.Select(p => p.GetAttribute("open") + "/" + p.GetAttribute("close")).ToArray())));
            rows.Add(Row("sil:matched-pair[paraClose]", string.Join(" ", pairs
                .Where(p => string.Equals(p.GetAttribute("paraClose"), "true", StringComparison.OrdinalIgnoreCase))
                .Select(p => p.GetAttribute("open") + "/" + p.GetAttribute("close")).ToArray())));
            rows.Add(Row("sil:punctuation-patterns", string.Join(" ", root
                .SelectNodes("delimiters/special/sil:punctuation-patterns/sil:punctuation-pattern", ns).Cast<XmlElement>()
                .Select(p => p.GetAttribute("pattern")).Where(p => p.Length > 0).ToArray())));

            // Quotation marks
            rows.Add(Row("quotationStart", Text(root, "delimiters/quotationStart", ns)));
            rows.Add(Row("quotationEnd", Text(root, "delimiters/quotationEnd", ns)));
            rows.Add(Row("alternateQuotationStart", Text(root, "delimiters/alternateQuotationStart", ns)));
            rows.Add(Row("alternateQuotationEnd", Text(root, "delimiters/alternateQuotationEnd", ns)));
            var marks = root.SelectSingleNode("delimiters/special/sil:quotation-marks", ns) as XmlElement;
            rows.Add(Row("sil:quotation-marks[paraContinueType]", marks != null ? marks.GetAttribute("paraContinueType") : ""));
            rows.Add(Row("sil:quotationContinue", Text(root, "delimiters/special/sil:quotation-marks/sil:quotationContinue", ns)));
            rows.Add(Row("sil:alternateQuotationContinue", Text(root, "delimiters/special/sil:quotation-marks/sil:alternateQuotationContinue", ns)));
            // One line per level: level|open|close|continue|type (formatted by the "quotes" decoder).
            rows.Add(Row("sil:quotation", string.Join("\n", root
                .SelectNodes("delimiters/special/sil:quotation-marks/sil:quotation", ns).Cast<XmlElement>()
                .Select(q => string.Join("|", new[] { q.GetAttribute("level"), q.GetAttribute("open"), q.GetAttribute("close"), q.GetAttribute("continue"), q.GetAttribute("type") }))
                .ToArray())));

            // Layout, numbers, fonts, keyboards
            rows.Add(Row("characterOrder", Text(root, "layout/orientation/characterOrder", ns)));
            rows.Add(Row("numberingSystem[digits]", Attr(root, "numbers/numberingSystem", "digits")));
            // One line per font: name|types|size|features (formatted by the "fonts" decoder).
            rows.Add(Row("sil:font", string.Join("\n", root
                .SelectNodes("special/sil:external-resources/sil:font", ns).Cast<XmlElement>()
                .Select(f => string.Join("|", new[] { f.GetAttribute("name"), f.GetAttribute("types"), f.GetAttribute("size"), f.GetAttribute("features") }))
                .ToArray())));
            rows.Add(Row("sil:kbd", string.Join("\n", root
                .SelectNodes("special/sil:external-resources/sil:kbd", ns).Cast<XmlElement>()
                .Select(k => k.GetAttribute("id") + (k.GetAttribute("type").Length > 0 ? " (" + k.GetAttribute("type") + ")" : ""))
                .ToArray())));
            return rows;
        }

        /// <summary>
        /// The alphabetic items of ValidCharacters (Settings.xml) that are not in the Alphabetic
        /// characters list (sil:simple: lines of forms such as "a/A \u00E1/\u00C1"). An item is alphabetic when
        /// it holds a letter (so \u02BC U+02BC and k' count; digits and punctuation do not). Both sides are
        /// compared in NFC. A letter combination (more than one text element) also counts as listed
        /// when its lower case form is listed, so Ch and CH pass when the list has ch; single
        /// letters must match exactly.
        /// </summary>
        public static List<string> LettersNotInList(string validCharacters, string simple)
        {
            var exact = new HashSet<string>(StringComparer.Ordinal);
            var lower = new HashSet<string>(StringComparer.Ordinal);
            foreach (string token in (simple ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                foreach (string form in token.Split('/'))
                {
                    if (form.Length == 0) continue;
                    string nfc = form.Normalize(NormalizationForm.FormC);
                    exact.Add(nfc);
                    lower.Add(nfc.ToLowerInvariant());
                }

            var missing = new List<string>();
            foreach (string token in (validCharacters ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string item = token.Normalize(NormalizationForm.FormC);
                if (!HasLetter(item) || exact.Contains(item) || missing.Contains(item)) continue;
                bool combination = new System.Globalization.StringInfo(item).LengthInTextElements > 1;
                if (combination && lower.Contains(item.ToLowerInvariant())) continue;
                missing.Add(item);
            }
            return missing;
        }

        private static bool HasLetter(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsLetter(s, i)) return true;
                if (char.IsHighSurrogate(s[i])) i++;
            }
            return false;
        }

        private static SettingItem Row(string name, string value)
        {
            var item = new SettingItem();
            item.Name = name;
            item.Value = value ?? "";
            return item;
        }

        private static string Text(XmlNode node, string xpath, XmlNamespaceManager ns)
        {
            XmlNode found = node.SelectSingleNode(xpath, ns);
            return found != null ? found.InnerText : "";
        }

        private static string Attr(XmlElement root, string xpath, string attribute)
        {
            var found = root.SelectSingleNode(xpath) as XmlElement;
            return found != null ? found.GetAttribute(attribute) : "";
        }

        /// <summary>An empty set "[]" is no value.</summary>
        private static string Set(string set)
        {
            string s = (set ?? "").Trim();
            return s == "[]" ? "" : s;
        }

        /// <summary>Lines of the Alphabetic characters list, without blank ones.</summary>
        private static string Lines(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray());
        }

        /// <summary>
        /// The items of an ICU UnicodeSet such as [a-c {ch} \u00F1 \-]: single characters, ranges
        /// (expanded, at most 400 characters each) and {strings}. Throws FormatException when the
        /// set uses syntax this reader does not know (properties, nested sets).
        /// </summary>
        public static List<string> ParseSet(string set)
        {
            string s = (set ?? "").Trim();
            if (s.Length < 2 || s[0] != '[' || s[s.Length - 1] != ']') throw new FormatException("not a set");
            s = s.Substring(1, s.Length - 2);
            var items = new List<string>();
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }
                if (c == '{')
                {
                    var sb = new StringBuilder();
                    i++;
                    while (i < s.Length && s[i] != '}')
                    {
                        if (s[i] == '\\') sb.Append(Escape(s, ref i));
                        else sb.Append(s[i++]);
                    }
                    if (i >= s.Length) throw new FormatException("unclosed {");
                    i++;
                    items.Add(sb.ToString());
                    continue;
                }
                if (c == '[' || c == '^' || c == '$' || c == '&') throw new FormatException("unsupported set syntax");
                string first = c == '\\' ? Escape(s, ref i) : s[i++].ToString();
                // A range: first-last (a '-' at the end of the set is a literal hyphen).
                if (i + 1 < s.Length && s[i] == '-' && s[i + 1] != ']')
                {
                    i++;
                    string last = s[i] == '\\' ? Escape(s, ref i) : s[i++].ToString();
                    int from = char.ConvertToUtf32(first, 0), to = char.ConvertToUtf32(last, 0);
                    if (to < from || to - from > 400) throw new FormatException("range too large");
                    for (int cp = from; cp <= to; cp++) items.Add(char.ConvertFromUtf32(cp));
                    continue;
                }
                items.Add(first);
            }
            return items;
        }

        /// <summary>A backslash escape at s[i]: \uXXXX, \UXXXXXXXX, \x{...} or a quoted character.</summary>
        private static string Escape(string s, ref int i)
        {
            i++;   // the backslash
            if (i >= s.Length) throw new FormatException("trailing backslash");
            char c = s[i];
            if (c == 'u' && i + 4 < s.Length)
            {
                string hex = s.Substring(i + 1, 4);
                i += 5;
                return char.ConvertFromUtf32(Convert.ToInt32(hex, 16));
            }
            if (c == 'U' && i + 8 < s.Length)
            {
                string hex = s.Substring(i + 1, 8);
                i += 9;
                return char.ConvertFromUtf32(Convert.ToInt32(hex, 16));
            }
            if (c == 'x' && i + 1 < s.Length && s[i + 1] == '{')
            {
                int close = s.IndexOf('}', i);
                if (close < 0) throw new FormatException("bad \\x");
                string hex = s.Substring(i + 2, close - i - 2);
                i = close + 1;
                return char.ConvertFromUtf32(Convert.ToInt32(hex, 16));
            }
            i++;
            return c.ToString();
        }
    }
}
