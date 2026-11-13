<#
.SYNOPSIS
  Makes a narrated demo video of Project Settings Report (MP4, 1920x1080), following
  tools\demo_video\Demo_Video_Standard.md.
  1. Writes the report of a copy of PDT15 with the plugin's test host (the same ReportWriter the
     plugin runs), then renders parts of it in a headless Edge (or Chrome). The plugin has no
     window of its own: Paratext opens the report in the default browser. The boxes come from the
     page itself (CSS selectors, read back through --dump-dom), so no mouse or window is used.
  2. Draws each frame: a headline, the page with orange boxes round the parts named and a
     pointer, and the narration as subtitles.
  3. Speaks the narration (Azure AI Speech, see azure_tts.ps1) and joins the steps with ffmpeg.
  The text is in narration.json (UTF-8). This file stays ASCII.

.EXAMPLE
  .\make_demo.ps1 -Lang en -NoVoice
#>
param(
    [string]$Lang = 'en',
    [string]$Work = '',
    [string]$Project = 'C:\ClaudeCode Projects\TestProject\PDT15',
    # The project's folder as the report shows it ("File: ..."), where a user's project would be.
    [string]$ShownProject = 'C:\My Paratext 8 Projects\PDT15',
    # The plugin's test host (it writes the same report as the plugin).
    [string]$TestHost = '',
    # A Chromium browser for the pictures; by default Edge, else Chrome.
    [string]$Browser = '',
    # The browser's page size in CSS pixels, and its scale (sharper text in the video).
    [int]$PageWidth = 1260,
    [int]$PageHeight = 620,
    [double]$PageScale = 1.5,
    # An Azure voice (a name ending in 'Neural', e.g. en-US-JennyNeural; key and region from
    # AZURE_SPEECH_KEY / AZURE_SPEECH_REGION, see azure_tts.ps1) or a Windows voice (System.Speech).
    [string]$Voice = '',
    [int]$Rate = -1,                 # Windows voices: -10..10
    [string]$AzureRate = '0%',       # Azure voices: e.g. '-5%'
    # ffmpeg.exe to use; by default a newer GPL build in C:\ffmpeg-*, else the one on PATH.
    [string]$FFmpeg = '',
    # Use the pictures of the last capture in $Work (the report is not made again).
    [switch]$Reuse,
    # Only make the pictures (then build with -Reuse).
    [switch]$CaptureOnly,
    # Zoom into the boxed parts in steps without cues (written as ..._zoom.mp4).
    [switch]$Zoom,
    # No narration: each step stays up as long as its subtitle takes to read (for testing).
    [switch]$NoVoice,
    # Background music (looped, kept soft; lowered further while the voice speaks). '' = none.
    [string]$Music = 'C:\Users\Pleckrone\Dropbox\Camtasia work\Background Music Files\Aetheric - Coconut Kind of Love (freetouse.com).mp3',
    [double]$MusicVolume = 0.30,        # the music on its own, at the start (the standard)
    [double]$MusicUnderVoice = 0.025,    # from the narration on (the standard)
    [double]$CoverIntro = 3.5,          # seconds of music alone on the cover before the narration
    # The cover's second line, under the plugin's name (narration.json, step "title").
    [string]$Series = 'Plugins by Phil',
    # Dissolve between steps, and dip to black round the slides (seconds). The report has no
    # count line, so steps always dissolve (the standard, section 6); $Wipe is kept for that rule.
    [double]$Dissolve = 1.0,
    [double]$Wipe = 2.0,
    [double]$Dip = 1.4
)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
if (-not $TestHost) { $TestHost = Join-Path (Split-Path $here) 'tests\TestHost\bin\Release\ProjectSettingsTestHost.exe' }
if (-not $Work) { $Work = Join-Path $env:TEMP "projsettings_demo_$Lang" }
if (-not $Voice) { $Voice = @{ 'en' = 'en-US-JennyNeural'; 'es' = 'es-MX-JorgeNeural'; 'fr' = 'fr-FR-RemyMultilingualNeural' }[$Lang] }   # the standard's voices
$azure = $Voice -match 'Neural$'
$musicOn = $Music -and (Test-Path -LiteralPath $Music)
if ($Music -and -not $musicOn) { "music file not found, none used: $Music" }
$es = $Lang -like 'es*'; $isFr = $Lang -like 'fr*'
# The slides' text in the video's language: English, Spanish, French.
function T($en, $sp, $frText) { if ($es) { $sp } elseif ($isFr -and $null -ne $frText) { $frText } else { $en } }

$narr = Get-Content -LiteralPath (Join-Path $here 'narration.json') -Raw -Encoding UTF8 | ConvertFrom-Json
function Text($id) {
    $t = ($narr.steps | Where-Object { $_.id -eq $id } | Select-Object -First 1).$Lang
    if (-not $t) { throw "narration.json has no '$Lang' text for step $id" }
    $t
}
# Box cues in the narration (the standard, section 8). "{1}" before a phrase shows box 1 (the
# step's boxes, in order) from there on, "{0,2}" boxes 0 and 2, "{}" none; the pointer goes on the
# first box named. Before the first cue no box is shown. A text without cues shows all its boxes
# the whole time. Cues are left out of the subtitles and the voice.
$cueRx = '\{\s*(\d+(\s*,\s*\d+)*)?\s*\}'
function Clean($raw) { if ($raw) { [regex]::Replace($raw, $cueRx, '') } else { $raw } }
# The parts of a step: @{ Pos (in the clean text); Show (box numbers) }, or none without cues.
function Cues($raw) {
    $clean = ''; $marks = @(); $last = 0
    if (-not $raw) { return , @() }
    foreach ($m in [regex]::Matches($raw, $cueRx)) {
        $clean += $raw.Substring($last, $m.Index - $last)
        $show = @()
        if ($m.Groups[1].Success) { $show = @($m.Groups[1].Value -split '\s*,\s*' | ForEach-Object { [int]$_ }) }
        $marks += [pscustomobject]@{ Pos = $clean.Length; Show = $show }
        $last = $m.Index + $m.Length
    }
    if ($marks.Count -and $marks[0].Pos -gt 0) { $marks = @([pscustomobject]@{ Pos = 0; Show = @() }) + $marks }
    , $marks
}

Add-Type -AssemblyName System.Drawing, System.Speech

# ------------------------------------------------------------ the report
$shots = Join-Path $Work 'shots'
$frames = Join-Path $Work 'frames'
$stepsFile = Join-Path $Work 'steps.xml'
$script:steps = @()
if ($Reuse) {
    if (-not (Test-Path -LiteralPath $stepsFile)) { throw "-Reuse: no capture in $Work (run once without -Reuse)" }
    $script:steps = @(Import-Clixml -LiteralPath $stepsFile)
    if (Test-Path $frames) { Remove-Item -Recurse -Force $frames }
    New-Item -ItemType Directory -Force $frames | Out-Null
    Get-ChildItem -LiteralPath $Work -File | Where-Object { $_.Name -ne 'steps.xml' } | Remove-Item -Force
    "reusing the capture of $((Get-Item -LiteralPath $stepsFile).LastWriteTime): $(($script:steps | ForEach-Object { $_.Id }) -join ' ')"
}
else {
if (Test-Path $Work) { Remove-Item -Recurse -Force $Work }
New-Item -ItemType Directory -Force $shots, $frames | Out-Null
if (-not $Browser) {
    $Browser = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Browser) { throw 'no Edge or Chrome found (-Browser)' }
"browser: $Browser"
# The report, in the video's language, as the plugin writes it (all books read).
$report = Join-Path $Work 'SettingsReport.html'
& $TestHost report $Project $report $Lang | Out-Host
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'the test host did not write the report' }
# The "File:" line shows where a user's project would be, not this test copy.
$reportHtml = [IO.File]::ReadAllText($report).Replace($Project, $ShownProject)

# One picture of the report: the part from the element $target on ($offset CSS pixels above it)
# moved to the top of the page, "Show empty settings" ticked when $empty. $groups: a list of
# CSS selector lists, one orange box each (the union of the matching elements on the page).
# A transform moves the page, because a headless screenshot does not paint a scrolled page.
function Render($id, $target, $offset, $empty, $groups) {
    $gj = @()
    foreach ($grp in $groups) { $gj += '[' + ((@($grp) | ForEach-Object { ConvertTo-Json ([string]$_) -Compress }) -join ',') + ']' }
    $cfg = '{"w":' + $PageWidth + ',"h":' + $PageHeight + ',"target":' + (ConvertTo-Json ([string]$target) -Compress) + ',"offset":' + [int]$offset + ',"empty":' + $(if ($empty) { 'true' } else { 'false' }) + ',"groups":[' + ($gj -join ',') + ']}'
    $js = @"
<script>
(function(){
var cfg=$cfg;
function go(){
 // The page's size given, not the window's: the --dump-dom window is shorter than the
 // screenshot's, and the boxes must fit the picture.
 document.documentElement.style.width=cfg.w+'px';
 var c=document.getElementById('showEmpty'); if(c) c.checked=cfg.empty;
 var pg=document.querySelector('.page'); pg.style.transform='';
 if(cfg.target){var el=document.querySelector(cfg.target); if(!el) throw 'no '+cfg.target; pg.style.transform='translateY('+(-(el.getBoundingClientRect().top-cfg.offset))+'px)';}
 var W=cfg.w, H=cfg.h, d=window.devicePixelRatio, out=[];
 cfg.groups.forEach(function(g){
  var x1=1e9,y1=1e9,x2=-1e9,y2=-1e9;
  // "selector@n": only the n-th match (from 0).
  g.forEach(function(sel){ var at=-1, m=/^(.*)@(\d+)$/.exec(sel); if(m){sel=m[1]; at=+m[2];}
   var els=Array.prototype.slice.call(document.querySelectorAll(sel)); if(at>=0) els=els.slice(at,at+1);
   els.forEach(function(e){
   var r=e.getBoundingClientRect(); if(r.width<=0||r.height<=0) return;
   x1=Math.min(x1,r.left); y1=Math.min(y1,r.top); x2=Math.max(x2,r.right); y2=Math.max(y2,r.bottom); }); });
  x1=Math.max(x1,0); y1=Math.max(y1,0); x2=Math.min(x2,W); y2=Math.min(y2,H);
  out.push(x2>x1&&y2>y1 ? [Math.round(x1*d),Math.round(y1*d),Math.round((x2-x1)*d),Math.round((y2-y1)*d)] : null);
 });
 var p=document.getElementById('demoBoxes'); if(!p){p=document.createElement('pre');p.id='demoBoxes';p.style.display='none';document.body.appendChild(p);}
 p.textContent=JSON.stringify(out);
}
go(); window.addEventListener('load',go);
})();
</script>
"@
    $page = Join-Path $Work "$id.html"
    [IO.File]::WriteAllText($page, $reportHtml.Replace('</body>', $js + '</body>'), (New-Object Text.UTF8Encoding $false))
    $url = 'file:///' + ($page -replace '\\', '/')
    $file = Join-Path $shots "$id.png"
    # Light colours (the report follows the system's dark mode otherwise).
    $common = @('--headless=new', '--disable-gpu', '--hide-scrollbars', "--force-device-scale-factor=$PageScale",
        "--window-size=$PageWidth,$PageHeight", "--user-data-dir=$Work\browser", '--force-color-profile=srgb',
        '--blink-settings=preferredColorScheme=1')
    Start-Process $Browser -ArgumentList ($common + @("--screenshot=$file", $url)) -Wait -WindowStyle Hidden
    if (-not (Test-Path -LiteralPath $file)) { throw "step $id : no picture" }
    $dom = Join-Path $Work "$id.dom.txt"
    Start-Process $Browser -ArgumentList ($common + @('--dump-dom', $url)) -Wait -NoNewWindow -RedirectStandardOutput $dom -RedirectStandardError (Join-Path $Work "$id.err.txt")
    $m = [regex]::Match([IO.File]::ReadAllText($dom), '<pre id="demoBoxes"[^>]*>(.*?)</pre>')
    if (-not $m.Success) { throw "step $id : the page gave no boxes" }
    $boxes = @()
    $k = 0
    # (Windows PowerShell gives a JSON array as one object: assigned first, it is enumerated.)
    $parsed = ConvertFrom-Json $m.Groups[1].Value
    foreach ($b in $parsed) {
        if ($null -eq $b) { throw "step $id : box $k has no element on the page" }
        $boxes += , @([int]$b[0], [int]$b[1], [int]$b[2], [int]$b[3]); $k++
    }
    , $boxes
}
# $pointer: index of the box to point at, or -1.
function Step($id, $target, $offset, $empty, $groups, $pointer = -1) {
    $boxes = Render $id $target $offset $empty $groups
    # Rows = -1: no count line, so the joins between steps are always dissolves.
    $script:steps += [pscustomobject]@{ Id = $id; Shot = (Join-Path $shots "$id.png"); Boxes = $boxes; Pointer = [int]$pointer; Dialog = $null; Rows = -1; PointAt = $null }
    "captured $id : $(($boxes | ForEach-Object { '(' + ($_ -join ',') + ')' }) -join ' ')"
}

# ------------------------------------------------------------ the demo
# The top: project, file and books read; the read-only note; the contents.
Step 's01_report' '' 0 $false @(@('header h1', 'header p.full', 'header p.full + p.meta'), @('p.readonly'), @('nav'))
# A group: name, value, description; a value shown in a readable form with the stored text.
Step 's02_rows' '#general' 8 $false @(
    @('#general th.name', '#general td.name'), @('#general th.val', '#general td.val'), @('#general th.desc', '#general td.desc'),
    @('#s-LanguageIsoCode td.val'))
# "Show empty settings" ticked: the box and the "+n empty" counts of the contents.
Step 's03_empty' 'input#showEmpty' 24 $true @(@('input#showEmpty', 'label.opt'), @('nav ul'))
# Empty settings in yellow.
Step 's04_yellow' '#files' 8 $true @(@('#files tr.isempty td.name@0'), @('#files tr.isempty td.name@1'))
# The language settings (.ldml).
Step 's05_language' '#language' 8 $false @(@('[id="l-ldml"]'), @('[id="l-exemplarCharacters"]', '[id="l-exemplarCharacters[punctuation]"]'))
# The characters used in the text, with counts; approved but unused.
Step 's06_chars' '#s-ValidCharacters' 4 $false @(@('#s-ValidCharacters td.val'), @('#x-ValidCharacters-unused td.val'))
# Disapproved characters still used; look-alike characters with their codes.
Step 's07_invalid' '#x-ValidCharacters-unused' 8 $false @(@('#x-InvalidCharacters-found'), @('#x-InvalidCharacters-found .chip:has(.cp)@0'), @('#x-InvalidCharacters-found .chip:has(.cp)@1'))
# Worked out from the project folder: books present, custom.vrs.
Step 's08_books' '#books' 8 $false @(@('#s-BooksPresent td.val'), @('[id="x-file-custom.vrs"]'))
# Quotation marks: the marks of each level, then the continuers.
Step 's08b_quotes' '#quotes' 8 $false @(@('#s-Quotes', '#s-InnerQuotes', '#s-InnerInnerQuotes'), @('#s-Continuer', '#s-InnerContinuer', '#s-InnerInnerContinuer'))
# The other groups, in the contents (each group fills a picture of its own): the inventories
# (capitalization, markers, words), numbers, Scripture references.
Step 's08c_more' 'nav' 40 $false @(
    @('nav li:has(a[href="#caps"])', 'nav li:has(a[href="#markers"])'), @('nav li:has(a[href="#words"])'),
    @('nav li:has(a[href="#numbers"])'), @('nav li:has(a[href="#refs"])'))

$script:steps | Export-Clixml -LiteralPath $stepsFile
}
if ($CaptureOnly) { "captured in ${Work}; build the video with -Reuse"; return }

# The Paratext Tools menu (tools_menu_<lang>.png), before the report. The boxes are in each
# picture's pixels: the Tools heading and the plugin's menu item. tools_menu_en.png (1291 x 925)
# is the user's screenshot of 2026-11-06; a new screenshot needs new numbers.
$menuBoxes = @{
    'en' = @(@(1037, 101, 54, 24), @(1037, 504, 182, 24))
}
$menuShot = Join-Path $here "tools_menu_$Lang.png"
if ((Test-Path -LiteralPath $menuShot) -and $menuBoxes.ContainsKey($Lang)) {
    $mb = @(); foreach ($b in $menuBoxes[$Lang]) { $mb += , $b }
    $menuStep = [pscustomobject]@{ Id = 's00_menu'; Shot = $menuShot; Boxes = $mb; Pointer = 1; Dialog = $null; Rows = -1 }
    $script:steps = @($menuStep) + $script:steps
}
else { "no $menuShot (or no boxes for it): the menu step is left out" }

# Where the report is saved: the user's screenshot of 2026-11-06 (local_folder_<lang>.png, else the
# English one; 1079 x 713), two Explorer windows: the project's local folder with ProjectSettings
# selected, and that folder with SettingsReport.html. Boxes: the upper path, the ProjectSettings
# row, the lower path, the file.
$localShot = Join-Path $here "local_folder_$Lang.png"
if (-not (Test-Path -LiteralPath $localShot)) { $localShot = Join-Path $here 'local_folder_en.png' }
if (Test-Path -LiteralPath $localShot) {
    $lb = @(@(378, 68, 374, 34), @(98, 252, 746, 30), @(378, 393, 316, 34), @(98, 541, 166, 28))
    $script:steps += [pscustomobject]@{ Id = 's09_saved'; Shot = $localShot; Boxes = $lb; Pointer = -1; Dialog = $null; Rows = -1 }
}
else { "no $localShot : the step about the saved file is left out" }

# Getting and installing the tool: the user's screenshots of 2026-11-06, showing ProjectSettings.zip
# (the standard, section 9, with this plugin's own set: no separate Download picture, and the
# Windows warning and the installer's window added). Each is demo\<File>_<lang>.png, or the English
# one when there is none for the language. Boxes are in that picture's pixels (x, y, width,
# height); Pointer = the box pointed at.
# Getting and installing the tool: the shared install section (Install_Phils_tools.exe, see
# tools\demo_video\install\InstallSection.ps1), then this plugin's Tools menu again.
. (Join-Path (Split-Path (Split-Path $here)) 'tools\demo_video\install\InstallSection.ps1')
$script:steps += Get-InstallSteps -Plugin 'ProjectSettings' -Lang $Lang -Name (Text 'title').title
$installSteps = @(
    @{ Id = 'i7_ready';    File = 'tools_menu';        Boxes = 'menu'; Pointer = 1 }
)
foreach ($is in $installSteps) {
    $shot = Join-Path $here ("{0}_{1}.png" -f $is.File, $Lang)
    if (-not (Test-Path -LiteralPath $shot)) { $shot = Join-Path $here ("{0}_en.png" -f $is.File) }
    if (-not (Test-Path -LiteralPath $shot)) { "no $shot : step $($is.Id) is left out"; continue }
    $shotLang = if ($shot -like "*_$Lang.png") { $Lang } else { 'en' }
    $boxList = $is.Boxes
    if ($boxList -is [hashtable]) { $boxList = $boxList[$shotLang] }
    elseif ($boxList -eq 'menu') { $boxList = $menuBoxes[$shotLang] }
    $bx = @(); foreach ($b in $boxList) { $bx += , $b }
    $script:steps += [pscustomobject]@{ Id = $is.Id; Shot = $shot; Boxes = $bx; Pointer = $is.Pointer; Dialog = $null; Rows = -1 }
}

# ------------------------------------------------------------ frames
$W = 1920; $H = 1080; $top = 96; $bottomBand = 190
# The "Sky" design (the user's choice): deep blue cover, light-to-mid blue steps, gold accents.
function Hex($h) { [Drawing.ColorTranslator]::FromHtml($h) }
$navy = Hex '#1F3A5F'          # headline bar
$orange = [Drawing.Color]::FromArgb(255, 140, 0)   # boxes round the controls
$gold = Hex '#F2B544'          # accents
$band = Hex '#18222E'          # subtitle band
$bgTop = Hex '#EEF7FF'; $bgBottom = Hex '#94BEE4'          # steps: the gradient
$coverTop = Hex '#2C5A9A'; $coverBottom = Hex '#0B1830'    # cover and closing slide
function Gradient($g, $x, $y, $w, $h, $c1, $c2) {
    $br = New-Object Drawing.Drawing2D.LinearGradientBrush (New-Object Drawing.RectangleF $x, ($y - 1), $w, ($h + 2)), $c1, $c2, 90.0
    $g.FillRectangle($br, [float]$x, [float]$y, [float]$w, [float]$h)
    $br.Dispose()
}
function RoundRect($x, $y, $w, $h, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc([float]$x, [float]$y, $d, $d, 180, 90); $p.AddArc([float]($x + $w - $d), [float]$y, $d, $d, 270, 90)
    $p.AddArc([float]($x + $w - $d), [float]($y + $h - $d), $d, $d, 0, 90); $p.AddArc([float]$x, [float]($y + $h - $d), $d, $d, 90, 90)
    $p.CloseFigure(); $p
}
function NewFrame { $f = New-Object Drawing.Bitmap $W, $H; $g = [Drawing.Graphics]::FromImage($f); $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'; @{ F = $f; G = $g } }
function Centered($g, $text, $font, $brush, $rect) {
    $sf = New-Object Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString($text, $font, $brush, $rect, $sf)
}
# A font of the given size, or smaller until the text fits on one line in $maxWidth pixels
# (long names, e.g. "Edicion de notas en lote", were cut off on the cover).
function FitFont($g, $text, $family, $size, $maxWidth) {
    $f = New-Object Drawing.Font $family, $size
    while ($size -gt 36 -and $g.MeasureString($text, $f).Width -gt $maxWidth) {
        $f.Dispose(); $size -= 2; $f = New-Object Drawing.Font $family, $size
    }
    $f
}
function Card($id, $file) {
    $t = Text $id
    $fr = NewFrame; $g = $fr.G
    Gradient $g 0 0 $W $H $coverTop $coverBottom
    $light = New-Object Drawing.SolidBrush $gold
    $grey = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(190, 205, 225))
    if ($id -eq 'title') {
        # The cover: a framed panel (gold border) with a tag on its top edge; the plugin's name
        # large and white, a gold rule, then the series smaller (the user's order).
        $px = 360; $py = 290; $pw = $W - 720; $ph = 470
        $panel = RoundRect $px $py $pw $ph 28
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))), $panel)
        $g.DrawPath((New-Object Drawing.Pen $gold, 4), $panel)
        $tagFont = New-Object Drawing.Font 'Segoe UI Semibold', 20
        $tagText = T 'PARATEXT PLUGIN' 'COMPLEMENTO DE PARATEXT' 'MODULE PARATEXT'
        $tw = $g.MeasureString($tagText, $tagFont).Width + 44; $th = 48; $tx = ($W - $tw) / 2; $ty = $py - $th / 2
        $g.FillPath((New-Object Drawing.SolidBrush $gold), (RoundRect $tx $ty $tw $th 24))
        Centered $g $tagText $tagFont (New-Object Drawing.SolidBrush $coverBottom) (New-Object Drawing.RectangleF $tx, $ty, $tw, $th)
        Centered $g $t.title (FitFont $g $t.title 'Segoe UI Semibold' 80 ($pw - 60)) ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF $px, ($py + 60), $pw, 170)
        $g.FillRectangle($light, [float]($W / 2 - 160), [float]($py + 250), 320, 5)
        Centered $g $Series (New-Object Drawing.Font 'Segoe UI', 50) $light (New-Object Drawing.RectangleF $px, ($py + 280), $pw, 120)
    }
    elseif ($id -like 'sec_*') {
        # A section slide (the user's "subtitle" slide): the cover's panel, its title large and
        # white, a gold rule, then the plugin's name smaller.
        $px = 360; $py = 290; $pw = $W - 720; $ph = 470
        $panel = RoundRect $px $py $pw $ph 28
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(40, 255, 255, 255))), $panel)
        $g.DrawPath((New-Object Drawing.Pen $gold, 4), $panel)
        Centered $g $t.title (FitFont $g $t.title 'Segoe UI Semibold' 80 ($pw - 60)) ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF $px, ($py + 60), $pw, 170)
        $g.FillRectangle($light, [float]($W / 2 - 160), [float]($py + 250), 320, 5)
        # The plugin's name, smaller when it would not fit on one line (the French name was cut).
        $name = (Text 'title').title
        Centered $g $name (FitFont $g $name 'Segoe UI' 50 ($pw - 60)) $light (New-Object Drawing.RectangleF $px, ($py + 280), $pw, 120)
    }
    else {
        $f1 = New-Object Drawing.Font 'Segoe UI Semibold', 72
        $f2 = New-Object Drawing.Font 'Segoe UI', 34
        # Smaller when a long name would not fit on one line (the French name of this tool).
        $f1 = FitFont $g $t.title 'Segoe UI Semibold' 72 ($W - 200)
        Centered $g $t.title $f1 ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF 80, 300, ($W - 160), 160)
        $g.FillRectangle($light, [float]($W / 2 - 160), 462, 320, 5)
        # The key points on one line, or each on its own line when they don't fit (a wrap fell in
        # the middle of a point in Spanish and French).
        $sub = $t.sub
        $subH = 120
        if ($g.MeasureString($sub, $f2).Width -gt ($W - 160)) { $sub = ($sub -split ('\s+' + [char]0xB7 + '\s+')) -join "`n"; $subH = 200 }
        Centered $g $sub $f2 $light (New-Object Drawing.RectangleF 80, 480, ($W - 160), $subH)
        # Credits, in small print.
        $credits = @(T 'Created with Claude AI' 'Creado con Claude AI' "Cr$([char]0xE9)$([char]0xE9) avec Claude AI")
        if (-not $NoVoice -and $azure) { $credits += T 'Narration: AI-generated voice (Microsoft Azure AI Speech)' "Narraci$([char]0xF3)n: voz generada por IA (Microsoft Azure AI Speech)" "Narration : voix g$([char]0xE9)n$([char]0xE9)r$([char]0xE9)e par IA (Microsoft Azure AI Speech)" }
        if ($musicOn) { $credits += T 'Music: Coconut Kind of Love by Aetheric (freetouse.com)' "M$([char]0xFA)sica: Coconut Kind of Love de Aetheric (freetouse.com)" 'Musique : Coconut Kind of Love par Aetheric (freetouse.com)' }
        $f3 = New-Object Drawing.Font 'Segoe UI', 22
        $y = $H - 70 - 42 * $credits.Count
        foreach ($c in $credits) { Centered $g $c $f3 $grey (New-Object Drawing.RectangleF 80, $y, ($W - 160), 42); $y += 42 }
    }
    $g.Dispose(); $fr.F.Save($file, [Drawing.Imaging.ImageFormat]::Png); $fr.F.Dispose()
}
function Pointer($g, $x, $y) {
    # A mouse arrow with its tip at (x, y).
    $s = 1.6
    $pts = @(@(0, 0), @(0, 25), @(6, 19), @(11, 30), @(15, 28), @(10, 18), @(18, 18)) | ForEach-Object { New-Object Drawing.PointF ($x + $_[0] * $s), ($y + $_[1] * $s) }
    $g.FillPolygon([Drawing.Brushes]::White, [Drawing.PointF[]]$pts)
    $pen = New-Object Drawing.Pen ([Drawing.Color]::Black), 2
    $g.DrawPolygon($pen, [Drawing.PointF[]]$pts)
}
# The window area of a frame: the band between the headline and the subtitles, less margins.
$areaX = 40; $areaY = $top + 22; $areaW = $W - 80; $areaH = $H - $top - $bottomBand - 44
# The view of the whole window: the shot widened (or heightened) to the area's shape.
function FullView($iw, $ih) {
    $ar = $areaW / $areaH
    if ($iw / $ih -lt $ar) { $w0 = $ih * $ar; , @((($iw - $w0) / 2), 0, $w0, $ih) }
    else { $h0 = $iw / $ar; , @(0, (($ih - $h0) / 2), $iw, $h0) }
}
# The close-up of a step: the boxes (and the dialog) with a margin, in the area's shape, at most
# twice the size of the full view; $null when that would hardly be closer than the full view.
function ZoomView($st, $iw, $ih) {
    $rects = @($st.Boxes)
    if ($st.Dialog) { $rects += , $st.Dialog }
    if ($rects.Count -eq 0) { return $null }
    $x1 = 1e9; $y1 = 1e9; $x2 = -1e9; $y2 = -1e9
    foreach ($r in $rects) {
        $x1 = [Math]::Min($x1, $r[0]); $y1 = [Math]::Min($y1, $r[1])
        $x2 = [Math]::Max($x2, $r[0] + $r[2]); $y2 = [Math]::Max($y2, $r[1] + $r[3])
    }
    $pad = 0.05 * $iw
    $x1 -= $pad; $y1 -= $pad; $x2 += $pad; $y2 += $pad
    $ar = $areaW / $areaH
    $w = $x2 - $x1; $h = $y2 - $y1
    if ($w / $h -lt $ar) { $w = $h * $ar } else { $h = $w / $ar }
    $full = FullView $iw $ih
    $maxZoom = 2.0
    if ($full[2] / $w -gt $maxZoom) { $w = $full[2] / $maxZoom; $h = $w / $ar }
    if ($full[2] / $w -lt 1.15) { return $null }
    $cx = ($x1 + $x2) / 2; $cy = ($y1 + $y2) / 2
    $x = $cx - $w / 2; $y = $cy - $h / 2
    if ($w -le $iw) { $x = [Math]::Max(0, [Math]::Min($x, $iw - $w)) } else { $x = ($iw - $w) / 2 }
    if ($h -le $ih) { $y = [Math]::Max(0, [Math]::Min($y, $ih - $h)) } else { $y = ($ih - $h) / 2 }
    , @($x, $y, $w, $h)
}
# Between two views, at s (0..1): the centre moves in a straight line, the size changes
# geometrically, so the zoom feels even.
function MixView($a, $b, $s) {
    $w = $a[2] * [Math]::Pow($b[2] / $a[2], $s); $h = $a[3] * [Math]::Pow($b[3] / $a[3], $s)
    $cx = ($a[0] + $a[2] / 2) * (1 - $s) + ($b[0] + $b[2] / 2) * $s
    $cy = ($a[1] + $a[3] / 2) * (1 - $s) + ($b[1] + $b[3] / 2) * $s
    , @(($cx - $w / 2), ($cy - $h / 2), $w, $h)
}
# $part: one part of a step with cues (its Show = the boxes shown); $null = all boxes.
function StepFrame($st, $num, $total, $file, $view = $null, $part = $null) {
    $t = Text $st.Id
    $fr = NewFrame; $g = $fr.G
    Gradient $g 0 $top $W ($H - $top - $bottomBand) $bgTop $bgBottom
    # headline, with a gold line under it
    $g.FillRectangle((New-Object Drawing.SolidBrush $navy), 0, 0, $W, $top)
    $g.FillRectangle((New-Object Drawing.SolidBrush $gold), 0, ($top - 5), $W, 5)
    $fh = New-Object Drawing.Font 'Segoe UI Semibold', 30
    $g.DrawString($t.title, $fh, [Drawing.Brushes]::White, 40, 22)
    $fn = New-Object Drawing.Font 'Segoe UI', 20
    $sf = New-Object Drawing.StringFormat; $sf.Alignment = 'Far'
    $g.DrawString("$num / $total", $fn, (New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(190, 205, 225))), (New-Object Drawing.RectangleF 0, 34, ($W - 40), 40), $sf)
    # the window: the view (a rectangle of the shot) fills the area; float coordinates, so a
    # zoom moves smoothly. Whatever falls outside the band between headline and subtitles is cut.
    $img = [Drawing.Image]::FromFile($st.Shot)
    if (-not $view) { $view = FullView $img.Width $img.Height }
    $sc = [Math]::Min($areaW / $view[2], $areaH / $view[3])
    $ox = $areaX + ($areaW - $view[2] * $sc) / 2 - $view[0] * $sc
    $oy = $areaY + ($areaH - $view[3] * $sc) / 2 - $view[1] * $sc
    $iw = $img.Width * $sc; $ih = $img.Height * $sc
    $g.SetClip((New-Object Drawing.RectangleF 0, $top, $W, ($H - $top - $bottomBand)))
    # a soft shadow (spread, faint layers) and a white frame, so the grey window floats on the page
    for ($k = 12; $k -ge 2; $k -= 2) {
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(12, 10, 30, 60))), (RoundRect ($ox - $k + 6) ($oy - $k + 10) ($iw + 2 * $k) ($ih + 2 * $k) (10 + $k)))
    }
    $g.FillPath([Drawing.Brushes]::White, (RoundRect ($ox - 6) ($oy - 6) ($iw + 12) ($ih + 12) 10))
    $g.DrawImage($img, [float]$ox, [float]$oy, [float]$iw, [float]$ih)
    $pen = New-Object Drawing.Pen $orange, 5
    if ($st.Dialog) {
        $d = $st.Dialog
        $g.DrawRectangle((New-Object Drawing.Pen ([Drawing.Color]::FromArgb(31, 58, 95)), 4), [float]($ox + $d[0] * $sc - 4), [float]($oy + $d[1] * $sc - 4), [float]($d[2] * $sc + 8), [float]($d[3] * $sc + 8))
    }
    $i = 0; $pointAtShown = -not $part
    foreach ($b in $st.Boxes) {
        $shown = (-not $part) -or ($part.Show -contains $i)
        if ($shown) {
            $bx = $ox + $b[0] * $sc - 6; $by = $oy + $b[1] * $sc - 6; $bwid = $b[2] * $sc + 12; $bhei = $b[3] * $sc + 12
            $g.DrawRectangle($pen, [float]$bx, [float]$by, [float]$bwid, [float]$bhei)
            # With cues, the pointer (if the step has one) is on the first box named.
            $pointHere = if ($part) { $st.Pointer -ge 0 -and $part.Show[0] -eq $i } else { $i -eq $st.Pointer }
            if ($pointHere) { Pointer $g ($bx + $bwid * 0.6) ($by + $bhei * 0.6) }
            if ($st.PointAt) {
                $pa = $st.PointAt; $cx = $pa[0] + $pa[2] / 2; $cy = $pa[1] + $pa[3] / 2
                if ($cx -ge $b[0] -and $cx -le $b[0] + $b[2] -and $cy -ge $b[1] -and $cy -le $b[1] + $b[3]) { $pointAtShown = $true }
            }
        }
        $i++
    }
    if ($st.PointAt -and $pointAtShown) {
        $pa = $st.PointAt
        # at the right end of the item, so its text stays readable
        Pointer $g ($ox + ($pa[0] + $pa[2] * 0.88) * $sc) ($oy + ($pa[1] + $pa[3] * 0.5) * $sc)
    }
    $g.ResetClip()
    $iwShot = $img.Width; $ihShot = $img.Height
    $img.Dispose()
    # subtitles
    $g.FillRectangle((New-Object Drawing.SolidBrush $band), 0, ($H - $bottomBand), $W, $bottomBand)
    $fs = New-Object Drawing.Font 'Segoe UI', 27
    Centered $g (Clean $t.text) $fs ([Drawing.Brushes]::White) (New-Object Drawing.RectangleF 70, ($H - $bottomBand + 8), ($W - 140), ($bottomBand - 16))
    $g.Dispose(); $fr.F.Save($file, [Drawing.Imaging.ImageFormat]::Png); $fr.F.Dispose()
}

$order = @('title')
foreach ($st in $script:steps) {
    if ($st.Id -eq 'd1_folder') { $order += 'sec_install' }
    $order += $st.Id
}
$order += 'end'
# Slides (cover, section slides, closing slide) have no step number.
function IsCard($id) { $id -eq 'title' -or $id -eq 'end' -or $id -like 'sec_*' }
$stepIds = @($order | Where-Object { -not (IsCard $_) })
# Zoom timing: it starts when the dissolve into the step is over, and takes $zoomTime seconds.
$zoomStart = 1.3; $zoomTime = 1.6; $zoomFrames = [int]($zoomTime * 30)
$zooms = @{}; $partFrames = @{}
$n = 0
foreach ($id in $order) {
    $file = Join-Path $frames ('{0:D2}_{1}.png' -f $n, $id)
    if (IsCard $id) { Card $id $file }
    else {
        $st = $script:steps | Where-Object { $_.Id -eq $id }
        $num = [array]::IndexOf($stepIds, $id) + 1
        StepFrame $st $num $stepIds.Count $file
        $cues = Cues (Text $id).text
        if ($cues.Count) {
            $pf = @()
            for ($k = 0; $k -lt $cues.Count; $k++) {
                $cfile = Join-Path $frames ('{0:D2}_{1}_c{2}.png' -f $n, $id, $k)
                StepFrame $st $num $stepIds.Count $cfile $null $cues[$k]
                $pf += $cfile
            }
            $partFrames[$n] = $pf
            "cues $id : $(($cues | ForEach-Object { '{' + ($_.Show -join ',') + '}' }) -join ' ')"
        }
        elseif ($Zoom) {
            $img = [Drawing.Image]::FromFile($st.Shot); $iw0 = $img.Width; $ih0 = $img.Height; $img.Dispose()
            $v1 = ZoomView $st $iw0 $ih0
            if ($v1) {
                $v0 = FullView $iw0 $ih0
                $zf = @()
                for ($k = 1; $k -le $zoomFrames; $k++) {
                    $pp = $k / $zoomFrames
                    $ss = $pp * $pp * (3 - 2 * $pp)     # ease in and out
                    $zfile = Join-Path $frames ('{0:D2}_z{1:D3}.png' -f $n, $k)
                    StepFrame $st $num $stepIds.Count $zfile (MixView $v0 $v1 $ss)
                    $zf += $zfile
                }
                $zooms[$n] = $zf
                "zoom $id"
            }
        }
    }
    $n++
}

# ------------------------------------------------------------ speech and video
# Spoken text is kept between runs ($Work is emptied each time), so a rebuild asks Azure only for
# what changed (the free tier allows few requests a minute).
$ttsCache = Join-Path $env:TEMP 'bulknotes_demo_tts'
New-Item -ItemType Directory -Force $ttsCache | Out-Null
function Say($text, $wav) {
    if (-not $azure) { $tts.SetOutputToWaveFile($wav); $tts.Speak($text); $tts.SetOutputToNull(); return }
    $keyText = "$Voice|$AzureRate|$text|" + ($script:SayAs | ConvertTo-Json -Compress -Depth 4)
    $sha = [Security.Cryptography.SHA1]::Create()
    $hash = ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($keyText)) | ForEach-Object { $_.ToString('x2') }) -join ''
    $cached = Join-Path $ttsCache "$hash.wav"
    if (-not (Test-Path -LiteralPath $cached)) { Speak-Azure -Text $text -Voice $Voice -OutFile $cached -Rate $AzureRate }
    Copy-Item -LiteralPath $cached $wav -Force
}
# Seconds from the start of a WAV (16-bit PCM, data at byte 44) to the end of the speech in it.
function SpeechEnd($wav) {
    $bytes = [IO.File]::ReadAllBytes($wav)
    $byteRate = [BitConverter]::ToInt32($bytes, 28)
    $i = $bytes.Length - 2
    while ($i -ge 44 -and [Math]::Abs([int][BitConverter]::ToInt16($bytes, $i)) -lt 700) { $i -= 2 }
    [Math]::Max(0.0, ($i - 44) / $byteRate)
}
if (-not $NoVoice) {
    if ($azure) { . (Join-Path $here 'azure_tts.ps1') }
    else {
        $tts = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $tts.SelectVoice($Voice)
        $tts.Rate = $Rate
    }
    "voice: $Voice"
}
else { 'voice: none' }
$ffmpeg = $FFmpeg
if (-not $ffmpeg) {
    $ffmpeg = Get-ChildItem 'C:\ffmpeg-*' -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $ffmpeg) { $ffmpeg = (Get-Command ffmpeg).Source }
"ffmpeg: $ffmpeg"
if (-not ((& $ffmpeg -hide_banner -filters 2>&1 | Out-String) -match '\bxfade\b')) {
    throw "This ffmpeg has no xfade filter (it needs ffmpeg 4.3 or later): $ffmpeg"
}
$inv = [Globalization.CultureInfo]::InvariantCulture
function Num($x) { ([double]$x).ToString('0.###', $inv) }

# The joins: a dip to black after the cover and before the closing slide, dissolves elsewhere.
$count = $order.Count
$joinType = @{}; $joinLen = @{}
for ($k = 1; $k -lt $count; $k++) {
    if ((IsCard $order[$k - 1]) -or (IsCard $order[$k])) { $joinType[$k] = 'fadeblack'; $joinLen[$k] = $Dip }
    else {
        $ra = ($script:steps | Where-Object { $_.Id -eq $order[$k - 1] }).Rows
        $rb = ($script:steps | Where-Object { $_.Id -eq $order[$k] }).Rows
        if ($ra -ge 0 -and $ra -eq $rb) { $joinType[$k] = 'wipeleft'; $joinLen[$k] = $Wipe }
        else { $joinType[$k] = 'fade'; $joinLen[$k] = $Dissolve }
    }
    "join $($order[$k - 1]) -> $($order[$k]): $($joinType[$k]) $($joinLen[$k]) s"
}
$fadeInFirst = 0.8; $endHold = 1.5
# Each step: its join in, a breath, the voice (or the reading time), a pause, its join out.
$args2 = @('-hide_banner', '-loglevel', 'error', '-y')
$durs = @(); $leads = @(); $voiceIdx = @{}; $videoIdx = @{}
$idx = 0
for ($n = 0; $n -lt $count; $n++) {
    $id = $order[$n]
    $frame = Join-Path $frames ('{0:D2}_{1}.png' -f $n, $id)
    $in = if ($n -eq 0) { $fadeInFirst } else { $joinLen[$n] }
    $out = if ($n -eq $count - 1) { $endHold } else { $joinLen[$n + 1] }
    $lead = $in + 0.3; $tail = 0.8
    if ($n -eq 0) { $lead += $CoverIntro }
    $text = Clean (Text $id).text
    if ($NoVoice) {
        # About 2.6 words a second: time to read the subtitle.
        $words = @($text -split '\s+' | Where-Object { $_ }).Count
        $secs = [Math]::Max(2.5, $words / 2.6)
        # The cover and closing slides show no subtitle: a short, fixed time.
        if ($id -eq 'title') { $secs = 1.5 } elseif ($id -eq 'end') { $secs = 4.5 } elseif ($id -like 'sec_*') { $secs = 2.5 }
        $wav = $null
    }
    else {
        $wav = Join-Path $Work ('{0:D2}.wav' -f $n)
        Say $text $wav
        $fs = [IO.File]::OpenRead($wav); $br = New-Object IO.BinaryReader $fs; $fs.Position = 28; $rate = $br.ReadInt32(); $br.Close()
        $secs = ((Get-Item $wav).Length - 44) / $rate
    }
    $dur = [Math]::Round([Math]::Max($lead + $secs + $tail + $out, 4.5), 2)
    $durs += $dur; $leads += $lead
    if ($partFrames.ContainsKey($n)) {
        # When each part starts: where its cue is spoken (the speech of the text before the cue,
        # spoken on its own), or without a voice, its share of the words.
        $cues = Cues (Text $id).text
        $starts = @()
        for ($k = 0; $k -lt $cues.Count; $k++) {
            $pos = $cues[$k].Pos
            if ($pos -eq 0) { $starts += 0.0; continue }
            $before = $text.Substring(0, $pos).Trim()
            if ($wav) {
                $pw = Join-Path $Work ('{0:D2}_cue{1}.wav' -f $n, $k)
                Say $before $pw
                $t = SpeechEnd $pw
            }
            else {
                $all = @($text -split '\s+' | Where-Object { $_ }).Count
                $t = $secs * @($before -split '\s+' | Where-Object { $_ }).Count / [Math]::Max(1, $all)
            }
            $starts += [Math]::Min($dur - 0.5, $lead + $t)
        }
        "  cues at $(($starts | ForEach-Object { Num $_ }) -join ', ') s"
        $q = { param($x) "file '" + ($x -replace '\\', '/') + "'" }
        $lines = @('ffconcat version 1.0')
        $pf = $partFrames[$n]
        for ($k = 0; $k -lt $pf.Count; $k++) {
            $end = if ($k -lt $pf.Count - 1) { $starts[$k + 1] } else { $dur + 1 }
            $lines += (& $q $pf[$k]); $lines += "duration $(Num ([Math]::Max(0.034, $end - $starts[$k])))"
        }
        $lines += (& $q $pf[-1])     # the concat reader ignores the last duration
        $listFile = Join-Path $Work ('{0:D2}_cues.txt' -f $n)
        [IO.File]::WriteAllLines($listFile, $lines)
        # Made into a clip of its own first: many image-list inputs in the one big join ran ffmpeg
        # out of memory.
        $clip = Join-Path $Work ('{0:D2}_cues.mp4' -f $n)
        & $ffmpeg -hide_banner -loglevel error -y -f concat -safe 0 -i $listFile -vf 'fps=30,format=yuv420p' -t (Num $dur) -c:v libx264 -preset veryfast -crf 12 -tune stillimage -threads 4 $clip
        if ($LASTEXITCODE -ne 0) { throw "ffmpeg failed on the cues of $id" }
        # One decoding thread each: with frame threads, every clip's decoder holds many 1080p
        # frames, and the big join ran out of memory.
        $args2 += @('-threads', '1', '-i', $clip)
    }
    elseif ($zooms.ContainsKey($n)) {
        $zf = $zooms[$n]
        $q = { param($x) "file '" + ($x -replace '\\', '/') + "'" }
        $zs = $in + 0.3
        $lines = @('ffconcat version 1.0', (& $q $frame), "duration $(Num $zs)")
        foreach ($z in $zf) { $lines += (& $q $z); $lines += 'duration 0.0333333' }
        $lines += (& $q $zf[-1]); $lines += "duration $(Num ($dur - $zs - $zoomTime + 1))"
        $lines += (& $q $zf[-1])     # the concat reader ignores the last duration
        $listFile = Join-Path $Work ('{0:D2}_zoom.txt' -f $n)
        [IO.File]::WriteAllLines($listFile, $lines)
        $args2 += @('-f', 'concat', '-safe', '0', '-i', $listFile)
    }
    else { $args2 += @('-loop', '1', '-framerate', '30', '-t', (Num $dur), '-i', $frame) }
    $videoIdx[$n] = $idx; $idx++
    if ($wav) { $args2 += @('-i', $wav); $voiceIdx[$n] = $idx; $idx++ }
    "step $id $(Num $dur)s"
}
if (-not $NoVoice -and -not $azure) { $tts.Dispose() }
if ($musicOn) { $args2 += @('-stream_loop', '-1', '-i', $Music); $musicIdx = $idx; $idx++ }

# Video: xfade joins. Each join starts $joinLen before the end of what is joined so far.
$f = @()
for ($n = 0; $n -lt $count; $n++) {
    $f += "[$($videoIdx[$n]):v]fps=30,format=yuv420p,setsar=1,trim=0:$(Num $durs[$n]),settb=AVTB,setpts=PTS-STARTPTS[v$n]"
}
$length = $durs[0]; $vPrev = 'v0'
for ($k = 1; $k -lt $count; $k++) {
    $f += "[$vPrev][v$k]xfade=transition=$($joinType[$k]):duration=$(Num $joinLen[$k]):offset=$(Num ($length - $joinLen[$k]))[xv$k]"
    $vPrev = "xv$k"
    $length += $durs[$k] - $joinLen[$k]
}
$total = $length
$f += "[$vPrev]fade=t=in:st=0:d=$(Num $fadeInFirst),fade=t=out:st=$(Num ($total - 1.2)):d=1.2[vout]"

# Audio: the voices, each delayed to its place, joined with the same overlaps; the music under it.
$mix = $null
if (-not $NoVoice) {
    for ($n = 0; $n -lt $count; $n++) {
        $ms = [int]($leads[$n] * 1000)
        $f += "[$($voiceIdx[$n]):a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,adelay=$ms|$ms,apad,atrim=0:$(Num $durs[$n]),asetpts=PTS-STARTPTS[a$n]"
    }
    $aPrev = 'a0'
    for ($k = 1; $k -lt $count; $k++) {
        $f += "[$aPrev][a$k]acrossfade=d=$(Num $joinLen[$k]):c1=tri:c2=tri[xa$k]"
        $aPrev = "xa$k"
    }
    $mix = $aPrev
}
if ($musicOn) {
    # The music alone at $MusicVolume, then down to $MusicUnderVoice over the second before the
    # narration starts (where it would start, without a voice), and there for the rest.
    $t1 = $leads[0]; $t0 = [Math]::Max(0.0, $t1 - 1.0)
    $hi = Num $MusicVolume; $lo = Num $MusicUnderVoice
    $level = "if(lt(t,$(Num $t0)),$hi,if(lt(t,$(Num $t1)),$hi-($hi-$lo)*(t-$(Num $t0))/$(Num ($t1 - $t0)),$lo))"
    $f += "[$($musicIdx):a]aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,atrim=0:$(Num $total),asetpts=PTS-STARTPTS,volume='$level':eval=frame,afade=t=in:st=0:d=2,afade=t=out:st=$(Num ($total - 3)):d=3[music]"
    if ($mix) {
        $f += "[$mix][music]amix=inputs=2:duration=first:normalize=0[amixed]"
        $mix = 'amixed'
    }
    else { $mix = 'music' }
}
if (-not $mix) { $f += "anullsrc=r=48000:cl=stereo,atrim=0:$(Num $total)[silence]"; $mix = 'silence' }
$f += "[$mix]afade=t=out:st=$(Num ($total - 1.2)):d=1.2[aout]"

$script = Join-Path $Work 'filter.txt'
[IO.File]::WriteAllText($script, ($f -join ";`n"))
$suffix = $(if ($Zoom) { '_zoom' } else { '' }) + $(if ($NoVoice) { '_novoice' } else { '' })
$outFile = Join-Path $here ("ProjectSettings_Demo_{0}{1}.mp4" -f $Lang.ToUpper(), $suffix)
# Newer ffmpeg reads an option's value from a file with -/option; older ones had -filter_complex_script.
$fromFile = if ((& $ffmpeg -hide_banner -h long 2>&1 | Out-String) -match 'filter_complex_script') { '-filter_complex_script' } else { '-/filter_complex' }
$args2 += @($fromFile, $script, '-map', '[vout]', '-map', '[aout]', '-t', (Num $total),
    # Four encoder threads: fewer buffers, so the join fits when memory is short.
    '-c:v', 'libx264', '-preset', 'medium', '-tune', 'stillimage', '-crf', '18', '-r', '30', '-threads', '4',
    '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', $outFile)
& $ffmpeg @args2
if ($LASTEXITCODE -ne 0) { throw 'ffmpeg failed' }
"written $outFile ($(Num $total)s)"

# The YouTube description (the standard, section 12), with each step's question ('q' in
# narration.json) at the time its step is fully on screen in this video: the end of the join into
# it, rounded up. Only for the voiced video, whose times differ from the test's. English is
# youtubetext.txt (the user's name for it), other languages youtubetext_<lang>.txt.
if (-not $NoVoice) {
    $yt = $narr.youtube.$Lang
    if (-not $yt) { "narration.json has no youtube text for '$Lang': no youtubetext written" }
    else {
        $lines = @($yt.about, '', $yt.download, $yt.link, '')
        $at = $durs[0]
        for ($k = 1; $k -lt $count; $k++) {
            $q = (Text $order[$k]).q
            if ($q) { $sec = [int][Math]::Ceiling($at); $lines += ('{0}:{1:D2} {2}' -f [int][Math]::Floor($sec / 60), ($sec % 60), $q) }
            $at += $durs[$k] - $joinLen[$k]
        }
        $lines += ''
        $lines += @($yt.credits)
        $ytFile = Join-Path $here $(if ($Lang -eq 'en') { 'youtubetext.txt' } else { "youtubetext_$Lang.txt" })
        [IO.File]::WriteAllText($ytFile, (($lines -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding $true))
        "written $ytFile"
    }
}
