using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ProjectSettings;

namespace ProjectSettingsTests
{
    /// <summary>
    /// The book texts of a project folder, for the tests (the plugin reads them through Paratext).
    /// Book files are named FileNamePrePart...FileNamePostPart (e.g. 01GENPDT15.SFM) and read in the
    /// Settings.xml Encoding, as in Numbered Book Titles' FolderBookSource. A file counts only when
    /// it starts with \id and its name holds that book code, which skips strays such as
    /// RegExBackup1.sfm; *.bak never matches the post part.
    /// </summary>
    internal static class FolderBooks
    {
        public static List<string> Files(string folder)
        {
            string settings = Path.Combine(folder, "Settings.xml");
            string pre = "", post = ".SFM";
            if (File.Exists(settings))
            {
                string xml = File.ReadAllText(settings);
                Match mp = Regex.Match(xml, @"<FileNamePrePart>([^<]*)</FileNamePrePart>");
                Match mq = Regex.Match(xml, @"<FileNamePostPart>([^<]*)</FileNamePostPart>");
                if (mp.Success) pre = mp.Groups[1].Value;
                if (mq.Success && mq.Groups[1].Value.Length > 0) post = mq.Groups[1].Value;
            }
            var byBook = new SortedDictionary<int, string>();
            foreach (string path in Directory.GetFiles(folder))
            {
                string fn = Path.GetFileName(path);
                if (!fn.StartsWith(pre, StringComparison.OrdinalIgnoreCase) || !fn.EndsWith(post, StringComparison.OrdinalIgnoreCase)) continue;
                string first = File.ReadLines(path).FirstOrDefault() ?? "";
                Match m = Regex.Match(first, @"^\uFEFF?\\id\s+(\w{3})");
                if (!m.Success) continue;
                string code = m.Groups[1].Value.ToUpperInvariant();
                int index = Array.IndexOf(ValueFormatter.BookCodes, code);
                if (index < 0 || Path.GetFileNameWithoutExtension(path).ToUpperInvariant().IndexOf(code, StringComparison.Ordinal) < 0) continue;
                byBook[index + 1] = path;
            }
            return byBook.Values.ToList();
        }

        /// <summary>The text of each book file, read lazily in the project's encoding.</summary>
        public static IEnumerable<string> Texts(string folder)
        {
            Encoding encoding = new UTF8Encoding(false);
            string settings = Path.Combine(folder, "Settings.xml");
            if (File.Exists(settings))
            {
                Match enc = Regex.Match(File.ReadAllText(settings), @"<Encoding>(\d+)</Encoding>");
                if (enc.Success && enc.Groups[1].Value != "65001")
                    encoding = Encoding.GetEncoding(int.Parse(enc.Groups[1].Value));
            }
            foreach (string path in Files(folder))
                yield return File.ReadAllText(path, encoding);
        }
    }
}
