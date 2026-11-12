<#
.SYNOPSIS
    Turns the JSON output of "jsondiff-cli diff --json" into a static HTML page.

.DESCRIPTION
    The page shows the files and the difference counts at the top, then every hunk as a collapsible section with the
    left and right lines side by side. It has no external dependencies and works offline.

    Works in Windows PowerShell 5.1 and PowerShell 7.

.PARAMETER Path
    A file containing the output of "jsondiff-cli diff --json".

.PARAMETER InputObject
    The output of "jsondiff-cli diff --json", piped in line by line or as one string.

.PARAMETER OutputPath
    The HTML file to write. Default: diff.html in the current directory.

.PARAMETER Title
    The page title. Default: "<left file> vs. <right file>".

.EXAMPLE
    jsondiff-cli diff left.json right.json --json | .\ConvertTo-DiffHtml.ps1 -OutputPath diff.html

.EXAMPLE
    jsondiff-cli diff left.json right.json --json --context 5 > diff.json
    .\ConvertTo-DiffHtml.ps1 -Path diff.json -OutputPath diff.html

.NOTES
    Piping decodes the CLI output with the console's code page, which can garble non-ASCII text in Windows PowerShell
    5.1. If that happens, set [Console]::OutputEncoding = [Text.Encoding]::UTF8 first, or go through a file with -Path.
#>
[CmdletBinding(DefaultParameterSetName = 'Pipeline')]
param(
    [Parameter(Mandatory, ParameterSetName = 'File', Position = 0)]
    [string] $Path,

    [Parameter(Mandatory, ParameterSetName = 'Pipeline', ValueFromPipeline)]
    [AllowEmptyString()]
    [string[]] $InputObject,

    [string] $OutputPath = 'diff.html',

    [string] $Title
)

begin {
    $lines = New-Object System.Collections.Generic.List[string]
}

process {
    if ($PSCmdlet.ParameterSetName -eq 'Pipeline') {
        foreach ($line in $InputObject) { $lines.Add($line) }
    }
}

end {
    Set-StrictMode -Version 2.0
    $ErrorActionPreference = 'Stop'

    if ($PSCmdlet.ParameterSetName -eq 'File') {
        # ReadAllText honors a BOM (e.g. UTF-16 from "> file" in Windows PowerShell) and defaults to UTF-8.
        $text = [System.IO.File]::ReadAllText((Resolve-Path -LiteralPath $Path).ProviderPath)
    }
    else {
        $text = $lines -join "`n"
    }
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw 'No input. Pipe in or pass -Path to the output of "jsondiff-cli diff --json".'
    }

    $diff = $text | ConvertFrom-Json
    if (-not ($diff.PSObject.Properties.Name -contains 'hunks')) {
        throw 'The input is not the output of "jsondiff-cli diff --json" (no "hunks" property).'
    }

    function Encode([object] $value) {
        if ($null -eq $value) { return '' }
        return [System.Net.WebUtility]::HtmlEncode([string] $value)
    }

    $leftName = Split-Path -Leaf $diff.left
    $rightName = Split-Path -Leaf $diff.right
    # Keep this file ASCII: Windows PowerShell 5.1 reads scripts without a BOM in the ANSI code page.
    if (-not $Title) { $Title = "$leftName $([char]0x2194) $rightName" }

    $hunks = @($diff.hunks)
    $s = $diff.summary
    $sb = New-Object System.Text.StringBuilder

    # Used twice: for the system dark mode, and for the toggle button's choice.
    $darkColors = @'
    color-scheme: dark;
    --bg: #0d1117; --fg: #e6edf3; --muted: #8d96a0; --border: #30363d; --panel: #161b22;
    --mod-bg: #3b2e00; --mod-fg: #e3b341;
    --type-bg: #4a2410; --type-fg: #f0883e;
    --left-bg: #4b1218; --left-fg: #ff7b72;
    --right-bg: #12361f; --right-fg: #56d364;
    --ign-fg: #6e7681;
'@

    [void] $sb.Append(@"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>$(Encode $Title)</title>
<script>
// Apply a remembered theme before the page renders, so it does not flash in the other one.
try { var t = localStorage.getItem('jsondiff-theme'); if (t) document.documentElement.setAttribute('data-theme', t); } catch (e) {}
</script>
<style>
:root {
  color-scheme: light;
  --bg: #ffffff; --fg: #1f2328; --muted: #656d76; --border: #d0d7de; --panel: #f6f8fa;
  --mod-bg: #fff8c5; --mod-fg: #7d4e00;
  --type-bg: #ffe2cc; --type-fg: #953800;
  --left-bg: #ffebe9; --left-fg: #a40e26;
  --right-bg: #dafbe1; --right-fg: #116329;
  --ign-fg: #8c959f;
  --gap: repeating-linear-gradient(135deg, transparent 0 4px, rgba(127,127,127,.12) 4px 8px);
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
$darkColors
  }
}
:root[data-theme="dark"] {
$darkColors
}
* { box-sizing: border-box; }
body { margin: 0; padding: 24px 16px 48px; background: var(--bg); color: var(--fg);
  font: 14px/1.45 system-ui, -apple-system, "Segoe UI", sans-serif; }
main { max-width: 1400px; margin: 0 auto; }
header { display: flex; align-items: flex-start; gap: 12px; }
header .titles { flex: 1; min-width: 0; }
h1 { font-size: 20px; margin: 0 0 4px; word-break: break-word; }
.files { color: var(--muted); font-size: 13px; margin-bottom: 16px; word-break: break-all; }
.files b { color: var(--fg); font-weight: 600; }
.stats { display: flex; flex-wrap: wrap; gap: 8px; margin-bottom: 16px; }
.stat { border: 1px solid var(--border); border-radius: 8px; padding: 8px 14px; min-width: 120px; background: var(--panel); }
.stat .n { font-size: 22px; font-weight: 600; font-variant-numeric: tabular-nums; }
.stat .l { font-size: 12px; color: var(--muted); }
.stat.mod .n { color: var(--mod-fg); } .stat.type .n { color: var(--type-fg); }
.stat.left .n { color: var(--left-fg); } .stat.right .n { color: var(--right-fg); }
.toolbar { display: flex; align-items: center; gap: 8px; margin: 0 0 12px; color: var(--muted); font-size: 13px; }
.toolbar .spacer { flex: 1; }
button { font: inherit; font-size: 13px; padding: 4px 10px; border-radius: 6px; cursor: pointer;
  border: 1px solid var(--border); background: var(--panel); color: var(--fg); }
.identical { padding: 24px; border: 1px solid var(--border); border-radius: 8px; text-align: center; background: var(--panel); }
details { border: 1px solid var(--border); border-radius: 8px; margin-bottom: 10px; overflow: hidden; }
summary { cursor: pointer; padding: 6px 12px; background: var(--panel); display: flex; gap: 12px; align-items: baseline;
  flex-wrap: wrap; list-style: none; }
summary::-webkit-details-marker { display: none; }
summary::before { content: "\25B8"; color: var(--muted); }
details[open] > summary::before { content: "\25BE"; }
details[open] > summary { border-bottom: 1px solid var(--border); }
.range { font-family: ui-monospace, Consolas, monospace; font-size: 12px; color: var(--muted); white-space: nowrap; }
.preview { font-family: ui-monospace, Consolas, monospace; font-size: 12px; flex: 1; min-width: 0;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.badges { display: flex; gap: 4px; }
.badge { font-size: 11px; padding: 1px 6px; border-radius: 10px; white-space: nowrap; }
.badge.modified { background: var(--mod-bg); color: var(--mod-fg); }
.badge.typeChanged { background: var(--type-bg); color: var(--type-fg); }
.badge.leftOnly { background: var(--left-bg); color: var(--left-fg); }
.badge.rightOnly { background: var(--right-bg); color: var(--right-fg); }
.scroll { overflow-x: auto; }
table { border-collapse: collapse; width: 100%; table-layout: fixed;
  font-family: ui-monospace, Consolas, monospace; font-size: 12px; }
td { padding: 0 8px; vertical-align: top; white-space: pre; overflow: hidden; text-overflow: ellipsis; }
td.no { width: 56px; text-align: right; color: var(--muted); user-select: none; border-right: 1px solid var(--border); }
td.split { border-left: 1px solid var(--border); }
td.modified { background: var(--mod-bg); }
td.typeChanged { background: var(--type-bg); }
td.unmatched.l { background: var(--left-bg); }
td.unmatched.r { background: var(--right-bg); }
td.ignored { color: var(--ign-fg); font-style: italic; }
td.gap { background: var(--gap); }
@media (max-width: 700px) { td.no { width: 40px; } }
</style>
</head>
<body>
<main>
<header>
<div class="titles">
<h1>$(Encode $Title)</h1>
<div class="files"><b>Left:</b> $(Encode $diff.left)<br><b>Right:</b> $(Encode $diff.right)</div>
</div>
<button type="button" id="theme" onclick="toggleTheme()" title="Switch between light and dark"></button>
</header>
<div class="stats">
  <div class="stat"><div class="n">$($s.total)</div><div class="l">Differences</div></div>
  <div class="stat mod"><div class="n">$($s.modified)</div><div class="l">Modified</div></div>
  <div class="stat type"><div class="n">$($s.typeChanged)</div><div class="l">Type changed</div></div>
  <div class="stat left"><div class="n">$($s.leftOnly)</div><div class="l">Only in left</div></div>
  <div class="stat right"><div class="n">$($s.rightOnly)</div><div class="l">Only in right</div></div>
</div>

"@)

    if ($hunks.Count -eq 0) {
        [void] $sb.Append('<div class="identical">No differences.</div>')
    }
    else {
        $noun = if ($hunks.Count -eq 1) { 'section' } else { 'sections' }
        [void] $sb.Append(@"
<div class="toolbar">$($hunks.Count) $noun<span class="spacer"></span>
  <button type="button" onclick="toggleAll(true)">Expand all</button>
  <button type="button" onclick="toggleAll(false)">Collapse all</button>
</div>

"@)
    }

    foreach ($hunk in $hunks) {
        # Count the differing lines in this hunk by kind, and take the first one as a preview.
        $counts = [ordered]@{ modified = 0; typeChanged = 0; leftOnly = 0; rightOnly = 0 }
        $preview = $null
        foreach ($row in $hunk.rows) {
            $l = $row.left; $r = $row.right
            if ($null -ne $l -and $l.kind -eq 'modified') { $counts.modified++ }
            elseif ($null -ne $l -and $l.kind -eq 'typeChanged') { $counts.typeChanged++ }
            elseif ($null -ne $l -and $l.kind -eq 'unmatched') { $counts.leftOnly++ }
            if ($null -ne $r -and $r.kind -eq 'unmatched') { $counts.rightOnly++ }

            if ($null -eq $preview) {
                foreach ($side in @($l, $r)) {
                    if ($null -ne $side -and $side.kind -ne 'unchanged' -and $side.kind -ne 'ignored') {
                        $preview = $side.text.Trim(); break
                    }
                }
            }
        }
        $labels = @{ modified = 'modified'; typeChanged = 'type changed'; leftOnly = 'only left'; rightOnly = 'only right' }
        $badges = foreach ($key in $counts.Keys) {
            if ($counts[$key] -gt 0) { "<span class=`"badge $key`">$($counts[$key]) $($labels[$key])</span>" }
        }
        $range = "@@ -$($hunk.left.start),$($hunk.left.count) +$($hunk.right.start),$($hunk.right.count) @@"

        [void] $sb.Append("<details open>`n<summary><span class=`"range`">$range</span>")
        [void] $sb.Append("<span class=`"preview`">$(Encode $preview)</span>")
        [void] $sb.Append("<span class=`"badges`">$($badges -join '')</span></summary>`n")
        [void] $sb.Append("<div class=`"scroll`"><table>`n")

        foreach ($row in $hunk.rows) {
            [void] $sb.Append('<tr>')
            foreach ($pair in @(@($row.left, 'l'), @($row.right, 'r'))) {
                $line = $pair[0]; $side = $pair[1]
                $split = if ($side -eq 'r') { ' split' } else { '' }
                if ($null -eq $line) {
                    [void] $sb.Append("<td class=`"no gap$split`"></td><td class=`"gap`"></td>")
                }
                else {
                    $kind = Encode $line.kind
                    [void] $sb.Append("<td class=`"no$split`">$($line.line)</td>")
                    [void] $sb.Append("<td class=`"$kind $side`" title=`"$kind`">$(Encode $line.text)</td>")
                }
            }
            [void] $sb.Append("</tr>`n")
        }
        [void] $sb.Append("</table></div>`n</details>`n")
    }

    [void] $sb.Append(@"
</main>
<script>
function toggleAll(open) {
  document.querySelectorAll('details').forEach(function (d) { d.open = open; });
}

// The theme shown now: the button's choice, else the system's.
function currentTheme() {
  var chosen = document.documentElement.getAttribute('data-theme');
  if (chosen) return chosen;
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

// The button names the theme it switches to.
function updateThemeButton() {
  document.getElementById('theme').textContent =
    currentTheme() === 'dark' ? String.fromCharCode(0x2600) + ' Light' : String.fromCharCode(0x263E) + ' Dark';
}

function toggleTheme() {
  var next = currentTheme() === 'dark' ? 'light' : 'dark';
  document.documentElement.setAttribute('data-theme', next);
  try { localStorage.setItem('jsondiff-theme', next); } catch (e) {}
  updateThemeButton();
}

window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', updateThemeButton);
updateThemeButton();
</script>
</body>
</html>
"@)

    $target = $PSCmdlet.GetUnresolvedProviderPathFromPSPath($OutputPath)
    [System.IO.File]::WriteAllText($target, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
    Write-Host "Wrote $target ($($hunks.Count) sections, $($s.total) differences)"
}
