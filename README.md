# Project Settings Report

*This page is the English user guide, [ProjectSettings_UserGuide.rtf](plugin/ProjectSettings_UserGuide.rtf). The guide is also available in Spanish ([ProjectSettings_UserGuide_sp.rtf](plugin/ProjectSettings_UserGuide_sp.rtf)) and French ([ProjectSettings_UserGuide_fr.rtf](plugin/ProjectSettings_UserGuide_fr.rtf)).*

## Purpose

Project Settings Report is a Paratext plugin. It shows the project's settings file (Settings.xml) as a page in your web browser. For each setting it gives its value and a description of what it is. It reads the project's files and the text of its books, but it never changes the project.

The settings are grouped by topic: General, Files and encoding, Books and versification, Fonts and display, Language settings, Characters, Punctuation, Quotation marks, Capitalization, Markers, Words and wordlist, Numbers, Scripture references, Footnotes and cross references, Biblical terms and interlinear, Study Bible, and Publishing Assistant. Messages saved by custom checks and settings with no known description are not shown.

## Installing

1. Download Install_Phils_tools.exe from the Paratext_Plugins folder. If your browser warns about the file, choose to keep it.

2. Close Paratext.

3. Double-click Install_Phils_tools.exe. If Windows says it protected your PC, click "More info", then "Run anyway" (the installer is not signed yet).

4. Tick Project Settings Report and click "Install / update ticked". It installs into every Paratext 9 on the computer. If Windows asks for permission, click Yes. The same window updates the plugin later (its status then says "Update available") or removes it ("Remove ticked"), and its "User guide" button opens this guide.

## Using it

1. Open the project in a Scripture window and choose Tools > Project Settings Report...

2. The report opens in your web browser. It starts with the project's name, the path of its Settings.xml, and a table of contents. Click a topic to go to it.

3. Each row has three columns: the name of the setting in Settings.xml, its value, and its description.

The page is saved as local\ProjectSettings\SettingsReport.html in the project folder (Send/Receive does not share the local folder). Each new report replaces the previous one. You can print it or save it from the browser.

## How values are shown

- Yes/No settings (stored as T/F or True/False) show Yes or No, with the stored text below.
- Under StyleSheet, the row "custom.sty file" says whether the project folder has a custom.sty (the project's own changes to the stylesheet), with its size and date. Under Versification, "custom.vrs file" does the same for custom.vrs, and at the end of Files and encoding, "autocorrect.txt file" for autocorrect.txt.
- Book lists (BooksPresent and others, one 0 or 1 per book) show the book codes and how many there are. For BooksPresent, the books from GEN to REV that the project does not have are listed below, in red.
- Versification, encoding, language code and project type are spelled out (for example 4 = English, 65001 = UTF-8).
- Lists (characters, punctuation, markers, words) show one box per item. Invisible characters and special spaces appear as small yellow labels with their code, for example ZWJ U+200D or NBSP U+00A0.
- Characters that are hard to tell apart show their Unicode value under them in small blue letters: apostrophes and glottal stops (' ʼ ꞌ ’), quotation marks, hyphens and dashes (- – —), colons, combining marks, and Cyrillic or Greek letters that look like Latin ones (х, о, ε). For example ꞌ shows U+A78C and ' shows U+0027.
- In punctuation lists, \_ marks a space or the edge of a word: \_( = at the start of a word, )\_ = at the end, \_-\_ = between spaces, - = inside a word. Point at an item to see its place.
- At first only settings that have a value are listed. Tick "Show empty settings" above the contents to also list the empty ones, including check options that Paratext stores as NullValue; they show (empty), with the setting name in light yellow. The top of the page says how many there are.
- The names of settings that list disapproved (invalid) items, such as InvalidCharacters, InvalidPunctuation or NonRepeatableWords, have a light orange background.

## Characters used in the text

The report reads the text of every book in the project the way Paratext's checks read it (publishable text and notes, not the markers, note callers or verse numbers) and counts how often each character and each punctuation item occurs. The top of the page says how many books were read. In the Characters section:

- ValidCharacters shows only the approved characters that occur in the text, each with a small number: how many times it occurs.
- "Approved but not used in the text", just below, lists the approved characters that do not occur. They may have been approved in the past and no longer be part of the project.
- "Disapproved characters found in the text", under InvalidCharacters (orange), lists disapproved characters that still occur, with their counts. These are errors to correct in the text.

A letter combination such as ch is counted wherever it occurs, and a single letter is counted everywhere, also inside combinations (the e of ie). A combining mark on its own (for example a macron below) counts only where it is not on a letter. Opening the report can take a few seconds while the books are read.

In the Punctuation section the same is done for ValidPunctuation and InvalidPunctuation: only the approved items used in the text, with counts, then "Approved but not used in the text", and under InvalidPunctuation (orange) "Disapproved punctuation found in the text". The items are formed exactly as in Paratext's punctuation inventory, single marks with what is around them (\_( .\_) or whole sequences (!»\_() when the inventory's advanced view is on.

## Language settings

The Language settings section shows the project's language settings (Project > Project settings > Language settings...). Paratext keeps them in a .ldml file in the project folder, named after the language code in Settings.xml (LanguageIsoCode): for example rub.ldml for rub:::, or es-x-taller.ldml for es:::x-taller. A project folder may hold several .ldml files; the report uses the one that matches the code, shows its name beside the section title, and lists the others. If none matches, the section says so.

It shows the letters of the language, the Alphabetic characters list in alphabetical order, punctuation, the Other characters tab (word medial punctuation, footnote and cross reference caller sequences, verse segment characters, diacritics, word break characters), matched pairs, quotation marks, writing direction, fonts and keyboards. Each character is shown in a box.

Under ValidCharacters, in the Characters section, the row "Valid characters not in the language settings" lists the letters (and letter combinations such as ch or kʼ) used in the text that the character inventory approves but that are missing from the Alphabetic characters list of the language settings. A capitalized combination such as Ch or CH counts as listed when ch is listed. If nothing is missing, the row says so. The row appears only when the project has both lists.

## Where the descriptions come from

Paratext's own settings are described by the plugin, using the names of the Paratext windows where they are set (Project properties, Language settings, Quotation rules, Number settings, Scripture reference settings, and the inventories).

Custom checks (CMS) save their options in Settings.xml too. The report leaves them out: it recognizes them from the .cms files in the cms folder of your Paratext projects folder. An option of a check that is no longer in the cms folder cannot be recognized, so it is not shown either.

## Changing settings

The report is read-only. To change a setting, use Paratext: Project > Project settings > Project properties..., Language settings..., Quotation rules..., Number settings... or Scripture reference settings..., or the checking inventories.
