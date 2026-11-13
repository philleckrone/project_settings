using System;

namespace ProjectSettings
{
    public enum Lang
    {
        English,
        Spanish,
        French
    }

    /// <summary>
    /// The plugin's text in English, Spanish or French, picked from Paratext's interface language.
    /// Keep this file ASCII: run tools\asciify_cs.py after typing accented letters.
    /// </summary>
    public sealed class Strings
    {
        /// <summary>The tool's version: shown in the report and reported to Paratext.</summary>
        public const string ToolVersion = "2.5";

        public Lang Language;
        public string MenuText;
        public string Title;
        public string Description;
        public string OpenProjectFirst;
        public string NoProjectFolder;
        public string NoSettingsFile;
        public string Error;

        // The report
        public string ReportTitle;
        public string Intro;
        public string ReadOnlyNote;
        public string FileLabel;
        public string MadeBy;
        public string Contents;
        public string CountSettings;
        public string ShowEmpty;
        public string EmptyCount;
        public string InvalidLegend;
        public string ColDescription;
        public string ColSetting;
        public string ColValue;
        public string Empty;
        public string StoredAs;
        public string Yes;
        public string No;
        public string NoDescription;
        public string CmsOption;
        public string CmsOptionChecks;
        public string Attributes;

        // Value notes
        public string BooksCount;
        public string BooksNone;
        public string BooksMissing;
        public string BooksNoneMissing;
        public string PunctNote;
        public string PunctInitial;
        public string PunctMedial;
        public string PunctFinal;
        public string PunctIsolated;
        public string LangLanguage;
        public string LangScript;
        public string LangRegion;
        public string LangVariant;
        public string ProjType;
        public string ProjBasedOn;
        public string MatthewFile;
        public string FontMissing;
        public string Space;
        public string[] VersificationNames;

        // Group titles, in the order of the report
        public string GroupGeneral;
        public string GroupFiles;
        public string GroupBooks;
        public string GroupDisplay;
        public string GroupLanguage;
        public string EmptyLegend;
        public string NoLdml;
        public string NotInLanguageName;
        public string NotInLanguageDesc;
        public string NotInLanguageNone;
        public string UsedValidDesc;
        public string UnusedName;
        public string UnusedDesc;
        public string UnusedNone;
        public string InvalidFoundName;
        public string InvalidFoundDesc;
        public string InvalidFoundNone;
        public string BooksRead;
        public string NotInLanguageDescUsed;
        public string NotInLanguageNoneUsed;
        public string ProjectFileName;
        public string FilePresent;
        public string FileAbsent;
        public string CustomStyDesc;
        public string CustomVrsDesc;
        public string AutoCorrectDesc;
        public string PunctUsedDesc;
        public string PunctUnusedDesc;
        public string PunctUnusedNone;
        public string PunctFoundName;
        public string PunctFoundDesc;
        public string PunctFoundNone;
        public string LookAlikeLegend;
        public string QuoteLevel;
        public string QuoteNarrative;
        public string QuoteOpenClose;
        public string QuoteContinue;
        public string FontSize;
        public string FontFeatureList;
        public string GroupQuotes;
        public string GroupChars;
        public string GroupPunct;
        public string GroupCaps;
        public string GroupWords;
        public string GroupNumbers;
        public string GroupRefs;
        public string GroupMarkers;
        public string GroupNotes;
        public string GroupTerms;
        public string GroupStudyBible;
        public string GroupPublishing;
        public string GroupCms;
        public string GroupCmsMessages;
        public string GroupOther;

        public static Strings For(string locale)
        {
            switch (LanguageOf(locale))
            {
                case Lang.Spanish: return MakeSpanish();
                case Lang.French: return MakeFrench();
                default: return English();
            }
        }

        /// <summary>The interface language for a Paratext locale: "es..." Spanish, "fr..." French, anything else English.</summary>
        public static Lang LanguageOf(string locale)
        {
            string lang = string.IsNullOrEmpty(locale) ? "" : locale.ToLowerInvariant();
            if (lang.StartsWith("es", StringComparison.Ordinal)) return Lang.Spanish;
            if (lang.StartsWith("fr", StringComparison.Ordinal)) return Lang.French;
            return Lang.English;
        }

        /// <summary>The title of a group (keys as in settings.txt).</summary>
        public string GroupTitle(string group)
        {
            switch (group)
            {
                case SettingCatalog.General: return GroupGeneral;
                case SettingCatalog.Files: return GroupFiles;
                case SettingCatalog.Books: return GroupBooks;
                case SettingCatalog.Display: return GroupDisplay;
                case SettingCatalog.Language: return GroupLanguage;
                case SettingCatalog.Quotes: return GroupQuotes;
                case SettingCatalog.Chars: return GroupChars;
                case SettingCatalog.Punct: return GroupPunct;
                case SettingCatalog.Caps: return GroupCaps;
                case SettingCatalog.Words: return GroupWords;
                case SettingCatalog.Numbers: return GroupNumbers;
                case SettingCatalog.Refs: return GroupRefs;
                case SettingCatalog.Markers: return GroupMarkers;
                case SettingCatalog.Notes: return GroupNotes;
                case SettingCatalog.Terms: return GroupTerms;
                case SettingCatalog.StudyBible: return GroupStudyBible;
                case SettingCatalog.Publishing: return GroupPublishing;
                case SettingCatalog.Cms: return GroupCms;
                case SettingCatalog.CmsMessages: return GroupCmsMessages;
                default: return GroupOther;
            }
        }

        public static Strings English()
        {
            var s = new Strings();
            s.Language = Lang.English;
            s.MenuText = "Project Settings Report...";
            s.Title = "Project Settings Report";
            s.Description = "Shows the project's settings (Settings.xml) as a page in the web browser, with a description of each setting and the characters used in the text. It never changes the project.";
            s.OpenProjectFirst = "Open a project (not a resource) in a Scripture window first, then choose this command from that window's Tools menu.";
            s.NoProjectFolder = "The folder of project {0} could not be found.";
            s.NoSettingsFile = "There is no Settings.xml in {0}.";
            s.Error = "The report could not be made: {0}";

            s.ReportTitle = "Settings of {0}";
            s.Intro = "Each row gives the name of the setting in Settings.xml, its value, and a description. Where it helps, the value is also shown in a more readable form, with the stored text below it.";
            s.ReadOnlyNote = "This page only shows the settings. To change them, use Paratext (Project properties, Language settings, Quotation rules, or the inventories).";
            s.FileLabel = "File:";
            s.MadeBy = "Made by {0} {1} on {2}.";
            s.Contents = "Contents";
            s.CountSettings = "{0} settings with a value, {1} empty.";
            s.ShowEmpty = "Show empty settings ({0})";
            s.EmptyCount = "+{0} empty";
            s.InvalidLegend = "Setting names in orange list disapproved (invalid) items: characters, punctuation, markers or words that the checks report as errors.";
            s.ColDescription = "Description";
            s.ColSetting = "Setting";
            s.ColValue = "Value";
            s.Empty = "(empty)";
            s.StoredAs = "Stored as:";
            s.Yes = "Yes";
            s.No = "No";
            s.NoDescription = "No description is available for this setting.";
            s.CmsOption = "Option \"{0}\" of the custom check \"{1}\".";
            s.CmsOptionChecks = "Also used by: {0}.";
            s.Attributes = "Attributes:";

            s.BooksCount = "{0} books:";
            s.BooksNone = "No books.";
            s.BooksMissing = "Missing from GEN to REV ({0} books):";
            s.BooksNoneMissing = "None missing from GEN to REV: all 66 books are present.";
            s.PunctNote = "_ marks a space or the edge of a word: _( = at the start of a word, )_ = at the end of a word, _-_ = between spaces, - = inside a word.";
            s.PunctInitial = "word-initial";
            s.PunctMedial = "word-medial";
            s.PunctFinal = "word-final";
            s.PunctIsolated = "isolated";
            s.LangLanguage = "Language";
            s.LangScript = "Script";
            s.LangRegion = "Region";
            s.LangVariant = "Variant";
            s.ProjType = "Type";
            s.ProjBasedOn = "Based on";
            s.MatthewFile = "File of Matthew: {0}";
            s.FontMissing = "(this font is not installed on this computer)";
            s.Space = "space";
            s.VersificationNames = new[] { "Unknown", "Original", "Septuagint", "Vulgate", "English", "Russian Protestant", "Russian Orthodox" };

            s.GroupGeneral = "General";
            s.GroupFiles = "Files and encoding";
            s.GroupBooks = "Books and versification";
            s.GroupDisplay = "Fonts and display";
            s.GroupLanguage = "Language settings";
            s.EmptyLegend = "Setting names in yellow are empty (shown when \"Show empty settings\" is ticked).";
            s.NoLdml = "No language settings file named {0} was found in the project folder.";
            s.NotInLanguageName = "Valid characters not in the language settings";
            s.NotInLanguageDesc = "Letters (and letter combinations) approved in ValidCharacters that are not in the Alphabetic characters list of the language settings ({0}, sil:simple). A capitalized combination such as Ch or CH counts as listed when ch is listed.";
            s.NotInLanguageNone = "None: every letter in ValidCharacters is in the Alphabetic characters list.";
            s.UsedValidDesc = "Approved characters (character inventory, Language settings) that occur in the text of the {0} books read, with how often each occurs. Approved characters that are not used are listed below.";
            s.UnusedName = "Approved but not used in the text";
            s.UnusedDesc = "Approved characters (ValidCharacters) that do not occur in the text of the {0} books read. NOTE: this list includes characters that may have been approved in the past and are no longer a part of this project.";
            s.UnusedNone = "None: every approved character occurs in the text.";
            s.InvalidFoundName = "Disapproved characters found in the text";
            s.InvalidFoundDesc = "Characters of InvalidCharacters that still occur in the text of the {0} books read, with how often each occurs. The checks report them as errors.";
            s.InvalidFoundNone = "None: no disapproved character occurs in the text.";
            s.BooksRead = "The text of {0} books was read to count the characters.";
            s.NotInLanguageDescUsed = "Letters (and letter combinations) used in the text and approved in ValidCharacters that are not in the Alphabetic characters list of the language settings ({0}, sil:simple), with how often each occurs. A capitalized combination such as Ch or CH counts as listed when ch is listed.";
            s.NotInLanguageNoneUsed = "None: every letter used in the text is in the Alphabetic characters list.";
            s.ProjectFileName = "{0} file";
            s.FilePresent = "Yes: {0} bytes, last changed {1}";
            s.FileAbsent = "No: the project folder has no {0}.";
            s.CustomStyDesc = "Whether the project folder has a custom.sty file: changes to the stylesheet above (new markers or different formatting) for this project only. Paratext applies them on top of the stylesheet.";
            s.CustomVrsDesc = "Whether the project folder has a custom.vrs file: changes to the versification above (different verse counts, mappings, missing verses) for this project only.";
            s.AutoCorrectDesc = "Whether the project folder has an autocorrect.txt file: replacements Paratext makes automatically while you type in this project, one per line (text-->replacement).";
            s.PunctUsedDesc = "Approved punctuation (punctuation inventory, Language settings) that occurs in the text of the {0} books read, with how often each item occurs. Items are formed as Paratext forms them: _ marks a space or the edge of a word. Approved items that are not used are listed below.";
            s.PunctUnusedDesc = "Approved punctuation (ValidPunctuation) that does not occur in the text of the {0} books read. It may have been approved in the past and no longer be part of this project.";
            s.PunctUnusedNone = "None: every approved item occurs in the text.";
            s.PunctFoundName = "Disapproved punctuation found in the text";
            s.PunctFoundDesc = "Items of InvalidPunctuation that still occur in the text of the {0} books read, with how often each occurs. The checks report them as errors.";
            s.PunctFoundNone = "None: no disapproved punctuation occurs in the text.";
            s.LookAlikeLegend = "Characters that look alike show their Unicode value under them.";
            s.QuoteLevel = "Level {0}";
            s.QuoteNarrative = "narrative";
            s.QuoteOpenClose = "{0} \u2026 {1}";
            s.QuoteContinue = "continues with {0}";
            s.FontSize = "size {0}";
            s.FontFeatureList = "features {0}";
            s.GroupQuotes = "Quotation marks";
            s.GroupChars = "Characters";
            s.GroupPunct = "Punctuation";
            s.GroupCaps = "Capitalization";
            s.GroupWords = "Words and wordlist";
            s.GroupNumbers = "Numbers";
            s.GroupRefs = "Scripture references";
            s.GroupMarkers = "Markers";
            s.GroupNotes = "Footnotes and cross references";
            s.GroupTerms = "Biblical terms and interlinear";
            s.GroupStudyBible = "Study Bible";
            s.GroupPublishing = "Publishing Assistant";
            s.GroupCms = "Options of custom checks";
            s.GroupCmsMessages = "Messages of checks";
            s.GroupOther = "Other settings";
            return s;
        }

        private static Strings MakeSpanish()
        {
            var s = new Strings();
            s.Language = Lang.Spanish;
            s.MenuText = "Informe de la configuraci\u00F3n del proyecto...";
            s.Title = "Informe de la configuraci\u00F3n del proyecto";
            s.Description = "Muestra la configuraci\u00F3n del proyecto (Settings.xml) como una p\u00E1gina en el navegador web, con una descripci\u00F3n de cada valor y los caracteres usados en el texto. Nunca cambia el proyecto.";
            s.OpenProjectFirst = "Primero abra un proyecto (no un recurso) en una ventana de Escritura y luego elija este comando en el men\u00FA Herramientas de esa ventana.";
            s.NoProjectFolder = "No se encontr\u00F3 la carpeta del proyecto {0}.";
            s.NoSettingsFile = "No hay ning\u00FAn Settings.xml en {0}.";
            s.Error = "No se pudo hacer el informe: {0}";

            s.ReportTitle = "Configuraci\u00F3n de {0}";
            s.Intro = "Cada fila da el nombre del valor en Settings.xml, su contenido y una descripci\u00F3n. Cuando ayuda, el contenido se muestra tambi\u00E9n de forma m\u00E1s legible, con el texto guardado debajo.";
            s.ReadOnlyNote = "Esta p\u00E1gina solo muestra la configuraci\u00F3n. Para cambiarla, use Paratext (Propiedades del proyecto, Configuraci\u00F3n del idioma, Reglas de comillas o los inventarios).";
            s.FileLabel = "Archivo:";
            s.MadeBy = "Hecho por {0} {1} el {2}.";
            s.Contents = "Contenido";
            s.CountSettings = "{0} valores con contenido, {1} vac\u00EDos.";
            s.ShowEmpty = "Mostrar los valores vac\u00EDos ({0})";
            s.EmptyCount = "+{0} vac\u00EDos";
            s.InvalidLegend = "Los nombres en naranja contienen elementos no aprobados (no v\u00E1lidos): caracteres, puntuaci\u00F3n, marcadores o palabras que las verificaciones se\u00F1alan como errores.";
            s.ColDescription = "Descripci\u00F3n";
            s.ColSetting = "Nombre";
            s.ColValue = "Valor";
            s.Empty = "(vac\u00EDo)";
            s.StoredAs = "Guardado como:";
            s.Yes = "S\u00ED";
            s.No = "No";
            s.NoDescription = "No hay descripci\u00F3n para este valor.";
            s.CmsOption = "Opci\u00F3n \"{0}\" de la verificaci\u00F3n personalizada \"{1}\".";
            s.CmsOptionChecks = "Tambi\u00E9n la usa: {0}.";
            s.Attributes = "Atributos:";

            s.BooksCount = "{0} libros:";
            s.BooksNone = "Ning\u00FAn libro.";
            s.BooksMissing = "Faltan de GEN a REV ({0} libros):";
            s.BooksNoneMissing = "No falta ninguno de GEN a REV: est\u00E1n los 66 libros.";
            s.PunctNote = "_ indica un espacio o el borde de una palabra: _( = al principio de una palabra, )_ = al final de una palabra, _-_ = entre espacios, - = dentro de una palabra.";
            s.PunctInitial = "inicial";
            s.PunctMedial = "en medio";
            s.PunctFinal = "final";
            s.PunctIsolated = "aislado";
            s.LangLanguage = "Idioma";
            s.LangScript = "Escritura";
            s.LangRegion = "Regi\u00F3n";
            s.LangVariant = "Variante";
            s.ProjType = "Tipo";
            s.ProjBasedOn = "Basado en";
            s.MatthewFile = "Archivo de Mateo: {0}";
            s.FontMissing = "(esta fuente no est\u00E1 instalada en este equipo)";
            s.Space = "espacio";
            s.VersificationNames = new[] { "Desconocida", "Original", "Septuaginta", "Vulgata", "Inglesa", "Rusa protestante", "Rusa ortodoxa" };

            s.GroupGeneral = "General";
            s.GroupFiles = "Archivos y codificaci\u00F3n";
            s.GroupBooks = "Libros y versificaci\u00F3n";
            s.GroupDisplay = "Fuentes y visualizaci\u00F3n";
            s.GroupLanguage = "Configuraci\u00F3n del idioma";
            s.EmptyLegend = "Los nombres en amarillo est\u00E1n vac\u00EDos (se muestran al marcar \"Mostrar los valores vac\u00EDos\").";
            s.NoLdml = "No se encontr\u00F3 en la carpeta del proyecto ning\u00FAn archivo de configuraci\u00F3n del idioma llamado {0}.";
            s.NotInLanguageName = "Caracteres v\u00E1lidos que no est\u00E1n en la configuraci\u00F3n del idioma";
            s.NotInLanguageDesc = "Letras (y combinaciones de letras) aprobadas en ValidCharacters que no est\u00E1n en la lista de Caracteres alfab\u00E9ticos de la configuraci\u00F3n del idioma ({0}, sil:simple). Una combinaci\u00F3n con may\u00FAscula, como Ch o CH, cuenta como incluida cuando ch est\u00E1 en la lista.";
            s.NotInLanguageNone = "Ninguno: todas las letras de ValidCharacters est\u00E1n en la lista de Caracteres alfab\u00E9ticos.";
            s.UsedValidDesc = "Caracteres aprobados (inventario de caracteres, Configuraci\u00F3n del idioma) que aparecen en el texto de los {0} libros le\u00EDdos, con cu\u00E1ntas veces aparece cada uno. Los caracteres aprobados que no se usan se enumeran debajo.";
            s.UnusedName = "Aprobados pero no usados en el texto";
            s.UnusedDesc = "Caracteres aprobados (ValidCharacters) que no aparecen en el texto de los {0} libros le\u00EDdos. NOTA: esta lista incluye caracteres que pueden haberse aprobado en el pasado y que ya no forman parte de este proyecto.";
            s.UnusedNone = "Ninguno: todos los caracteres aprobados aparecen en el texto.";
            s.InvalidFoundName = "Caracteres no aprobados que aparecen en el texto";
            s.InvalidFoundDesc = "Caracteres de InvalidCharacters que todav\u00EDa aparecen en el texto de los {0} libros le\u00EDdos, con cu\u00E1ntas veces aparece cada uno. Las verificaciones los se\u00F1alan como errores.";
            s.InvalidFoundNone = "Ninguno: ning\u00FAn car\u00E1cter no aprobado aparece en el texto.";
            s.BooksRead = "Se ley\u00F3 el texto de {0} libros para contar los caracteres.";
            s.NotInLanguageDescUsed = "Letras (y combinaciones de letras) usadas en el texto y aprobadas en ValidCharacters que no est\u00E1n en la lista de Caracteres alfab\u00E9ticos de la configuraci\u00F3n del idioma ({0}, sil:simple), con cu\u00E1ntas veces aparece cada una. Una combinaci\u00F3n con may\u00FAscula, como Ch o CH, cuenta como incluida cuando ch est\u00E1 en la lista.";
            s.NotInLanguageNoneUsed = "Ninguno: todas las letras usadas en el texto est\u00E1n en la lista de Caracteres alfab\u00E9ticos.";
            s.ProjectFileName = "Archivo {0}";
            s.FilePresent = "S\u00ED: {0} bytes, \u00FAltimo cambio {1}";
            s.FileAbsent = "No: la carpeta del proyecto no tiene {0}.";
            s.CustomStyDesc = "Si la carpeta del proyecto tiene un archivo custom.sty: cambios a la hoja de estilo de arriba (marcadores nuevos o formato diferente) solo para este proyecto. Paratext los aplica sobre la hoja de estilo.";
            s.CustomVrsDesc = "Si la carpeta del proyecto tiene un archivo custom.vrs: cambios a la versificaci\u00F3n de arriba (otro n\u00FAmero de vers\u00EDculos, correspondencias, vers\u00EDculos que faltan) solo para este proyecto.";
            s.AutoCorrectDesc = "Si la carpeta del proyecto tiene un archivo autocorrect.txt: reemplazos que Paratext hace autom\u00E1ticamente mientras se escribe en este proyecto, uno por l\u00EDnea (texto-->reemplazo).";
            s.PunctUsedDesc = "Puntuaci\u00F3n aprobada (inventario de puntuaci\u00F3n, Configuraci\u00F3n del idioma) que aparece en el texto de los {0} libros le\u00EDdos, con cu\u00E1ntas veces aparece cada elemento. Los elementos se forman como los forma Paratext: _ indica un espacio o el borde de una palabra. Los elementos aprobados que no se usan se enumeran debajo.";
            s.PunctUnusedDesc = "Puntuaci\u00F3n aprobada (ValidPunctuation) que no aparece en el texto de los {0} libros le\u00EDdos. Puede que se aprobara en el pasado y ya no forme parte de este proyecto.";
            s.PunctUnusedNone = "Ninguno: todos los elementos aprobados aparecen en el texto.";
            s.PunctFoundName = "Puntuaci\u00F3n no aprobada que aparece en el texto";
            s.PunctFoundDesc = "Elementos de InvalidPunctuation que todav\u00EDa aparecen en el texto de los {0} libros le\u00EDdos, con cu\u00E1ntas veces aparece cada uno. Las verificaciones los se\u00F1alan como errores.";
            s.PunctFoundNone = "Ninguno: ninguna puntuaci\u00F3n no aprobada aparece en el texto.";
            s.LookAlikeLegend = "Los caracteres que se parecen muestran su valor Unicode debajo.";
            s.QuoteLevel = "Nivel {0}";
            s.QuoteNarrative = "narrativa";
            s.QuoteOpenClose = "{0} \u2026 {1}";
            s.QuoteContinue = "contin\u00FAa con {0}";
            s.FontSize = "tama\u00F1o {0}";
            s.FontFeatureList = "caracter\u00EDsticas {0}";
            s.GroupQuotes = "Comillas";
            s.GroupChars = "Caracteres";
            s.GroupPunct = "Puntuaci\u00F3n";
            s.GroupCaps = "May\u00FAsculas";
            s.GroupWords = "Palabras y lista de palabras";
            s.GroupNumbers = "N\u00FAmeros";
            s.GroupRefs = "Referencias b\u00EDblicas";
            s.GroupMarkers = "Marcadores";
            s.GroupNotes = "Notas al pie y referencias cruzadas";
            s.GroupTerms = "Vocablos b\u00EDblicos e interlineal";
            s.GroupStudyBible = "Biblia de estudio";
            s.GroupPublishing = "Publishing Assistant";
            s.GroupCms = "Opciones de verificaciones personalizadas";
            s.GroupCmsMessages = "Mensajes de verificaciones";
            s.GroupOther = "Otros valores";
            return s;
        }

        private static Strings MakeFrench()
        {
            var s = new Strings();
            s.Language = Lang.French;
            s.MenuText = "Rapport des param\u00E8tres du projet...";
            s.Title = "Rapport des param\u00E8tres du projet";
            s.Description = "Affiche les param\u00E8tres du projet (Settings.xml) dans une page du navigateur web, avec une description de chaque param\u00E8tre et les caract\u00E8res utilis\u00E9s dans le texte. Il ne modifie jamais le projet.";
            s.OpenProjectFirst = "Ouvrez d'abord un projet (pas une ressource) dans une fen\u00EAtre de texte biblique, puis choisissez cette commande dans le menu Outils de cette fen\u00EAtre.";
            s.NoProjectFolder = "Le dossier du projet {0} est introuvable.";
            s.NoSettingsFile = "Il n'y a pas de Settings.xml dans {0}.";
            s.Error = "Le rapport n'a pas pu \u00EAtre fait : {0}";

            s.ReportTitle = "Param\u00E8tres de {0}";
            s.Intro = "Chaque ligne donne le nom du param\u00E8tre dans Settings.xml, sa valeur et une description. Quand c'est utile, la valeur est aussi pr\u00E9sent\u00E9e sous une forme plus lisible, avec le texte enregistr\u00E9 en dessous.";
            s.ReadOnlyNote = "Cette page ne fait qu'afficher les param\u00E8tres. Pour les modifier, utilisez Paratext (Propri\u00E9t\u00E9s du projet, Param\u00E8tres de langue, R\u00E8gles des guillemets ou les inventaires).";
            s.FileLabel = "Fichier :";
            s.MadeBy = "Fait par {0} {1} le {2}.";
            s.Contents = "Sommaire";
            s.CountSettings = "{0} param\u00E8tres avec une valeur, {1} vides.";
            s.ShowEmpty = "Afficher les param\u00E8tres vides ({0})";
            s.EmptyCount = "+{0} vides";
            s.InvalidLegend = "Les noms en orange contiennent des \u00E9l\u00E9ments non approuv\u00E9s (non valides) : caract\u00E8res, ponctuation, marqueurs ou mots que les v\u00E9rifications signalent comme des erreurs.";
            s.ColDescription = "Description";
            s.ColSetting = "Param\u00E8tre";
            s.ColValue = "Valeur";
            s.Empty = "(vide)";
            s.StoredAs = "Enregistr\u00E9 sous la forme :";
            s.Yes = "Oui";
            s.No = "Non";
            s.NoDescription = "Aucune description n'est disponible pour ce param\u00E8tre.";
            s.CmsOption = "Option \u00AB {0} \u00BB de la v\u00E9rification personnalis\u00E9e \u00AB {1} \u00BB.";
            s.CmsOptionChecks = "Aussi utilis\u00E9e par : {0}.";
            s.Attributes = "Attributs :";

            s.BooksCount = "{0} livres :";
            s.BooksNone = "Aucun livre.";
            s.BooksMissing = "Manquants de GEN \u00E0 REV ({0} livres) :";
            s.BooksNoneMissing = "Aucun manquant de GEN \u00E0 REV : les 66 livres sont pr\u00E9sents.";
            s.PunctNote = "_ marque une espace ou le bord d'un mot : _( = au d\u00E9but d'un mot, )_ = \u00E0 la fin d'un mot, _-_ = entre des espaces, - = \u00E0 l'int\u00E9rieur d'un mot.";
            s.PunctInitial = "initial";
            s.PunctMedial = "m\u00E9dian";
            s.PunctFinal = "final";
            s.PunctIsolated = "isol\u00E9";
            s.LangLanguage = "Langue";
            s.LangScript = "\u00C9criture";
            s.LangRegion = "R\u00E9gion";
            s.LangVariant = "Variante";
            s.ProjType = "Type";
            s.ProjBasedOn = "Bas\u00E9 sur";
            s.MatthewFile = "Fichier de Matthieu : {0}";
            s.FontMissing = "(cette police n'est pas install\u00E9e sur cet ordinateur)";
            s.Space = "espace";
            s.VersificationNames = new[] { "Inconnue", "Originale", "Septante", "Vulgate", "Anglaise", "Russe protestante", "Russe orthodoxe" };

            s.GroupGeneral = "G\u00E9n\u00E9ral";
            s.GroupFiles = "Fichiers et encodage";
            s.GroupBooks = "Livres et versification";
            s.GroupDisplay = "Polices et affichage";
            s.GroupLanguage = "Param\u00E8tres de langue";
            s.EmptyLegend = "Les noms en jaune sont vides (affich\u00E9s quand \u00AB Afficher les param\u00E8tres vides \u00BB est coch\u00E9).";
            s.NoLdml = "Aucun fichier de param\u00E8tres de langue nomm\u00E9 {0} n'a \u00E9t\u00E9 trouv\u00E9 dans le dossier du projet.";
            s.NotInLanguageName = "Caract\u00E8res valides absents des param\u00E8tres de langue";
            s.NotInLanguageDesc = "Lettres (et combinaisons de lettres) approuv\u00E9es dans ValidCharacters qui ne figurent pas dans la liste des Caract\u00E8res alphab\u00E9tiques des param\u00E8tres de langue ({0}, sil:simple). Une combinaison avec majuscule, comme Ch ou CH, compte comme pr\u00E9sente quand ch figure dans la liste.";
            s.NotInLanguageNone = "Aucun : toutes les lettres de ValidCharacters figurent dans la liste des Caract\u00E8res alphab\u00E9tiques.";
            s.UsedValidDesc = "Caract\u00E8res approuv\u00E9s (inventaire de caract\u00E8res, Param\u00E8tres de langue) qui apparaissent dans le texte des {0} livres lus, avec le nombre d'occurrences de chacun. Les caract\u00E8res approuv\u00E9s qui ne sont pas utilis\u00E9s sont list\u00E9s en dessous.";
            s.UnusedName = "Approuv\u00E9s mais non utilis\u00E9s dans le texte";
            s.UnusedDesc = "Caract\u00E8res approuv\u00E9s (ValidCharacters) qui n'apparaissent pas dans le texte des {0} livres lus. REMARQUE : cette liste comprend des caract\u00E8res qui ont pu \u00EAtre approuv\u00E9s par le pass\u00E9 et qui ne font plus partie de ce projet.";
            s.UnusedNone = "Aucun : tous les caract\u00E8res approuv\u00E9s apparaissent dans le texte.";
            s.InvalidFoundName = "Caract\u00E8res non approuv\u00E9s pr\u00E9sents dans le texte";
            s.InvalidFoundDesc = "Caract\u00E8res de InvalidCharacters qui apparaissent encore dans le texte des {0} livres lus, avec le nombre d'occurrences de chacun. Les v\u00E9rifications les signalent comme des erreurs.";
            s.InvalidFoundNone = "Aucun : aucun caract\u00E8re non approuv\u00E9 n'appara\u00EEt dans le texte.";
            s.BooksRead = "Le texte de {0} livres a \u00E9t\u00E9 lu pour compter les caract\u00E8res.";
            s.NotInLanguageDescUsed = "Lettres (et combinaisons de lettres) utilis\u00E9es dans le texte et approuv\u00E9es dans ValidCharacters qui ne figurent pas dans la liste des Caract\u00E8res alphab\u00E9tiques des param\u00E8tres de langue ({0}, sil:simple), avec le nombre d'occurrences de chacune. Une combinaison avec majuscule, comme Ch ou CH, compte comme pr\u00E9sente quand ch figure dans la liste.";
            s.NotInLanguageNoneUsed = "Aucun : toutes les lettres utilis\u00E9es dans le texte figurent dans la liste des Caract\u00E8res alphab\u00E9tiques.";
            s.ProjectFileName = "Fichier {0}";
            s.FilePresent = "Oui : {0} octets, derni\u00E8re modification le {1}";
            s.FileAbsent = "Non : le dossier du projet n'a pas de {0}.";
            s.CustomStyDesc = "Si le dossier du projet contient un fichier custom.sty : des changements \u00E0 la feuille de style ci-dessus (nouveaux marqueurs ou mise en forme diff\u00E9rente) pour ce projet seulement. Paratext les applique par-dessus la feuille de style.";
            s.CustomVrsDesc = "Si le dossier du projet contient un fichier custom.vrs : des changements \u00E0 la versification ci-dessus (autre nombre de versets, correspondances, versets manquants) pour ce projet seulement.";
            s.AutoCorrectDesc = "Si le dossier du projet contient un fichier autocorrect.txt : des remplacements que Paratext fait automatiquement pendant la saisie dans ce projet, un par ligne (texte-->remplacement).";
            s.PunctUsedDesc = "Ponctuation approuv\u00E9e (inventaire de ponctuation, Param\u00E8tres de langue) qui appara\u00EEt dans le texte des {0} livres lus, avec le nombre d'occurrences de chaque \u00E9l\u00E9ment. Les \u00E9l\u00E9ments sont form\u00E9s comme Paratext les forme : _ marque une espace ou le bord d'un mot. Les \u00E9l\u00E9ments approuv\u00E9s qui ne sont pas utilis\u00E9s sont list\u00E9s en dessous.";
            s.PunctUnusedDesc = "Ponctuation approuv\u00E9e (ValidPunctuation) qui n'appara\u00EEt pas dans le texte des {0} livres lus. Elle a pu \u00EAtre approuv\u00E9e par le pass\u00E9 et ne plus faire partie de ce projet.";
            s.PunctUnusedNone = "Aucun : tous les \u00E9l\u00E9ments approuv\u00E9s apparaissent dans le texte.";
            s.PunctFoundName = "Ponctuation non approuv\u00E9e pr\u00E9sente dans le texte";
            s.PunctFoundDesc = "\u00C9l\u00E9ments de InvalidPunctuation qui apparaissent encore dans le texte des {0} livres lus, avec le nombre d'occurrences de chacun. Les v\u00E9rifications les signalent comme des erreurs.";
            s.PunctFoundNone = "Aucun : aucune ponctuation non approuv\u00E9e n'appara\u00EEt dans le texte.";
            s.LookAlikeLegend = "Les caract\u00E8res qui se ressemblent affichent leur valeur Unicode en dessous.";
            s.QuoteLevel = "Niveau {0}";
            s.QuoteNarrative = "narrative";
            s.QuoteOpenClose = "{0} \u2026 {1}";
            s.QuoteContinue = "continue avec {0}";
            s.FontSize = "taille {0}";
            s.FontFeatureList = "fonctionnalit\u00E9s {0}";
            s.GroupQuotes = "Guillemets";
            s.GroupChars = "Caract\u00E8res";
            s.GroupPunct = "Ponctuation";
            s.GroupCaps = "Majuscules";
            s.GroupWords = "Mots et liste de mots";
            s.GroupNumbers = "Nombres";
            s.GroupRefs = "R\u00E9f\u00E9rences bibliques";
            s.GroupMarkers = "Marqueurs";
            s.GroupNotes = "Notes de bas de page et r\u00E9f\u00E9rences crois\u00E9es";
            s.GroupTerms = "Termes bibliques et interlin\u00E9aire";
            s.GroupStudyBible = "Bible d'\u00E9tude";
            s.GroupPublishing = "Publishing Assistant";
            s.GroupCms = "Options des v\u00E9rifications personnalis\u00E9es";
            s.GroupCmsMessages = "Messages des v\u00E9rifications";
            s.GroupOther = "Autres param\u00E8tres";
            return s;
        }
    }
}
