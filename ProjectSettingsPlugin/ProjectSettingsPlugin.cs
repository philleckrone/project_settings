using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using Microsoft.Win32;
using Paratext.PluginInterfaces;

namespace ProjectSettings
{
    /// <summary>
    /// Paratext plugin that adds Tools > Project Settings Report...: the project's Settings.xml as
    /// a page in the web browser, with a description of each setting. It only reads the project;
    /// the page is written to the project's local folder.
    /// </summary>
    public class ProjectSettingsPlugin : IParatextStandalonePlugin
    {
        public const string PluginName = "Project Settings Report";
        public const string PublisherName = "Phil Leckrone (SIL)";

        private const string DefaultProjectsDir = @"C:\My Paratext 9 Projects";

        private IPluginHost host;

        public string Name
        {
            get { return PluginName; }
        }

        public Version Version
        {
            get { return new Version(Strings.ToolVersion); }
        }

        public string VersionString
        {
            get { return Strings.ToolVersion; }
        }

        public string Publisher
        {
            get { return PublisherName; }
        }

        public IEnumerable<PluginMenuEntry> PluginMenuEntries
        {
            get
            {
                var entry = new PluginMenuEntry(Strings.English().MenuText, OnMenuClicked, PluginMenuLocation.ScrTextTools);
                entry.LocalizedTextNeeded += LocalizeMenuText;
                yield return entry;
            }
        }

        /// <summary>Menu text in Paratext's interface language; null keeps English.</summary>
        internal static string LocalizeMenuText(string defaultText, string locale)
        {
            return Strings.LanguageOf(locale) != Lang.English ? Strings.For(locale).MenuText : null;
        }

        public string GetDescription(string locale)
        {
            return Strings.For(locale).Description;
        }

        public IDataFileMerger GetMerger(IPluginHost pluginHost, string dataIdentifier)
        {
            return null;   // the plugin keeps no project data
        }

        // ------------------------------------------------------------ the report

        private void OnMenuClicked(IPluginHost pluginHost, IParatextChildState state)
        {
            if (host == null) host = pluginHost;
            Strings strings = Strings.For(UiLocale(host));
            try
            {
                IProject project = state != null ? state.Project : null;
                if (project == null || project.IsResource)
                {
                    Report(strings, strings.OpenProjectFirst, null);
                    return;
                }

                string projectsDir = ProjectsDirectory();
                string projectDir = ResolveProjectDir(projectsDir, project.ShortName, project.ID);
                if (projectDir == null)
                {
                    Report(strings, string.Format(strings.NoProjectFolder, project.ShortName), null);
                    return;
                }
                if (!File.Exists(Path.Combine(projectDir, "Settings.xml")))
                {
                    Report(strings, string.Format(strings.NoSettingsFile, projectDir), null);
                    return;
                }

                string outPath = ReportWriter.DefaultReportPath(projectDir);
                // Reading every book to count the characters can take a few seconds.
                Cursor previous = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    ReportWriter.Write(projectDir, Path.Combine(projectsDir, "cms"), strings, outPath, BookTexts(project));
                }
                finally
                {
                    Cursor.Current = previous;
                }

                // Opens the page in the default web browser.
                var start = new ProcessStartInfo(outPath);
                start.UseShellExecute = true;
                Process.Start(start);
            }
            catch (Exception ex)
            {
                Report(strings, string.Format(strings.Error, ex.Message), ex);
            }
        }

        /// <summary>
        /// The USFM of every book in the project, through Paratext (what its editor shows). A book
        /// that can't be read is skipped (null), so the rest are still counted.
        /// </summary>
        private static IEnumerable<string> BookTexts(IProject project)
        {
            foreach (var book in project.AvailableBooks.OrderBy(b => b.Number))
            {
                string usfm = null;
                try
                {
                    usfm = project.GetUSFM(book.Number, 0);
                }
                catch (Exception) { }
                yield return usfm;
            }
        }

        private void Report(Strings strings, string message, Exception ex)
        {
            try
            {
                if (host != null)
                    host.Log(this, message + (ex != null ? "\n" + ex : ""));
            }
            catch (Exception) { }
            MessageBox.Show(message, strings.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private static string UiLocale(IPluginHost pluginHost)
        {
            try
            {
                if (pluginHost != null && pluginHost.UserSettings != null)
                    return pluginHost.UserSettings.UiLocale;
            }
            catch (Exception) { }
            return null;
        }

        // ------------------------------------------------------------ project folder

        /// <summary>
        /// The Paratext projects folder: Settings_Directory under SOFTWARE\Paratext\8 (Paratext 9
        /// still uses that key), checked in both registry views, then for the current user.
        /// Same as the AutoCorrect plugin.
        /// </summary>
        internal static string ProjectsDirectory()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                string dir = ReadSettingsDirectory(RegistryHive.LocalMachine, view);
                if (dir != null) return dir;
            }
            string user = ReadSettingsDirectory(RegistryHive.CurrentUser, RegistryView.Default);
            return user ?? DefaultProjectsDir;
        }

        private static string ReadSettingsDirectory(RegistryHive hive, RegistryView view)
        {
            try
            {
                using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = root.OpenSubKey(@"SOFTWARE\Paratext\8"))
                {
                    if (key == null) return null;
                    string dir = key.GetValue("Settings_Directory") as string;
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        return dir.TrimEnd('\\');
                }
            }
            catch (Exception) { }
            return null;
        }

        /// <summary>
        /// The project's folder: projectsDir\shortName when its Settings.xml Guid matches the
        /// project ID (or has no readable Guid); otherwise the subfolder whose Guid matches.
        /// Same as the AutoCorrect plugin.
        /// </summary>
        internal static string ResolveProjectDir(string projectsDir, string shortName, string projectId)
        {
            if (string.IsNullOrEmpty(projectsDir) || !Directory.Exists(projectsDir)) return null;

            string byName = null;
            if (!string.IsNullOrEmpty(shortName))
            {
                string candidate = Path.Combine(projectsDir, shortName);
                string settings = Path.Combine(candidate, "Settings.xml");
                if (File.Exists(settings))
                {
                    byName = candidate;
                    string guid = ReadProjectGuid(settings);
                    if (guid == null || string.IsNullOrEmpty(projectId) || SameId(guid, projectId))
                        return candidate;
                }
            }

            if (string.IsNullOrEmpty(projectId)) return byName;
            try
            {
                foreach (string dir in Directory.GetDirectories(projectsDir))
                {
                    string settings = Path.Combine(dir, "Settings.xml");
                    if (File.Exists(settings) && SameId(ReadProjectGuid(settings), projectId))
                        return dir;
                }
            }
            catch (Exception) { }
            return byName;
        }

        private static bool SameId(string a, string b)
        {
            return a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        internal static string ReadProjectGuid(string settingsPath)
        {
            try
            {
                var xmlSettings = new XmlReaderSettings();
                xmlSettings.DtdProcessing = DtdProcessing.Ignore;
                using (XmlReader reader = XmlReader.Create(settingsPath, xmlSettings))
                {
                    if (reader.ReadToFollowing("Guid"))
                    {
                        string guid = reader.ReadElementContentAsString();
                        return string.IsNullOrEmpty(guid) ? null : guid.Trim();
                    }
                }
            }
            catch (Exception) { }
            return null;
        }
    }
}
