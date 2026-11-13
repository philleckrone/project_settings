using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProjectSettings
{
    /// <summary>An option declared by a custom check (.cms file). Paratext saves its value in Settings.xml under the option's name.</summary>
    public sealed class CmsOption
    {
        public string Name;
        public string LocalizedName;
        public string Description;

        /// <summary>The titles of the checks that declare this option, first the one whose description is used.</summary>
        public readonly List<string> Checks = new List<string>();
    }

    /// <summary>
    /// Reads the options of the custom checks in a cms folder: \check, \optionName,
    /// \optionLocalizedName and \optionDescription lines. Never writes.
    /// </summary>
    public static class CmsOptions
    {
        public static Dictionary<string, CmsOption> Load(string cmsDir)
        {
            var options = new Dictionary<string, CmsOption>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(cmsDir) || !Directory.Exists(cmsDir)) return options;

            string[] files;
            try
            {
                files = Directory.GetFiles(cmsDir, "*.cms", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return options;
            }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string text;
                try
                {
                    text = ReadText(file);
                }
                catch (Exception)
                {
                    continue;
                }
                AddOptions(options, text, Path.GetFileNameWithoutExtension(file));
            }
            return options;
        }

        private static void AddOptions(Dictionary<string, CmsOption> options, string text, string fileName)
        {
            string check = fileName;
            CmsOption current = null;
            string currentCheck = null, currentLocalized = null, currentDescription = null;

            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r').TrimStart();
                string value;
                if (Field(line, "\\check", out value))
                {
                    if (value.Length > 0) check = value;
                }
                else if (Field(line, "\\optionName", out value))
                {
                    Finish(current, currentCheck, currentLocalized, currentDescription);
                    current = null;
                    currentCheck = check;
                    currentLocalized = null;
                    currentDescription = null;
                    if (value.Length == 0) continue;
                    if (!options.TryGetValue(value, out current))
                    {
                        current = new CmsOption();
                        current.Name = value;
                        options[value] = current;
                    }
                    if (!current.Checks.Contains(check)) current.Checks.Add(check);
                }
                else if (current != null && Field(line, "\\optionLocalizedName", out value))
                {
                    currentLocalized = value;
                }
                else if (current != null && Field(line, "\\optionDescription", out value))
                {
                    currentDescription = value;
                }
            }
            Finish(current, currentCheck, currentLocalized, currentDescription);
        }

        /// <summary>The first check that gives a name or description wins.</summary>
        private static void Finish(CmsOption option, string check, string localized, string description)
        {
            if (option == null) return;
            if (string.IsNullOrEmpty(option.LocalizedName) && !string.IsNullOrEmpty(localized))
                option.LocalizedName = localized;
            if (string.IsNullOrEmpty(option.Description) && !string.IsNullOrEmpty(description))
            {
                option.Description = description;
                // The check whose description is shown comes first.
                option.Checks.Remove(check);
                option.Checks.Insert(0, check);
            }
        }

        /// <summary>A "\field value" line (the field name must be followed by white space or the end).</summary>
        private static bool Field(string line, string field, out string value)
        {
            value = null;
            if (!line.StartsWith(field, StringComparison.Ordinal)) return false;
            if (line.Length > field.Length && !char.IsWhiteSpace(line[field.Length])) return false;
            value = line.Substring(field.Length).Trim();
            return true;
        }

        /// <summary>
        /// .cms files are UTF-8 (with or without BOM), sometimes UTF-16, and some old ones are in
        /// the Windows code page. Invalid UTF-8 means the Windows code page.
        /// </summary>
        internal static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            int start = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes, start, bytes.Length - start);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }
    }
}
