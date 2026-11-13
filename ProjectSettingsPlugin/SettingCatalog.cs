using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ProjectSettings
{
    /// <summary>A line of settings.txt: what one setting (or a family of them) is.</summary>
    public sealed class SettingInfo
    {
        public string Name;
        public string Group;
        public string Decoder;
        public string[] Text = new string[3];   // by Lang

        /// <summary>Line order in settings.txt: the order of the rows within a group.</summary>
        public int Index;

        public bool IsPattern
        {
            get { return Name.IndexOf('*') >= 0; }
        }

        /// <summary>For a pattern: the text the '*' stands for in name, or null when it does not match.</summary>
        public string Match(string name)
        {
            int star = Name.IndexOf('*');
            string prefix = Name.Substring(0, star), suffix = Name.Substring(star + 1);
            if (name.Length < prefix.Length + suffix.Length) return null;
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;
            string rest = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
            return rest.Length > 0 ? rest : null;
        }
    }

    /// <summary>What the report says about one setting.</summary>
    public sealed class Described
    {
        public SettingItem Item;
        public string Group;
        public string Decoder;

        /// <summary>Sort key within the group: the settings.txt line, or after them all (file order).</summary>
        public int Order = int.MaxValue;

        /// <summary>The description (plain text), or null when there is none.</summary>
        public string Text;

        /// <summary>A second line, for custom check options: the other checks that use it.</summary>
        public string Note;

        /// <summary>Row id prefix: s = Settings.xml, l = language settings (.ldml), x = a row the report works out.</summary>
        public string IdPrefix = "s";

        /// <summary>Shown in the Setting column instead of the element name (rows the report works out).</summary>
        public string DisplayName;

        /// <summary>How often each item occurs in the text (CharacterUsage); null = not counted.</summary>
        public Dictionary<string, int> Counts;

        /// <summary>Highlight the name as a disapproved (invalid) list, also for worked-out rows.</summary>
        public bool Invalid;
    }

    /// <summary>
    /// The descriptions of Settings.xml elements: the embedded settings.txt, then the options
    /// of the custom checks (.cms files), then the patterns of settings.txt.
    /// </summary>
    public sealed class SettingCatalog
    {
        // Groups, in the order of the report.
        public const string General = "general";
        public const string Files = "files";
        public const string Books = "books";
        public const string Display = "display";
        public const string Language = "language";
        public const string Quotes = "quotes";
        public const string Chars = "chars";
        public const string Punct = "punct";
        public const string Caps = "caps";
        public const string Words = "words";
        public const string Numbers = "numbers";
        public const string Refs = "refs";
        public const string Markers = "markers";
        public const string Notes = "notes";
        public const string Terms = "terms";
        public const string StudyBible = "studybible";
        public const string Publishing = "publishing";
        public const string Cms = "cms";
        public const string CmsMessages = "cmsmsg";
        public const string Other = "other";

        /// <summary>Settings left out of the report at the user's request (2.4); not in GroupOrder.</summary>
        public const string Hidden = "hidden";

        public static readonly string[] GroupOrder =
        {
            General, Files, Books, Display, Language, Chars, Punct, Quotes, Caps, Markers, Words, Numbers, Refs,
            Notes, Terms, StudyBible, Publishing, Cms, CmsMessages, Other
        };

        /// <summary>
        /// Whether a group appears in the report. Custom-check options (1.5), check messages and Other
        /// (2.5) are left out: the user found them of no use to most readers. So is Hidden, and a group
        /// missing from GroupOrder (a typo in settings.txt), which would have gone under Other.
        /// </summary>
        public static bool IsShown(string group)
        {
            return group != Cms && group != CmsMessages && group != Other && Array.IndexOf(GroupOrder, group) >= 0;
        }

        private const string ResourceName = "ProjectSettings.settings.txt";

        private readonly Dictionary<string, SettingInfo> exact = new Dictionary<string, SettingInfo>(StringComparer.Ordinal);
        private readonly Dictionary<string, SettingInfo> exactIgnoreCase = new Dictionary<string, SettingInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SettingInfo> patterns = new List<SettingInfo>();
        private readonly Dictionary<string, CmsOption> cmsOptions;

        public SettingCatalog(Dictionary<string, CmsOption> cmsOptions)
        {
            this.cmsOptions = cmsOptions ?? new Dictionary<string, CmsOption>();
            foreach (SettingInfo info in Parse(LoadResource()))
            {
                if (info.IsPattern)
                {
                    patterns.Add(info);
                }
                else
                {
                    exact[info.Name] = info;
                    if (!exactIgnoreCase.ContainsKey(info.Name)) exactIgnoreCase[info.Name] = info;
                }
            }
        }

        public int CmsOptionCount
        {
            get { return cmsOptions.Count; }
        }

        private static string LoadResource()
        {
            using (Stream stream = typeof(SettingCatalog).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null) throw new InvalidOperationException("Missing resource " + ResourceName);
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        /// <summary>Lines of name|group|decoder|en|es|fr; '#' lines are comments; "\|" is a '|' in a description.</summary>
        internal static List<SettingInfo> Parse(string text)
        {
            var list = new List<SettingInfo>();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Trim().Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                List<string> fields = SplitFields(line);
                if (fields.Count != 6)
                    throw new InvalidDataException("settings.txt: expected 6 fields: " + line);
                var info = new SettingInfo();
                info.Name = fields[0].Trim();
                info.Group = fields[1].Trim();
                info.Decoder = fields[2].Trim();
                info.Text[(int)Lang.English] = fields[3].Trim();
                info.Text[(int)Lang.Spanish] = fields[4].Trim();
                info.Text[(int)Lang.French] = fields[5].Trim();
                info.Index = list.Count;
                list.Add(info);
            }
            return list;
        }

        private static List<string> SplitFields(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '\\' && i + 1 < line.Length && line[i + 1] == '|')
                {
                    current.Append('|');
                    i++;
                }
                else if (line[i] == '|')
                {
                    fields.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(line[i]);
                }
            }
            fields.Add(current.ToString());
            return fields;
        }

        /// <summary>The group, decoder and description of a setting, in the given language.</summary>
        public Described Describe(SettingItem item, Strings strings)
        {
            var d = new Described();
            d.Item = item;
            int lang = (int)strings.Language;

            SettingInfo info;
            if (exact.TryGetValue(item.Name, out info) || exactIgnoreCase.TryGetValue(item.Name, out info))
            {
                d.Group = info.Group;
                d.Decoder = info.Decoder;
                d.Order = info.Index;
                d.Text = Pick(info, lang);
                return d;
            }

            CmsOption option;
            if (cmsOptions.TryGetValue(item.Name, out option))
            {
                d.Group = Cms;
                d.Decoder = "text";
                string label = !string.IsNullOrEmpty(option.LocalizedName) ? option.LocalizedName : option.Name;
                string head = string.Format(strings.CmsOption, label, option.Checks[0]);
                d.Text = string.IsNullOrEmpty(option.Description) ? head : head + " " + option.Description;
                if (option.Checks.Count > 1)
                {
                    // Generic options (MatchCase...) are declared by dozens of checks: name a few.
                    const int MaxNamed = 5;
                    List<string> others = option.Checks.Skip(1).ToList();
                    string named = string.Join(", ", others.Take(MaxNamed).ToArray());
                    if (others.Count > MaxNamed) named += " (+" + (others.Count - MaxNamed) + ")";
                    d.Note = string.Format(strings.CmsOptionChecks, named);
                }
                return d;
            }

            foreach (SettingInfo pattern in patterns)
            {
                string rest = pattern.Match(item.Name);
                if (rest == null) continue;
                d.Group = pattern.Group;
                d.Decoder = pattern.Decoder;
                d.Order = pattern.Index;
                string what = pattern.Group == Publishing ? SplitWords(rest) : rest;
                d.Text = string.Format(Pick(pattern, lang), what);
                return d;
            }

            d.Group = Other;
            d.Decoder = "text";
            return d;
        }

        private static string Pick(SettingInfo info, int lang)
        {
            string text = info.Text[lang];
            return string.IsNullOrEmpty(text) ? info.Text[(int)Lang.English] : text;
        }

        /// <summary>Disapproved inventory lists whose names do not say "Invalid".</summary>
        private static readonly HashSet<string> OtherDisapprovedLists = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NonRepeatableWords", "UnmatchedPairingCharacters", "UnmatchedPairingCharacters_NonText"
        };

        /// <summary>
        /// Whether a setting lists disapproved (invalid) items, so its name is highlighted:
        /// InvalidCharacters, InvalidPunctuation... and the disapproved lists of the repeated words and
        /// matched pairs inventories. Check messages (Err_Invalid...) are not lists.
        /// </summary>
        public static bool IsDisapprovedList(string name)
        {
            if (string.IsNullOrEmpty(name) || name.StartsWith("Err_", StringComparison.OrdinalIgnoreCase)) return false;
            return name.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 || OtherDisapprovedLists.Contains(name);
        }

        /// <summary>"MarginTop" -> "Margin Top", for the Publishing Assistant settings.</summary>
        internal static string SplitWords(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(name[i - 1]) || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]))))
                    sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
