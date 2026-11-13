using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ProjectSettings;

namespace ProjectSettingsTests
{
    /// <summary>
    /// Test host for the plugin's Paratext-free code.
    ///   report PROJECTDIR OUT.html [LANG] [CMSDIR]   writes the report (LANG en/es/fr; CMSDIR = folder of .cms files)
    ///   unknown ROOTDIR [CMSDIR]                     lists the settings with no description, over every
    ///                                                ROOTDIR\*\Settings.xml (and ROOTDIR\Settings.xml), with counts
    ///   check ROOTDIR [CMSDIR]                       makes the report of every project in all three languages
    ///                                                (in memory) and checks each setting appears once
    /// Output is UTF-8 on standard output.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var output = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            output.AutoFlush = true;
            try
            {
                string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
                switch (mode)
                {
                    case "report":
                        ReportWriter.Write(args[1], args.Length > 4 ? args[4] : null, Strings.For(args.Length > 3 ? args[3] : "en"), args[2],
                            Environment.GetEnvironmentVariable("PS_NOTEXT") == "1" ? null : FolderBooks.Texts(args[1]));
                        output.WriteLine("WROTE " + args[2]);
                        return 0;
                    case "count":
                    {
                        // count PROJECTDIR ITEM...: how often each item occurs in the books' text
                        // (as a character and as a punctuation-inventory item).
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        CharacterUsage usage;
                        PunctuationUsage punct;
                        Count(args[1], out usage, out punct);
                        output.WriteLine("BOOKS " + usage.BookCount + " MS " + watch.ElapsedMilliseconds);
                        for (int i = 2; i < args.Length; i++)
                            output.WriteLine("COUNT " + args[i] + " " + usage.Count(args[i]) + " PUNCT " + punct.Count(args[i]));
                        return 0;
                    }
                    case "punct":
                        if (args.Length > 2)
                        {
                            // punct PROJECTDIR ITEM: many samples of the text where that item was found.
                            PunctuationUsage.ExampleLimit = 60;
                            CharacterUsage u;
                            PunctuationUsage pu;
                            Count(args[1], out u, out pu);
                            List<string> samples;
                            if (pu.Examples.TryGetValue(args[2], out samples))
                                foreach (string sample in samples) output.WriteLine("[" + sample + "]");
                            return 0;
                        }
                        return Punct(output, args[1]);
                    case "punctfile":
                    {
                        // punctfile PROJECTDIR FILE: the punctuation items of one USFM file, with the project's rules.
                        List<SettingItem> items = SettingsFile.Read(Path.Combine(args[1], "Settings.xml"));
                        List<SettingItem> language = ReportWriter.ReadLanguage(args[1], items);
                        CharacterUsage u;
                        PunctuationUsage pu;
                        ReportWriter.CountText(args[1], items, language, new[] { File.ReadAllText(args[2]) }, out u, out pu);
                        foreach (var kv in pu.Items.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                            output.WriteLine(kv.Value + "\t[" + kv.Key + "]");
                        return 0;
                    }
                    case "tokens":
                    {
                        // tokens PROJECTDIR BOOKFILE [TEXT]: the text tokens of one book file (those holding TEXT).
                        List<SettingItem> items = SettingsFile.Read(Path.Combine(args[1], "Settings.xml"));
                        StyleSheets styles = StyleSheets.Load(args[1], SettingsFile.ValueOf(items, "StyleSheet"));
                        foreach (TextTok t in UsfmTextTokens.Tokenize(File.ReadAllText(Path.Combine(args[1], args[2])), styles))
                        {
                            if (args.Length > 3 && t.Text.IndexOf(args[3], StringComparison.Ordinal) < 0) continue;
                            output.WriteLine(t.Type + (t.ParaStart ? " P" : "") + (t.NoteStart ? " N" : "") + (t.CellStart ? " C" : "") + (t.IsFigure ? " F" : "") + " [" + t.Text + "]");
                        }
                        return 0;
                    }
                    case "unknown":
                        return Unknown(output, args[1], args.Length > 2 ? args[2] : null);
                    case "check":
                        return Check(output, args[1], args.Length > 2 ? args[2] : null);
                    case "notin":
                        return NotIn(output, args[1]);
                    default:
                        output.WriteLine("usage: report PROJECTDIR OUT.html [LANG] [CMSDIR] (PS_NOTEXT=1: don't read the books) | count PROJECTDIR ITEM... | unknown ROOTDIR [CMSDIR] | check ROOTDIR [CMSDIR] | notin ROOTDIR");
                        return 2;
                }
            }
            catch (Exception ex)
            {
                output.WriteLine("ERROR " + ex);
                return 1;
            }
        }

        private static List<string> SettingsFiles(string root)
        {
            var files = new List<string>();
            if (File.Exists(Path.Combine(root, "Settings.xml"))) files.Add(Path.Combine(root, "Settings.xml"));
            foreach (string dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                string path = Path.Combine(dir, "Settings.xml");
                if (File.Exists(path)) files.Add(path);
            }
            return files;
        }

        private static int Unknown(StreamWriter output, string root, string cmsDir)
        {
            var catalog = new SettingCatalog(CmsOptions.Load(cmsDir));
            Strings strings = Strings.English();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var groups = new Dictionary<string, int>(StringComparer.Ordinal);
            int files = 0, failed = 0;
            foreach (string path in SettingsFiles(root))
            {
                List<SettingItem> items;
                try
                {
                    items = SettingsFile.Read(path);
                }
                catch (Exception ex)
                {
                    output.WriteLine("UNREADABLE " + path + ": " + ex.Message);
                    failed++;
                    continue;
                }
                files++;
                foreach (SettingItem item in items)
                {
                    Described d = catalog.Describe(item, strings);
                    int n;
                    groups.TryGetValue(d.Group, out n);
                    groups[d.Group] = n + 1;
                    if (d.Text != null) continue;
                    counts.TryGetValue(item.Name, out n);
                    counts[item.Name] = n + 1;
                }
            }
            output.WriteLine("FILES " + files + " UNREADABLE " + failed + " CMSOPTIONS " + catalog.CmsOptionCount);
            foreach (var g in groups.OrderBy(g => Array.IndexOf(SettingCatalog.GroupOrder, g.Key)))
                output.WriteLine("GROUP " + g.Key + " " + g.Value);
            foreach (var kv in counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                output.WriteLine("UNKNOWN " + kv.Value + " " + kv.Key);
            output.WriteLine("UNKNOWN NAMES " + counts.Count);
            return 0;
        }

        private static int Check(StreamWriter output, string root, string cmsDir)
        {
            var catalog = new SettingCatalog(CmsOptions.Load(cmsDir));
            int files = 0, problems = 0, noLdml = 0, withBooks = 0;
            foreach (string path in SettingsFiles(root))
            {
                List<SettingItem> items;
                try
                {
                    items = SettingsFile.Read(path);
                }
                catch (Exception ex)
                {
                    output.WriteLine("UNREADABLE " + path + ": " + ex.Message);
                    continue;
                }
                files++;
                // Language settings: the .ldml named after LanguageIsoCode must be found and read,
                // and every character set in it must parse.
                List<SettingItem> language = LdmlFile.Read(Path.GetDirectoryName(path), SettingsFile.ValueOf(items, "LanguageIsoCode"));
                if (string.IsNullOrEmpty(SettingsFile.ValueOf(language, LdmlFile.FileRow)))
                {
                    output.WriteLine("NOLDML " + path + " (" + SettingsFile.ValueOf(items, "LanguageIsoCode") + ")");
                    noLdml++;
                }
                foreach (SettingItem row in language.Where(r => r.Value.StartsWith("[", StringComparison.Ordinal)))
                {
                    try
                    {
                        LdmlFile.ParseSet(row.Value);
                    }
                    catch (FormatException ex)
                    {
                        output.WriteLine("SET " + path + " " + row.Name + ": " + ex.Message + " " + row.Value);
                        problems++;
                    }
                }
                // Characters used in the text of the project's books.
                CharacterUsage usage;
                PunctuationUsage punct;
                ReportWriter.CountText(Path.GetDirectoryName(path), items, language, FolderBooks.Texts(Path.GetDirectoryName(path)), out usage, out punct);
                if (usage.BookCount > 0) withBooks++;
                foreach (string lang in new[] { "en", "es", "fr" })
                {
                    Strings strings = Strings.For(lang);
                    string html = HtmlReport.Build(items, path, language, usage, punct, catalog, strings, null, DateTime.Now);
                    problems += CheckUsageRows(output, path + " " + lang, html, items, usage.BookCount > 0, "Characters");
                    problems += CheckUsageRows(output, path + " " + lang, html, items, punct.BookCount > 0, "Punctuation");
                    problems += CheckLookAlikes(output, path + " " + lang, html);
                    // "Valid characters not in the language settings": one row when both lists exist.
                    bool wantNotIn = !string.IsNullOrEmpty(SettingsFile.ValueOf(items, "ValidCharacters"))
                        && SettingsFile.ValueOf(items, "ValidCharacters") != "NullValue"
                        && !string.IsNullOrEmpty(SettingsFile.ValueOf(language, "sil:simple"));
                    if (Count(html, "id=\"x-ValidCharacters-notInLanguageSettings\"") != (wantNotIn ? 1 : 0))
                    {
                        output.WriteLine("NOTIN " + path + " " + lang + ": row " + (wantNotIn ? "missing" : "unexpected"));
                        problems++;
                    }
                    // custom.sty, custom.vrs, autocorrect.txt: one row each, saying Yes exactly when the file exists.
                    foreach (string file in new[] { "custom.sty", "custom.vrs", "autocorrect.txt" })
                    {
                        string fileRow = RowHtml(html, "x-file-" + file);
                        bool exists = File.Exists(Path.Combine(Path.GetDirectoryName(path), file));
                        string yes = strings.FilePresent.Substring(0, strings.FilePresent.IndexOf(':'));
                        if (Count(html, "id=\"x-file-" + file + "\"") != 1 || fileRow.Contains(">" + ValueFormatter.Enc(yes)) != exists)
                        {
                            output.WriteLine("FILEROW " + path + " " + lang + " " + file + ": exists=" + exists);
                            problems++;
                        }
                    }
                    foreach (SettingItem row in language)
                    {
                        string lid = "id=\"l-" + System.Net.WebUtility.HtmlEncode(row.Name) + "\"";
                        if (Count(html, lid) != 1)
                        {
                            output.WriteLine("LROWS " + path + " " + lang + " " + row.Name + ": " + Count(html, lid) + " rows");
                            problems++;
                        }
                    }
                    // Every element has one row, found by its id, except the groups left out (IsShown),
                    // which have none; exactly the empty ones are hidden (class isempty, "Show empty settings").
                    var reported = items.Where(i => SettingCatalog.IsShown(catalog.Describe(i, strings).Group)).ToList();
                    foreach (var group in items.GroupBy(i => i.Name))
                    {
                        string id = "id=\"s-" + System.Net.WebUtility.HtmlEncode(group.Key) + "\"";
                        int found = Count(html, id);
                        int hidden = Count(html, id + " class=\"isempty\"");
                        int expected = group.Count(i => reported.Contains(i));
                        int empty = group.Count(i => reported.Contains(i) && !i.HasValue);
                        if (found != expected || hidden != empty)
                        {
                            output.WriteLine("ROWS " + path + " " + lang + " " + group.Key + ": " + found + " rows (" + hidden + " hidden), "
                                + expected + " expected (" + empty + " empty)");
                            problems++;
                        }
                    }
                    if (html.Contains("id=\"" + SettingCatalog.Cms + "\""))
                    {
                        output.WriteLine("CMS " + path + " " + lang + ": custom-check section present");
                        problems++;
                    }
                    bool hasEmpty = reported.Any(i => !i.HasValue) || language.Any(i => !i.HasValue);
                    if (hasEmpty != html.Contains("id=\"showEmpty\""))
                    {
                        output.WriteLine("OPTION " + path + " " + lang + ": checkbox " + (hasEmpty ? "missing" : "shown with nothing empty"));
                        problems++;
                    }
                }
            }
            output.WriteLine("CHECKED " + files + " PROBLEMS " + problems + " NOLDML " + noLdml + " WITHBOOKS " + withBooks);
            return problems == 0 ? 0 : 1;
        }

        /// <summary>
        /// With books read, for kind Characters or Punctuation: the unused row exists exactly when
        /// Valid{kind} has a value, the found row exactly when Invalid{kind} has one, and the used
        /// chips plus the unused chips are the whole Valid{kind} list (each item once).
        /// </summary>
        private static int CheckUsageRows(StreamWriter output, string where, string html, List<SettingItem> items, bool counted, string kind)
        {
            int problems = 0;
            string valid = SettingsFile.ValueOf(items, "Valid" + kind);
            string invalid = SettingsFile.ValueOf(items, "Invalid" + kind);
            bool hasValid = !string.IsNullOrEmpty(valid) && valid != "NullValue";
            bool hasInvalid = !string.IsNullOrEmpty(invalid) && invalid != "NullValue";
            if (Count(html, "id=\"x-Valid" + kind + "-unused\"") != (counted && hasValid ? 1 : 0))
            {
                output.WriteLine("UNUSED " + kind + " " + where + ": row count wrong");
                problems++;
            }
            if (Count(html, "id=\"x-Invalid" + kind + "-found\"") != (counted && hasInvalid ? 1 : 0))
            {
                output.WriteLine("FOUND " + kind + " " + where + ": row count wrong");
                problems++;
            }
            if (counted && hasValid)
            {
                int all = valid.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct().Count();
                int chips = Count(RowHtml(html, "s-Valid" + kind), "<span class=\"chip\"") + Count(RowHtml(html, "x-Valid" + kind + "-unused"), "<span class=\"chip\"");
                if (chips != all)
                {
                    output.WriteLine("SPLIT " + kind + " " + where + ": used+unused " + chips + " chips, Valid" + kind + " has " + all + " items");
                    problems++;
                }
            }
            return problems;
        }

        /// <summary>A box holding one of these look-alikes must show its Unicode value.</summary>
        private static int CheckLookAlikes(StreamWriter output, string where, string html)
        {
            int problems = 0;
            foreach (int cp in new[] { 0x0445, 0x2019, 0xA78C, 0x2014, 0x0027 })
            {
                string ch = char.ConvertFromUtf32(cp);
                string code = "U+" + cp.ToString("X4");
                string enc = System.Net.WebUtility.HtmlEncode(ch);
                bool inChip = html.Contains("<span class=\"chip\">" + enc) || html.Contains("<span class=\"chip\">" + ch);
                if (inChip && !html.Contains(code))
                {
                    output.WriteLine("LOOKALIKE " + where + ": " + code + " in a box without its value");
                    problems++;
                }
            }
            return problems;
        }

        private static void Count(string projectDir, out CharacterUsage usage, out PunctuationUsage punct)
        {
            List<SettingItem> items = SettingsFile.Read(Path.Combine(projectDir, "Settings.xml"));
            List<SettingItem> language = ReportWriter.ReadLanguage(projectDir, items);
            ReportWriter.CountText(projectDir, items, language, FolderBooks.Texts(projectDir), out usage, out punct);
        }

        /// <summary>
        /// punct PROJECTDIR: every punctuation-inventory item found in the text with its count, marked
        /// V (in ValidPunctuation), I (in InvalidPunctuation) or ? (in neither). A reviewed inventory
        /// should leave few ?: that shows the items are formed the way Paratext forms them.
        /// </summary>
        private static int Punct(StreamWriter output, string projectDir)
        {
            List<SettingItem> items = SettingsFile.Read(Path.Combine(projectDir, "Settings.xml"));
            CharacterUsage usage;
            PunctuationUsage punct;
            Count(projectDir, out usage, out punct);
            var valid = new HashSet<string>((SettingsFile.ValueOf(items, "ValidPunctuation") ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            var invalid = new HashSet<string>((SettingsFile.ValueOf(items, "InvalidPunctuation") ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            int kinds = 0, known = 0, total = 0, knownTotal = 0;
            foreach (var kv in punct.Items.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal))
            {
                string mark = valid.Contains(kv.Key) ? "V" : invalid.Contains(kv.Key) ? "I" : "?";
                List<string> ex;
                string sample = mark == "?" && punct.Examples.TryGetValue(kv.Key, out ex) ? "\t| " + string.Join(" | ", ex.ToArray()) : "";
                output.WriteLine(mark + "\t" + kv.Value + "\t" + kv.Key + "\t" + LookAlikes.Codes(kv.Key) + sample);
                kinds++;
                total += kv.Value;
                if (mark != "?")
                {
                    known++;
                    knownTotal += kv.Value;
                }
            }
            output.WriteLine("ADVANCED " + (SettingsFile.ValueOf(items, "AdvancedView") == "True") + " BOOKS " + punct.BookCount
                + " ITEMS " + kinds + " LISTED " + known + " OCCURRENCES " + total + " LISTED " + knownTotal
                + " COVERAGE " + (total > 0 ? (100.0 * knownTotal / total).ToString("0.0") : "-") + "%");
            return 0;
        }

        /// <summary>The HTML of the row with that id (up to its &lt;/tr&gt;), or "".</summary>
        private static string RowHtml(string html, string id)
        {
            int start = html.IndexOf("<tr id=\"" + id + "\"", StringComparison.Ordinal);
            if (start < 0) return "";
            int end = html.IndexOf("</tr>", start, StringComparison.Ordinal);
            return end < 0 ? "" : html.Substring(start, end - start);
        }

        /// <summary>For each project: the ValidCharacters letters missing from the .ldml Alphabetic characters list.</summary>
        private static int NotIn(StreamWriter output, string root)
        {
            int withMissing = 0, compared = 0;
            foreach (string path in SettingsFiles(root))
            {
                List<SettingItem> items = SettingsFile.Read(path);
                List<SettingItem> language = LdmlFile.Read(Path.GetDirectoryName(path), SettingsFile.ValueOf(items, "LanguageIsoCode"));
                string valid = SettingsFile.ValueOf(items, "ValidCharacters"), simple = SettingsFile.ValueOf(language, "sil:simple");
                if (string.IsNullOrEmpty(valid) || string.IsNullOrEmpty(simple)) continue;
                compared++;
                List<string> missing = LdmlFile.LettersNotInList(valid, simple);
                if (missing.Count > 0) withMissing++;
                output.WriteLine(Path.GetFileName(Path.GetDirectoryName(path)) + "\t" + string.Join(" ", missing.ToArray()));
            }
            output.WriteLine("COMPARED " + compared + " WITH MISSING " + withMissing);
            return 0;
        }

        private static int Count(string text, string what)
        {
            int n = 0, at = 0;
            while ((at = text.IndexOf(what, at, StringComparison.Ordinal)) >= 0)
            {
                n++;
                at += what.Length;
            }
            return n;
        }
    }
}
