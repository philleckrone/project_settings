using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Text;

namespace ProjectSettings
{
    /// <summary>
    /// Makes the report of a project folder and writes it to a file. Paratext-free, so the test
    /// host uses it too. It reads Settings.xml and the .cms files and writes only the report.
    /// </summary>
    public static class ReportWriter
    {
        /// <summary>The report's place: the project's local folder, which Send/Receive does not share.</summary>
        public static string DefaultReportPath(string projectDir)
        {
            return Path.Combine(projectDir, "local", "ProjectSettings", "SettingsReport.html");
        }

        /// <summary>Writes the report of projectDir\Settings.xml to outPath (folders are created).</summary>
        /// <param name="cmsDir">Folder of custom checks: their options are recognized and left out of the report; null or missing = none.</param>
        /// <param name="bookTexts">The USFM of each book, read lazily, to count the characters and punctuation used; null = don't count.</param>
        public static void Write(string projectDir, string cmsDir, Strings strings, string outPath, IEnumerable<string> bookTexts)
        {
            string settingsPath = Path.Combine(projectDir, "Settings.xml");
            List<SettingItem> items = SettingsFile.Read(settingsPath);
            var catalog = new SettingCatalog(CmsOptions.Load(cmsDir));
            List<SettingItem> language = ReadLanguage(projectDir, items);
            CharacterUsage characters = null;
            PunctuationUsage punctuation = null;
            if (bookTexts != null) CountText(projectDir, items, language, bookTexts, out characters, out punctuation);
            string html = HtmlReport.Build(items, settingsPath, language, characters, punctuation, catalog, strings, FontInstalled, DateTime.Now);

            string folder = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(outPath, html, new UTF8Encoding(false));
        }

        /// <summary>
        /// Reads each book once into Paratext's text tokens (with the project's stylesheet, for what
        /// is publishable) and counts both the characters and the punctuation-inventory items.
        /// </summary>
        public static void CountText(string projectDir, List<SettingItem> items, List<SettingItem> language,
            IEnumerable<string> bookTexts, out CharacterUsage characters, out PunctuationUsage punctuation)
        {
            StyleSheets styles = StyleSheets.Load(projectDir, SettingsFile.ValueOf(items, "StyleSheet"));
            var books = new List<List<TextTok>>();
            foreach (string usfm in bookTexts)
                if (!string.IsNullOrEmpty(usfm)) books.Add(UsfmTextTokens.Tokenize(usfm, styles));
            characters = new CharacterUsage(books);
            punctuation = new PunctuationUsage(books, PunctuationRules.From(items, language));
        }

        /// <summary>The language settings rows, or null when the .ldml file can't be read (the report still appears).</summary>
        public static List<SettingItem> ReadLanguage(string projectDir, List<SettingItem> items)
        {
            try
            {
                return LdmlFile.Read(projectDir, SettingsFile.ValueOf(items, "LanguageIsoCode"));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static HashSet<string> installedFonts;

        /// <summary>Whether a font family of that name is installed (checked once per session).</summary>
        public static bool FontInstalled(string name)
        {
            if (installedFonts == null)
            {
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using (var fonts = new InstalledFontCollection())
                        foreach (FontFamily family in fonts.Families)
                            set.Add(family.Name);
                }
                catch (Exception)
                {
                    return true;   // can't tell: say nothing
                }
                installedFonts = set;
            }
            return installedFonts.Contains(name);
        }
    }
}
