# Renders every Kenaz raster asset from the SVG sources in docs/brand/.
#
# Uses headless Edge because it is already on the machine and needs no npm
# dependency. Load-bearing details, learned the hard way on this machine's
# Edge build (151.x):
#   --headless=new     the OLD --headless (no "=new") never returns on this
#                       build: it silently falls back to a normal multi-process
#                       browser instead of running the one-shot --screenshot
#                       path, so Start-Process -Wait hangs forever. If your
#                       Edge build behaves the other way round, swap it back.
#   retry per render   --headless=new itself is not perfectly reliable back
#                       to back on this machine (roughly one run in three
#                       fails to exit) — most likely resource contention from
#                       spinning up full browser processes in quick succession
#                       (a redundant "usagestats"/"urlstats" registry-write
#                       error precedes every run and is unrelated noise). Each
#                       render gets a bounded wait and a couple of retries
#                       with a stray-process cleanup between them, rather than
#                       one indefinite Start-Process -Wait.
#   --default-background-color  without it, transparent regions render as a white band.
#
# Fraunces is inlined as a base64 data URI rather than referenced by path.
# Headless Chromium treats file:// origins as opaque and blocks font fetches
# across them; it does not error, it silently falls back to Palatino. A
# path-based @font-face would ship a banner in the wrong typeface with nothing
# to show for it. font-display is deliberately left unset in the @font-face —
# "block" measurably correlated with the hang above during testing, and it
# buys nothing for a single static screenshot taken after the page has fully
# loaded.

$ErrorActionPreference = "Stop"

$web  = Split-Path $PSScriptRoot -Parent
$repo = Split-Path $web -Parent
$brand = Join-Path $repo "docs\brand"
$icons = Join-Path $web "public\icons"
$font  = Join-Path $web "public\design-system\assets\fonts\fraunces-latin-600-normal.woff2"

# Standard install locations first; fall back to a versioned EdgeCore folder
# (seen on machines where Edge is deployed under Program Files\Microsoft\EdgeCore\<version>\
# rather than the usual Edge\Application\ path).
$edgeCandidates = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "${env:ProgramFiles}\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ }

if (-not $edgeCandidates) {
    $edgeCore = @(
        "${env:ProgramFiles(x86)}\Microsoft\EdgeCore",
        "${env:ProgramFiles}\Microsoft\EdgeCore"
    ) | Where-Object { Test-Path $_ }
    $edgeCandidates = $edgeCore |
        ForEach-Object { Get-ChildItem $_ -Filter "msedge.exe" -Recurse -Depth 2 -ErrorAction SilentlyContinue } |
        Select-Object -ExpandProperty FullName
}

$edge = $edgeCandidates | Select-Object -First 1
if (-not $edge) { throw "Edge not found. Install Microsoft Edge or edit the paths above." }
if (-not (Test-Path $font)) { throw "Fraunces woff2 missing at $font. Run the design-system extract first (Task 2)." }

if (-not (Test-Path $icons)) { New-Item -ItemType Directory $icons | Out-Null }

$fontFace = "@font-face{font-family:'Fraunces';font-weight:600;font-style:normal;src:url(data:font/woff2;base64," +
            [Convert]::ToBase64String([IO.File]::ReadAllBytes($font)) + ") format('woff2')}"

$tmp = Join-Path ([IO.Path]::GetTempPath()) "kenaz-brand"
if (-not (Test-Path $tmp)) { New-Item -ItemType Directory $tmp | Out-Null }

function Render {
    param([string]$Svg, [int]$W, [int]$H, [string]$Out, [string]$Bg = "#000000", [double]$Scale = 1.0)

    $inner = Get-Content $Svg -Raw
    $box = if ($Scale -eq 1.0) { "width:100%;height:100%" } else { "width:$($Scale*100)%;height:$($Scale*100)%" }
    $html = @"
<!doctype html><meta charset="utf-8"><style>
$fontFace
*{margin:0;padding:0}
html,body{width:${W}px;height:${H}px;background:$Bg;overflow:hidden}
body{display:flex;align-items:center;justify-content:center}
svg{$box;display:block}
</style>
$inner
"@
    $page = Join-Path $tmp ((Split-Path $Out -Leaf) + ".html")
    [IO.File]::WriteAllText($page, $html, (New-Object Text.UTF8Encoding $false))

    $bgArg = $Bg.TrimStart('#') + "FF"
    if (Test-Path $Out) { Remove-Item $Out -Force }

    $maxAttempts = 3
    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++) {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $edge
        $psi.Arguments = "--headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 " +
            "--default-background-color=$bgArg --window-size=$W,$H --screenshot=`"$Out`" `"$page`""
        $psi.UseShellExecute = $false
        $proc = [System.Diagnostics.Process]::Start($psi)
        $finished = $proc.WaitForExit(20000)
        if (-not $finished) {
            try { $proc.Kill($true) } catch {}
        }
        # One render at a time by design: parallel invocations return early
        # and only the fastest actually writes its file. Sweep any stray
        # processes from a killed/hung attempt before the next one starts.
        Get-Process msedge -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

        if ((Test-Path $Out) -and (Get-Item $Out).Length -gt 0) { break }
        if ($attempt -lt $maxAttempts) { Start-Sleep -Seconds 1 }
    }

    if (-not (Test-Path $Out)) { throw "Render produced nothing after $maxAttempts attempts: $Out" }
    Write-Host ("  {0,-28} {1}x{2}  {3:N0} bytes" -f (Split-Path $Out -Leaf), $W, $H, (Get-Item $Out).Length)
}

$mark  = Join-Path $brand "mark.svg"
$small = Join-Path $brand "mark-small.svg"
$banner = Join-Path $brand "banner.svg"

Write-Host "Icons:"
Render -Svg $small -W 32  -H 32  -Out (Join-Path $icons "icon-32.png")
Render -Svg $mark  -W 192 -H 192 -Out (Join-Path $icons "icon-192.png")
Render -Svg $mark  -W 512 -H 512 -Out (Join-Path $icons "icon-512.png")

# Android crops maskable icons to a circle at ~80% of the tile. The mark's ring
# sits at 88%, so at full bleed the ring gets shaved off. 62% keeps it whole.
Render -Svg $mark -W 512 -H 512 -Out (Join-Path $icons "icon-maskable-512.png") -Scale 0.62

Write-Host "Cards:"
Render -Svg $banner -W 1280 -H 640 -Out (Join-Path $brand "banner.png") -Bg "#0b0a09"
Render -Svg $banner -W 1200 -H 630 -Out (Join-Path $web "public\og.png") -Bg "#0b0a09"

Remove-Item $tmp -Recurse -Force
Write-Host "Done."
