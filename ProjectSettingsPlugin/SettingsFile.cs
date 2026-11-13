using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ProjectSettings
{
    /// <summary>One element of Settings.xml.</summary>
    public sealed class SettingItem
    {
        public string Name;

        /// <summary>The element's text, exactly as stored (XML entities decoded).</summary>
        public string Value;

        /// <summary>The element's attributes, in file order (the Naming element has some).</summary>
        public readonly List<KeyValuePair<string, string>> Attributes = new List<KeyValuePair<string, string>>();

        /// <summary>
        /// Whether there is anything to show: text other than Paratext's "NullValue", or a non-empty
        /// attribute. A value that is only a space (BookChapterSeparator) counts.
        /// </summary>
        public bool HasValue
        {
            get
            {
                if (!string.IsNullOrEmpty(Value) && Value != "NullValue") return true;
                foreach (KeyValuePair<string, string> a in Attributes)
                    if (!string.IsNullOrEmpty(a.Value)) return true;
                return false;
            }
        }

        public string Attribute(string name)
        {
            foreach (KeyValuePair<string, string> a in Attributes)
                if (a.Key == name) return a.Value;
            return null;
        }
    }

    /// <summary>
    /// Reads a project's Settings.xml: the child elements of its root, in file order. Never writes.
    /// </summary>
    public static class SettingsFile
    {
        public static List<SettingItem> Read(string path)
        {
            var doc = new XmlDocument();
            doc.PreserveWhitespace = true;   // values such as ValidCharacters start with spaces
            doc.XmlResolver = null;
            var readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Ignore;
            readerSettings.XmlResolver = null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (XmlReader reader = XmlReader.Create(stream, readerSettings))
                doc.Load(reader);

            var items = new List<SettingItem>();
            if (doc.DocumentElement == null) return items;
            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                var element = node as XmlElement;
                if (element == null) continue;
                var item = new SettingItem();
                item.Name = element.Name;
                item.Value = HasChildElements(element) ? element.InnerXml : element.InnerText;
                foreach (XmlAttribute a in element.Attributes)
                    item.Attributes.Add(new KeyValuePair<string, string>(a.Name, a.Value));
                items.Add(item);
            }
            return items;
        }

        private static bool HasChildElements(XmlElement element)
        {
            foreach (XmlNode child in element.ChildNodes)
                if (child is XmlElement) return true;
            return false;
        }

        /// <summary>The value of the first element with that name, or null.</summary>
        public static string ValueOf(IEnumerable<SettingItem> items, string name)
        {
            foreach (SettingItem item in items)
                if (item.Name == name) return item.Value;
            return null;
        }
    }
}
