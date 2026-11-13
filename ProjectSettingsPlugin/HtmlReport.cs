using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ProjectSettings
{
    /// <summary>
    /// The report: a self-contained HTML page (inline CSS, no scripts) listing every element of
    /// Settings.xml, grouped by topic, with its description and value.
    /// </summary>
    public static class HtmlReport
    {
        /// <param name="settingsPath">The Settings.xml that was read (shown in the header).</param>
        /// <param name="languageItems">Rows of the language settings (LdmlFile.Read), or null for none.</param>
        /// <param name="usage">Characters counted in the books' text, or null (no book read): then the
        /// character rows show the lists as stored.</param>
        /// <param name="punctuation">Punctuation-inventory items counted in the same text, or null.</param>
        public static string Build(List<SettingItem> items, string settingsPath, List<SettingItem> languageItems,
            CharacterUsage usage, PunctuationUsage punctuation, SettingCatalog catalog, Strings strings, Func<string, bool> fontInstalled, DateTime now)
        {
            var formatter = new ValueFormatter(strings, fontInstalled);
            if (usage != null && usage.BookCount == 0) usage = null;
            if (punctuation != null && punctuation.BookCount == 0) punctuation = null;
            // Settings with no value (empty, or Paratext's NullValue) get rows too, hidden until
            // the "Show empty settings" box is ticked (pure CSS, so it works without scripts).
            // Groups the user found of no use to most readers are left out altogether (SettingCatalog.IsShown).
            // The .cms files are still read, so that custom-check options are recognized.
            List<Described> described = items.Select(i => catalog.Describe(i, strings))
                .Where(d => SettingCatalog.IsShown(d.Group)).ToList();

            // The language settings (.ldml) rows form their own section, whatever the catalog says.
            string ldmlFile = null, ldmlExpected = null;
            if (languageItems != null)
            {
                foreach (SettingItem item in languageItems)
                {
                    Described d = catalog.Describe(item, strings);
                    d.Group = SettingCatalog.Language;
                    d.IdPrefix = "l";
                    described.Add(d);
                    if (item.Name == LdmlFile.FileRow) ldmlFile = item.Value;
                }
                ldmlExpected = LdmlFile.FileNameFor(SettingsFile.ValueOf(items, "LanguageIsoCode"));
            }
            bool ldmlMissing = languageItems != null && string.IsNullOrEmpty(ldmlFile);
            int emptyCount = described.Count(d => !d.Item.HasValue);
            int settingCount = described.Count;

            // Worked-out rows below ValidCharacters and InvalidCharacters. They are not settings, so
            // they are not counted; each takes its anchor's Order and is added after it, so the stable
            // sort puts them right below it, in the order added.
            Described validRow = described.FirstOrDefault(d => d.IdPrefix == "s" && d.Item.Name == "ValidCharacters");
            Described invalidRow = described.FirstOrDefault(d => d.IdPrefix == "s" && d.Item.Name == "InvalidCharacters");
            string originalValid = validRow != null ? validRow.Item.Value : null;
            if (usage != null && validRow != null && validRow.Item.HasValue)
                described.Add(SplitByUsage(validRow, CharacterRows(usage, strings), strings));
            string simple = languageItems != null ? SettingsFile.ValueOf(languageItems, "sil:simple") : null;
            if (validRow != null && !string.IsNullOrEmpty(originalValid) && originalValid != "NullValue" && !string.IsNullOrEmpty(simple))
                described.Add(NotInLanguageRow(validRow, originalValid, simple, ldmlFile, usage, strings));
            if (usage != null && invalidRow != null && invalidRow.Item.HasValue)
                described.Add(InvalidFoundRow(invalidRow, CharacterRows(usage, strings)));

            // The same for the punctuation inventory (2.1).
            Described validPunct = described.FirstOrDefault(d => d.IdPrefix == "s" && d.Item.Name == "ValidPunctuation");
            Described invalidPunct = described.FirstOrDefault(d => d.IdPrefix == "s" && d.Item.Name == "InvalidPunctuation");
            if (punctuation != null && validPunct != null && validPunct.Item.HasValue)
                described.Add(SplitByUsage(validPunct, PunctuationRows(punctuation, strings), strings));
            if (punctuation != null && invalidPunct != null && invalidPunct.Item.HasValue)
                described.Add(InvalidFoundRow(invalidPunct, PunctuationRows(punctuation, strings)));

            // Whether the project's own files exist (2.3): custom.sty under StyleSheet, custom.vrs
            // under Versification, autocorrect.txt at the end of Files and encoding.
            string projectDir = Path.GetDirectoryName(settingsPath) ?? "";
            described.Add(ProjectFileRow(projectDir, "custom.sty", Anchor(described, catalog, strings, "StyleSheet"), strings.CustomStyDesc, strings));
            described.Add(ProjectFileRow(projectDir, "custom.vrs", Anchor(described, catalog, strings, "Versification"), strings.CustomVrsDesc, strings));
            var filesEnd = new Described();
            filesEnd.Group = SettingCatalog.Files;
            filesEnd.Order = int.MaxValue - 1;
            described.Add(ProjectFileRow(projectDir, "autocorrect.txt", filesEnd, strings.AutoCorrectDesc, strings));

            string name = SettingsFile.ValueOf(items, "Name") ?? Path.GetFileName(Path.GetDirectoryName(settingsPath) ?? "");
            string fullName = SettingsFile.ValueOf(items, "FullName");
            string font = SettingsFile.ValueOf(items, "DefaultFont");
            bool rtl = (SettingsFile.ValueOf(items, "LeftToRight") ?? "") == "F";

            var html = new StringBuilder();
            html.Append("<!DOCTYPE html>\n<html lang=\"").Append(LangCode(strings.Language)).Append("\">\n<head>\n<meta charset=\"utf-8\">\n");
            html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
            html.Append("<title>").Append(Enc(string.Format(strings.ReportTitle, name))).Append("</title>\n");
            html.Append("<style>\n").Append(Css(font)).Append("</style>\n</head>\n<body>\n<div class=\"page\">\n");

            // Header
            html.Append("<header>\n<h1>").Append(Enc(string.Format(strings.ReportTitle, name))).Append("</h1>\n");
            if (!string.IsNullOrEmpty(fullName)) html.Append("<p class=\"full\">").Append(Enc(fullName)).Append("</p>\n");
            html.Append("<p class=\"meta\">").Append(Enc(strings.FileLabel)).Append(" <code>").Append(Enc(settingsPath)).Append("</code><br>")
                .Append(Enc(string.Format(strings.MadeBy, strings.Title, Strings.ToolVersion, now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))))
                .Append(" ").Append(Enc(string.Format(strings.CountSettings, settingCount - emptyCount, emptyCount)));
            if (usage != null) html.Append("<br>").Append(Enc(string.Format(strings.BooksRead, usage.BookCount)));
            html.Append("</p>\n");
            html.Append("<p>").Append(Enc(strings.Intro)).Append("</p>\n");
            html.Append("<p class=\"readonly\">").Append(Enc(strings.ReadOnlyNote)).Append("</p>\n");
            if (described.Any(d => SettingCatalog.IsDisapprovedList(d.Item.Name)))
                html.Append("<p class=\"meta\"><span class=\"swatch\"></span> ").Append(Enc(strings.InvalidLegend)).Append("</p>\n");
            if (emptyCount > 0)
                html.Append("<p class=\"meta\"><span class=\"swatch emptyswatch\"></span> ").Append(Enc(strings.EmptyLegend)).Append("</p>\n");
            html.Append("<p class=\"meta\"><span class=\"chip\">\u02BC<span class=\"cp\">U+02BC</span></span> ").Append(Enc(strings.LookAlikeLegend)).Append("</p>\n");
            html.Append("</header>\n");

            // The option sits before nav and main: the CSS shows the empty rows when it is ticked.
            if (emptyCount > 0)
                html.Append("<input type=\"checkbox\" id=\"showEmpty\" class=\"opt\"><label for=\"showEmpty\" class=\"opt\">")
                    .Append(Enc(string.Format(strings.ShowEmpty, emptyCount))).Append("</label>\n");

            // Contents
            var groups = SettingCatalog.GroupOrder
                .Select(g => new { Key = g, Rows = described.Where(d => d.Group == g).OrderBy(d => d.Order).ToList() })
                .Where(g => g.Rows.Count > 0).ToList();

            html.Append("<nav><h2>").Append(Enc(strings.Contents)).Append("</h2>\n<ul>\n");
            foreach (var g in groups)
            {
                // Worked-out rows (x-) are not settings, so they are not counted.
                int filled = g.Rows.Count(d => d.Item.HasValue && d.IdPrefix != "x"), empty = g.Rows.Count(d => !d.Item.HasValue);
                bool hide = filled == 0 && !(g.Key == SettingCatalog.Language && ldmlMissing);
                html.Append(hide ? "<li class=\"allempty\">" : "<li>").Append("<a href=\"#").Append(g.Key).Append("\">")
                    .Append(Enc(strings.GroupTitle(g.Key))).Append("</a> <span class=\"count\">").Append(filled);
                if (empty > 0)
                    html.Append(" <span class=\"emptycount\">").Append(Enc(string.Format(strings.EmptyCount, empty))).Append("</span>");
                html.Append("</span></li>\n");
            }
            html.Append("</ul>\n</nav>\n<main>\n");

            // One table per group
            foreach (var g in groups)
            {
                bool isLanguage = g.Key == SettingCatalog.Language;
                bool allEmpty = g.Rows.All(d => !d.Item.HasValue) && !(isLanguage && ldmlMissing);
                html.Append("<section id=\"").Append(g.Key).Append(allEmpty ? "\" class=\"allempty\">\n<h2>" : "\">\n<h2>")
                    .Append(Enc(strings.GroupTitle(g.Key)));
                if (isLanguage && !ldmlMissing) html.Append(" <span class=\"count\">(").Append(Enc(ldmlFile)).Append(")</span>");
                html.Append("</h2>\n");
                if (isLanguage && ldmlMissing)
                    html.Append("<p class=\"readonly\">").Append(Enc(string.Format(strings.NoLdml, ldmlExpected ?? "?.ldml"))).Append("</p>\n");
                html.Append("<table>\n<thead><tr><th class=\"name\">").Append(Enc(strings.ColSetting))
                    .Append("</th><th class=\"val\">").Append(Enc(strings.ColValue))
                    .Append("</th><th class=\"desc\">").Append(Enc(strings.ColDescription)).Append("</th></tr></thead>\n<tbody>\n");
                foreach (Described d in g.Rows)
                {
                    // The id prefix keeps Settings.xml (s-), .ldml (l-) and worked-out (x-) rows apart.
                    html.Append("<tr id=\"").Append(d.IdPrefix).Append("-").Append(Enc(d.Item.Name))
                        .Append(d.Item.HasValue ? (d.DisplayName != null ? "\" class=\"derived\">" : "\">") : "\" class=\"isempty\">");
                    if (d.DisplayName != null)
                        html.Append(d.Invalid ? "<td class=\"name invalid\">" : "<td class=\"name\">").Append(Enc(d.DisplayName)).Append("</td><td class=\"val\"");
                    else
                        html.Append(SettingCatalog.IsDisapprovedList(d.Item.Name) ? "<td class=\"name invalid\"><code>" : "<td class=\"name\"><code>")
                            .Append(Enc(d.Item.Name)).Append("</code></td><td class=\"val\"");
                    if (rtl) html.Append(" dir=\"auto\"");
                    html.Append(">").Append(formatter.Format(d)).Append("</td><td class=\"desc\">");
                    if (d.Text != null) html.Append(Enc(d.Text));
                    else html.Append("<span class=\"empty\">").Append(Enc(strings.NoDescription)).Append("</span>");
                    if (d.Note != null) html.Append("<div class=\"small\">").Append(Enc(d.Note)).Append("</div>");
                    html.Append("</td></tr>\n");
                }
                html.Append("</tbody>\n</table>\n</section>\n");
            }
            html.Append("</main>\n</div>\n</body>\n</html>\n");
            return html.ToString();
        }

        /// <summary>The row "Valid characters not in the language settings" (LdmlFile.LettersNotInList).</summary>
        /// <param name="validCharacters">ValidCharacters as stored (the row itself may show only the used part).</param>
        /// <param name="usage">With usage, only letters used in the text are listed, with their counts.</param>
        private static Described NotInLanguageRow(Described validRow, string validCharacters, string simple, string ldmlFile,
            CharacterUsage usage, Strings strings)
        {
            List<string> missing = LdmlFile.LettersNotInList(validCharacters, simple);
            Dictionary<string, int> counts = null;
            if (usage != null)
            {
                counts = CountAll(missing, usage.Count);
                missing = missing.Where(m => counts[m] > 0).ToList();
            }
            Described d = Derived(validRow, "ValidCharacters-notInLanguageSettings",
                usage != null ? strings.NotInLanguageNoneUsed : strings.NotInLanguageNone, missing, strings.NotInLanguageName);
            d.Text = string.Format(usage != null ? strings.NotInLanguageDescUsed : strings.NotInLanguageDesc, ldmlFile);
            if (missing.Count > 0) d.Counts = counts;
            return d;
        }

        /// <summary>The Settings.xml row named name, or (when the file lacks it) where the catalog would put it.</summary>
        private static Described Anchor(List<Described> described, SettingCatalog catalog, Strings strings, string name)
        {
            Described row = described.FirstOrDefault(d => d.IdPrefix == "s" && d.Item.Name == name);
            if (row != null) return row;
            var item = new SettingItem();
            item.Name = name;
            return catalog.Describe(item, strings);
        }

        /// <summary>A row telling whether projectDir holds fileName, with its size and date when it does.</summary>
        private static Described ProjectFileRow(string projectDir, string fileName, Described anchor, string description, Strings strings)
        {
            var file = new FileInfo(Path.Combine(projectDir, fileName));
            string value = file.Exists
                ? string.Format(strings.FilePresent, file.Length, file.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
                : string.Format(strings.FileAbsent, fileName);
            Described d = Derived(anchor, "file-" + fileName, value, new List<string>(), string.Format(strings.ProjectFileName, fileName));
            d.Text = description;
            return d;
        }

        /// <summary>What the used/unused/found rows of one kind of list (characters or punctuation) need.</summary>
        private sealed class UsageRows
        {
            public Func<string, int> Count;
            public int Books;
            public string Decoder;
            public string UsedDesc, UnusedName, UnusedDesc, UnusedNone, FoundName, FoundDesc, FoundNone;
        }

        private static UsageRows CharacterRows(CharacterUsage usage, Strings s)
        {
            return new UsageRows
            {
                Count = usage.Count, Books = usage.BookCount, Decoder = "chars",
                UsedDesc = s.UsedValidDesc, UnusedName = s.UnusedName, UnusedDesc = s.UnusedDesc, UnusedNone = s.UnusedNone,
                FoundName = s.InvalidFoundName, FoundDesc = s.InvalidFoundDesc, FoundNone = s.InvalidFoundNone
            };
        }

        private static UsageRows PunctuationRows(PunctuationUsage usage, Strings s)
        {
            return new UsageRows
            {
                Count = usage.Count, Books = usage.BookCount, Decoder = "punct",
                UsedDesc = s.PunctUsedDesc, UnusedName = s.UnusedName, UnusedDesc = s.PunctUnusedDesc, UnusedNone = s.PunctUnusedNone,
                FoundName = s.PunctFoundName, FoundDesc = s.PunctFoundDesc, FoundNone = s.PunctFoundNone
            };
        }

        /// <summary>
        /// Shows only the approved items used in the text on the Valid... row (with counts) and returns
        /// the row "Approved but not used in the text" for the rest.
        /// </summary>
        private static Described SplitByUsage(Described validRow, UsageRows u, Strings strings)
        {
            List<string> all = Items(validRow.Item.Value);
            Dictionary<string, int> counts = CountAll(all, u.Count);
            List<string> used = all.Where(i => counts[i] > 0).ToList();
            List<string> unused = all.Where(i => counts[i] == 0).ToList();

            // A copy, so the item read from Settings.xml keeps the list as stored.
            var shown = new SettingItem();
            shown.Name = validRow.Item.Name;
            shown.Value = used.Count > 0 ? string.Join(" ", used.ToArray()) : strings.Empty;
            validRow.Item = shown;
            validRow.Decoder = used.Count > 0 ? u.Decoder : "text";
            validRow.Counts = counts;
            validRow.Text = string.Format(u.UsedDesc, u.Books);

            Described d = Derived(validRow, validRow.Item.Name + "-unused", u.UnusedNone, unused, u.UnusedName, u.Decoder);
            d.Text = string.Format(u.UnusedDesc, u.Books);
            return d;
        }

        /// <summary>The row "Disapproved ... found in the text", under the Invalid... row.</summary>
        private static Described InvalidFoundRow(Described invalidRow, UsageRows u)
        {
            List<string> all = Items(invalidRow.Item.Value);
            Dictionary<string, int> counts = CountAll(all, u.Count);
            List<string> found = all.Where(i => counts[i] > 0).ToList();
            Described d = Derived(invalidRow, invalidRow.Item.Name + "-found", u.FoundNone, found, u.FoundName, u.Decoder);
            d.Text = string.Format(u.FoundDesc, u.Books);
            if (found.Count > 0) d.Counts = counts;
            d.Invalid = true;
            return d;
        }

        /// <summary>A worked-out row under an anchor row: its items as chips, or a "None" text.</summary>
        private static Described Derived(Described anchor, string name, string none, List<string> items, string displayName, string decoder = "chars")
        {
            var item = new SettingItem();
            item.Name = name;
            item.Value = items.Count > 0 ? string.Join(" ", items.ToArray()) : none;
            var d = new Described();
            d.Item = item;
            d.Group = anchor.Group;
            d.Order = anchor.Order;
            d.Decoder = items.Count > 0 ? decoder : "text";
            d.IdPrefix = "x";
            d.DisplayName = displayName;
            return d;
        }

        /// <summary>The items of a space-separated list, as the chars and punct decoders split them, without repeats.</summary>
        private static List<string> Items(string value)
        {
            return (value ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        }

        private static Dictionary<string, int> CountAll(IEnumerable<string> items, Func<string, int> count)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string item in items)
                if (!counts.ContainsKey(item)) counts[item] = count(item);
            return counts;
        }

        private static string LangCode(Lang lang)
        {
            switch (lang)
            {
                case Lang.Spanish: return "es";
                case Lang.French: return "fr";
                default: return "en";
            }
        }

        private static string Enc(string s)
        {
            return ValueFormatter.Enc(s);
        }

        /// <summary>Light and dark colours; the project's font for values.</summary>
        private static string Css(string projectFont)
        {
            string valueFont = string.IsNullOrEmpty(projectFont)
                ? "inherit"
                : "'" + ValueFormatter.CssString(projectFont) + "', 'Segoe UI', 'Charis SIL', sans-serif";
            return
                ":root{--bg:#fbfaf7;--fg:#1f2328;--muted:#5f6670;--line:#dcd8cf;--head:#efece4;--chip:#ffffff;--chipline:#cfcabe;--accent:#2f5f8a;--badge:#fde9b8;--badgefg:#5a4300;--warn:#a33a1f;--invalid:#ffd9b0;--emptyname:#fff3a8;}\n" +
                "@media (prefers-color-scheme: dark){:root{--bg:#1c1d1f;--fg:#e6e3dc;--muted:#a3a7ad;--line:#3a3c40;--head:#26282b;--chip:#2a2c30;--chipline:#4a4d52;--accent:#8fb8de;--badge:#5a4a1c;--badgefg:#fbe7b0;--warn:#f08f72;--invalid:#6a4318;--emptyname:#5c5418;}}\n" +
                "*{box-sizing:border-box}\n" +
                "body{margin:0;padding:0 16px 48px;background:var(--bg);color:var(--fg);font:15px/1.5 'Segoe UI',system-ui,sans-serif}\n" +
                ".page{max-width:1200px;margin:0 auto}\n" +
                // Empty settings: hidden unless "Show empty settings" (#showEmpty) is ticked.
                "tr.isempty,section.allempty,li.allempty,.emptycount{display:none}\n" +
                "#showEmpty:checked~main tr.isempty{display:table-row}\n" +
                "#showEmpty:checked~main section.allempty{display:block}\n" +
                "#showEmpty:checked~nav li.allempty{display:list-item}\n" +
                "#showEmpty:checked~nav .emptycount{display:inline}\n" +
                "input.opt{margin:16px 6px 0 0;width:16px;height:16px;vertical-align:middle}\n" +
                "label.opt{display:inline-block;margin-top:16px;vertical-align:middle;cursor:pointer;font-weight:600}\n" +
                "td.name.invalid{background:var(--invalid)}\n" +
                "tr.isempty td.name{background:var(--emptyname)}\n" +
                "tr.derived td.name{font-style:italic;padding-left:22px}\n" +
                "sup.n{margin-left:3px;font:10px/1 'Segoe UI',sans-serif;color:var(--muted);vertical-align:super}\n" +
                ".cp{display:block;font:9px/1.2 Consolas,'Courier New',monospace;color:var(--accent);white-space:nowrap}\n" +
                ".cp.inline{display:inline;font-size:11px;margin-left:4px}\n" +
                ".swatch.emptyswatch{background:var(--emptyname)}\n" +
                "pre.rules{margin:0;white-space:pre-wrap;font-family:Consolas,'Courier New',monospace;font-size:.85em}\n" +
                "ol.lines{margin:0;padding-left:2.2em;columns:4 9em;column-gap:1.5em}ol.lines li{margin:2px 0;break-inside:avoid}\n" +
                ".swatch{display:inline-block;width:2.2em;height:1em;vertical-align:middle;background:var(--invalid);border:1px solid var(--chipline);border-radius:2px}\n" +
                "h1{font-size:1.7em;margin:28px 0 2px}\n" +
                "h2{font-size:1.25em;margin:32px 0 10px;padding-bottom:4px;border-bottom:2px solid var(--accent);color:var(--accent)}\n" +
                "nav h2{border:0;margin-top:20px}\n" +
                "nav ul{list-style:none;padding:0;margin:0;columns:3 220px}\n" +
                "nav li{margin:2px 0}\n" +
                "a{color:var(--accent)}\n" +
                ".full{font-size:1.1em;margin:0 0 8px}\n" +
                ".meta,.small,.count,.stored,.note{color:var(--muted);font-size:.86em}\n" +
                ".count{margin-left:4px}\n" +
                ".readonly{border-left:4px solid var(--accent);padding:6px 10px;background:var(--head)}\n" +
                "table{width:100%;border-collapse:collapse;table-layout:fixed}\n" +
                "th,td{text-align:left;vertical-align:top;padding:8px 10px;border-bottom:1px solid var(--line)}\n" +
                "th{background:var(--head);font-weight:600}\n" +
                "th.name{width:20%}th.val{width:38%}\n" +
                "td.name code{word-break:break-all}\n" +
                "td.val{overflow-wrap:anywhere}\n" +
                "code{font-family:Consolas,'Courier New',monospace;font-size:.92em}\n" +
                ".v{font-family:" + valueFont + "}\n" +
                ".stored{margin-top:4px}.stored code{overflow-wrap:anywhere}\n" +
                ".empty{color:var(--muted);font-style:italic}\n" +
                ".warn{color:var(--warn);font-size:.86em}\n" +
                ".dim{color:var(--muted)}\n" +
                ".chips{display:flex;flex-wrap:wrap;gap:4px;margin:2px 0}\n" +
                ".chip{display:inline-block;min-width:1.6em;text-align:center;padding:1px 6px;border:1px solid var(--chipline);border-radius:4px;background:var(--chip)}\n" +
                ".chip.book{font-family:Consolas,'Courier New',monospace;font-size:.85em}\n" +
                ".chip.absent{border-style:dashed;border-color:var(--warn);color:var(--warn);background:transparent}\n" +
                ".missing{margin-top:6px}\n" +
                ".badge{display:inline-block;margin:0 1px;padding:0 4px;border-radius:3px;background:var(--badge);color:var(--badgefg);font:11px/1.5 'Segoe UI',sans-serif;vertical-align:middle}\n" +
                ".note{margin-bottom:4px}\n" +
                "table.parts{width:auto;table-layout:auto}table.parts td{border:0;padding:0 12px 0 0}\n" +
                "ul.bar{margin:0;padding-left:18px}\n" +
                "@media (max-width:700px){table,thead,tbody,tr,th,td{display:block;width:auto!important}thead{display:none}tr{border-bottom:1px solid var(--line);padding:6px 0}td{border:0;padding:3px 0}#showEmpty:checked~main tr.isempty{display:block}}\n" +
                "@media print{body{background:#fff;color:#000}nav,.opt{display:none}h2{break-after:avoid}tr{break-inside:avoid}td.name{-webkit-print-color-adjust:exact;print-color-adjust:exact}}\n";
        }
    }
}
