using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProjectSettings
{
    /// <summary>
    /// Characters that are hard to tell apart on screen, whose Unicode value the report shows
    /// under them (the user's choice: look-alike groups only, not every character).
    /// </summary>
    public static class LookAlikes
    {
        private static readonly HashSet<int> Set = new HashSet<int>(new[]
        {
            // Apostrophes, glottal stops and accents used as letters
            0x0027, 0x02BC, 0xA78C, 0xA78B, 0x2019, 0x2018, 0x201B, 0x02BB, 0x02BD, 0x02B9, 0x00B4, 0x0060,
            0x2032, 0x02C8, 0x02CA, 0x02CB, 0x02BE, 0x02BF,
            // Quotation marks
            0x0022, 0x201C, 0x201D, 0x201E, 0x201F, 0x2033, 0x02DD, 0x02BA, 0x2039, 0x203A, 0x201A,
            // Hyphens and dashes
            0x002D, 0x2010, 0x2011, 0x2012, 0x2013, 0x2014, 0x2015, 0x2212, 0x2043, 0x02D7, 0x005F, 0x203E,
            // Colons and dots
            0x003A, 0xA789, 0x02D0, 0x2236, 0x02F8, 0x00B7, 0x2022, 0x2027, 0x2219, 0x22C5, 0x2024,
            // Bars
            0x01C0, 0x007C, 0x2223,
            // Spacing accents
            0x005E, 0x02C6, 0x007E, 0x02DC, 0x00A8,
            // Cyrillic letters that look Latin: a v e k m n o r s t u x dze i je shha palochka komi-de qa we ue
            0x0430, 0x0432, 0x0435, 0x043A, 0x043C, 0x043D, 0x043E, 0x0440, 0x0441, 0x0442, 0x0443, 0x0445,
            0x0455, 0x0456, 0x0458, 0x04BB, 0x04CF, 0x0501, 0x051B, 0x051D, 0x04AF,
            0x0410, 0x0412, 0x0415, 0x041A, 0x041C, 0x041D, 0x041E, 0x0420, 0x0421, 0x0422, 0x0423, 0x0425,
            0x0405, 0x0406, 0x0408, 0x04BA, 0x04C0, 0x0500, 0x051A, 0x051C, 0x04AE,
            // Greek letters that look Latin
            0x03B1, 0x03B2, 0x03B5, 0x03B9, 0x03BA, 0x03BD, 0x03BF, 0x03C1, 0x03C4, 0x03C5, 0x03C7, 0x03F2,
            0x0391, 0x0392, 0x0395, 0x0396, 0x0397, 0x0399, 0x039A, 0x039C, 0x039D, 0x039F, 0x03A1, 0x03A4,
            0x03A5, 0x03A7,
            // IPA letters close to Latin or Greek ones: open e, open o, alpha, script g, dotless i, iota, esh, ezh
            0x025B, 0x0254, 0x0251, 0x0261, 0x0131, 0x0269, 0x0283, 0x0292
        });

        /// <summary>Whether a code point is a look-alike; every combining mark is one.</summary>
        public static bool Is(int codePoint)
        {
            if (Set.Contains(codePoint)) return true;
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
            return cat == UnicodeCategory.NonSpacingMark || cat == UnicodeCategory.SpacingCombiningMark || cat == UnicodeCategory.EnclosingMark;
        }

        /// <summary>"U+A78C" for each look-alike in the text (in order, without repeats), or "" for none.</summary>
        public static string Codes(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var seen = new HashSet<int>();
            var sb = new StringBuilder();
            foreach (int cp in PunctuationRules.CodePoints(text))
            {
                if (!Is(cp) || !seen.Add(cp)) continue;
                if (sb.Length > 0) sb.Append(' ');
                sb.Append("U+").Append(cp.ToString("X4", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
