using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ProjectSettings
{
    /// <summary>
    /// The project's stylesheet merged field by field with its custom.sty, keyed by marker, field
    /// names lowercased. Copied from Marker Spaces' StyleSheets, with two changes: the base file is
    /// the project's own &lt;StyleSheet&gt; (a bare file name), and #!\ lines (USFM 3 entries in
    /// usfm.sty) are active, as Paratext reads them. Only StyleType and TextProperties are used.
    /// </summary>
    public sealed class StyleSheets
    {
        public readonly Dictionary<string, Dictionary<string, string>> Markers =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        public Dictionary<string, string> Get(string marker)
        {
            Dictionary<string, string> f;
            return marker != null && Markers.TryGetValue(marker, out f) ? f : null;
        }

        /// <summary>"paragraph", "character", "note", "milestone"... or null for an unknown marker.</summary>
        public string StyleType(string marker)
        {
            Dictionary<string, string> f = Get(marker);
            string type;
            return f != null && f.TryGetValue("styletype", out type) ? type.Trim().ToLowerInvariant() : null;
        }

        public bool IsNonpublishable(string marker)
        {
            Dictionary<string, string> f = Get(marker);
            string props;
            return f != null && f.TryGetValue("textproperties", out props)
                && (" " + props.ToLowerInvariant() + " ").Contains(" nonpublishable ");
        }

        /// <summary>
        /// The base stylesheet: projectsDir\styleSheetName (a bare file name from Settings.xml),
        /// otherwise projectsDir\usfm.sty, otherwise the built-in usfm.sty (3.0.11); then the
        /// project's custom.sty on top.
        /// </summary>
        public static StyleSheets Load(string projectDirectory, string styleSheetName)
        {
            var s = new StyleSheets();
            var usfm = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            string parent = projectDirectory != null ? Path.GetDirectoryName(projectDirectory.TrimEnd('\\', '/')) : null;
            if (parent != null)
            {
                if (IsPlainFileName(styleSheetName))
                {
                    usfm = Parse(Path.Combine(projectDirectory, styleSheetName));
                    if (usfm.Count == 0) usfm = Parse(Path.Combine(parent, styleSheetName));
                }
                if (usfm.Count == 0) usfm = Parse(Path.Combine(parent, "usfm.sty"));
            }
            if (usfm.Count == 0)
            {
                using (Stream st = typeof(StyleSheets).Assembly.GetManifestResourceStream("ProjectSettings.usfm.sty"))
                using (var ms = new MemoryStream())
                {
                    if (st != null)
                    {
                        st.CopyTo(ms);
                        usfm = ParseText(Decode(ms.ToArray()));
                    }
                }
            }
            var custom = projectDirectory != null ? Parse(Path.Combine(projectDirectory, "custom.sty"))
                : new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

            foreach (var kv in usfm)
                s.Markers[kv.Key] = new Dictionary<string, string>(kv.Value, StringComparer.Ordinal);
            foreach (var kv in custom)
            {
                Dictionary<string, string> target;
                if (!s.Markers.TryGetValue(kv.Key, out target))
                {
                    target = new Dictionary<string, string>(StringComparer.Ordinal);
                    s.Markers[kv.Key] = target;
                }
                foreach (var f in kv.Value) target[f.Key] = f.Value;
            }
            return s;
        }

        /// <summary>A file name only (no folder, no drive), as Custom Stylesheet Editor accepts it.</summary>
        private static bool IsPlainFileName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.IndexOfAny(new[] { '\\', '/', ':' }) < 0
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        /// <summary>An empty result when the file is missing or unreadable.</summary>
        public static Dictionary<string, Dictionary<string, string>> Parse(string path)
        {
            var entries = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return entries;
            byte[] data;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    data = ms.ToArray();
                }
            }
            catch (Exception)
            {
                return entries;
            }
            return ParseText(Decode(data));
        }

        /// <summary>UTF-8 (with or without BOM), falling back to the ANSI code page.</summary>
        private static string Decode(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(data, start, data.Length - start);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(data);
            }
        }

        /// <summary>
        /// A \Marker line opens an entry; any other \Field value line sets that field on the current
        /// entry (the last value wins). #!\ lines count as active; other # lines, blank lines and
        /// lines not starting with a backslash are skipped. A # after white space starts a comment.
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> ParseText(string text)
        {
            var entries = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            Dictionary<string, string> current = null;
            foreach (string raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#!", StringComparison.Ordinal)) line = line.Substring(2).Trim();
                if (line.Length == 0 || line.StartsWith("#") || !line.StartsWith("\\")) continue;
                for (int i = 1; i < line.Length; i++)
                    if (line[i] == '#' && char.IsWhiteSpace(line[i - 1])) { line = line.Substring(0, i).Trim(); break; }
                string rest = line.Substring(1);
                int ws = 0;
                while (ws < rest.Length && !char.IsWhiteSpace(rest[ws])) ws++;
                string field = rest.Substring(0, ws);
                if (field.Length == 0) continue;
                string value = rest.Substring(ws).Trim();
                if (field.Equals("Marker", StringComparison.OrdinalIgnoreCase))
                {
                    if (!entries.TryGetValue(value, out current))
                    {
                        current = new Dictionary<string, string>(StringComparer.Ordinal);
                        entries[value] = current;
                    }
                }
                else if (current != null)
                {
                    current[field.ToLowerInvariant()] = value;
                }
            }
            return entries;
        }
    }
}
