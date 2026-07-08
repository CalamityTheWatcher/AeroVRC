<#
.SYNOPSIS
    AeroVRC Publish & Release - a small GUI front-end for building the standalone
    exe and cutting GitHub releases, so you don't have to remember commands.

.DESCRIPTION
    Double-click this file (or run it) to open a window with three actions:
      - Publish standalone     : rebuild publish\sc\AeroVRC.exe
      - Publish & Launch       : rebuild, then run it
      - Cut GitHub Release     : bump version, rebuild, commit, tag, and publish
                                 a GitHub release with the exe attached
    Work runs in the background with a live log, so the window stays responsive.

.NOTES
    Requires the local .NET 9 SDK, gh CLI (authenticated), and git - all already
    set up on this machine. Equivalent to release.ps1 but with a UI.
#>
param([switch]$SelfTest)

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$ProjectDir = $PSScriptRoot
$Csproj     = Join-Path $ProjectDir 'AeroVRC.csproj'

# ---- locate tools ----
$dotnet = "$env:LOCALAPPDATA\Microsoft\dotnet-sdk9\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source }
$gh = "C:\Program Files\GitHub CLI\gh.exe"
if (-not (Test-Path $gh)) { $gh = (Get-Command gh -ErrorAction SilentlyContinue).Source }

# ---- current version from the csproj ----
$curVer = '1.0.0'
try {
    $v = ([xml](Get-Content $Csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ($v) { $curVer = $v }
} catch {}

# ---- shared state between the UI and the background worker runspace ----
$sync = [hashtable]::Synchronized(@{
    Log     = [System.Collections.Queue]::Synchronized((New-Object System.Collections.Queue))
    Dotnet  = $dotnet
    Gh      = $gh
    ProjDir = $ProjectDir
    Csproj  = $Csproj
    Repo    = 'CalamityTheWatcher/AeroVRC'
})

# ============================ WORKER (runs off the UI thread) ================
$worker = @'
function log($m){ $sync.Log.Enqueue([string]$m) }
$dotnet=$sync.Dotnet; $gh=$sync.Gh; $proj=$sync.ProjDir; $csproj=$sync.Csproj; $repo=$sync.Repo
Set-Location $proj
$scExe = Join-Path $proj 'publish\sc\AeroVRC.exe'

function Publish-Sc {
    log "== Publishing standalone (self-contained) exe... =="
    Get-Process AeroVRC -ErrorAction SilentlyContinue | Stop-Process -Force
    & $dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true -o publish\sc --source https://api.nuget.org/v3/index.json 2>&1 | ForEach-Object { log $_ }
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (sc) failed.' }
}
function Publish-Fd {
    log "== Publishing small runtime-dependent (fd) exe... =="
    & $dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o publish\fd --source https://api.nuget.org/v3/index.json 2>&1 | ForEach-Object { log $_ }
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (fd) failed.' }
}

try {
    switch ($jobArgs.Op) {
        'publish' {
            Publish-Sc
            if ($jobArgs.Fd) { Publish-Fd }
            log "`nDONE. Standalone exe: $scExe"
        }
        'run' {
            Publish-Sc
            if ($jobArgs.Fd) { Publish-Fd }
            log "`nLaunching $scExe ..."
            Start-Process $scExe
            log "DONE."
        }
        'release' {
            $ver = $jobArgs.Version
            if ($ver -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 2.1.0 (got '$ver')." }
            $tag = "v$ver"
            log "== Cutting release $tag =="
            $null = & $gh release view $tag --repo $repo --json tagName 2>$null
            if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists - pick a new version." }

            $xml = [xml](Get-Content $csproj)
            $pg  = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
            if (-not $pg) { throw "No <Version> in the csproj." }
            $pg.Version = $ver; $xml.Save($csproj)
            log "Version set to $ver."

            Publish-Sc
            if ($jobArgs.Fd) { Publish-Fd }

            log "== Committing version bump and pushing main =="
            & git -C $proj add AeroVRC.csproj 2>&1 | ForEach-Object { log $_ }
            & git -C $proj commit -m "Release $tag" 2>&1 | ForEach-Object { log $_ }
            & git -C $proj push 2>&1 | ForEach-Object { log $_ }

            log "== Creating GitHub release $tag =="
            $asset = "$scExe#AeroVRC.exe (standalone, no .NET needed)"
            if ($jobArgs.Notes) {
                & $gh release create $tag --repo $repo --title "AeroVRC $tag" --latest --notes $jobArgs.Notes $asset 2>&1 | ForEach-Object { log $_ }
            } else {
                & $gh release create $tag --repo $repo --title "AeroVRC $tag" --latest --generate-notes $asset 2>&1 | ForEach-Object { log $_ }
            }
            if ($LASTEXITCODE -ne 0) { throw 'gh release create failed.' }
            log "`nDONE: https://github.com/$repo/releases/tag/$tag"
        }
        'debug' {
            log "== Building Debug and launching... =="
            Get-Process AeroVRC -ErrorAction SilentlyContinue | Stop-Process -Force
            & $dotnet build -c Debug 2>&1 | ForEach-Object { log $_ }
            if ($LASTEXITCODE -ne 0) { throw 'dotnet build (Debug) failed.' }
            $dbg = Join-Path $proj 'bin\Debug\net9.0-windows\AeroVRC.exe'
            log "Launching $dbg ..."
            Start-Process $dbg
            log "DONE."
        }
        'prerelease' {
            $ver = $jobArgs.Version
            if ($ver -notmatch '^\d+\.\d+\.\d+$') { throw "Version must look like 2.1.0 (got '$ver')." }
            $stamp = Get-Date -Format 'yyyyMMdd-HHmm'
            $tag = "v$ver-test.$stamp"
            log "== Cutting PRE-RELEASE $tag (won't replace 'latest') =="
            Publish-Sc
            if ($jobArgs.Fd) { Publish-Fd }
            $asset = "$scExe#AeroVRC.exe (test build, no .NET needed)"
            $title = "AeroVRC $ver (test $stamp)"
            if ($jobArgs.Notes) {
                & $gh release create $tag --repo $repo --title $title --prerelease --notes $jobArgs.Notes $asset 2>&1 | ForEach-Object { log $_ }
            } else {
                & $gh release create $tag --repo $repo --title $title --prerelease --generate-notes $asset 2>&1 | ForEach-Object { log $_ }
            }
            if ($LASTEXITCODE -ne 0) { throw 'gh release create (prerelease) failed.' }
            log "`nDONE (pre-release): https://github.com/$repo/releases/tag/$tag"
        }
    }
} catch {
    log "`nERROR: $($_.Exception.Message)"
}
'@

# ================================ UI ========================================
$form = New-Object System.Windows.Forms.Form
$form.Text = "AeroVRC - Publish & Release"
$form.Size = New-Object System.Drawing.Size(660, 560)
$form.MinimumSize = New-Object System.Drawing.Size(560, 460)
$form.StartPosition = 'CenterScreen'
$form.BackColor = [System.Drawing.Color]::FromArgb(24, 28, 52)
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$form.ForeColor = [System.Drawing.Color]::White
try { if (Test-Path (Join-Path $ProjectDir 'Logo.ico')) { $form.Icon = New-Object System.Drawing.Icon((Join-Path $ProjectDir 'Logo.ico')) } } catch {}

$accent = [System.Drawing.Color]::FromArgb(74, 156, 255)
$card   = [System.Drawing.Color]::FromArgb(34, 40, 76)

function New-Label($text, $x, $y, $w) {
    $l = New-Object System.Windows.Forms.Label
    $l.Text = $text; $l.Location = New-Object System.Drawing.Point($x, $y)
    $l.Size = New-Object System.Drawing.Size($w, 20); $l.ForeColor = [System.Drawing.Color]::FromArgb(180, 190, 220)
    $form.Controls.Add($l); $l
}
function Style-Button($b, $primary) {
    $b.FlatStyle = 'Flat'; $b.FlatAppearance.BorderSize = 0; $b.ForeColor = [System.Drawing.Color]::White
    $b.BackColor = if ($primary) { $accent } else { [System.Drawing.Color]::FromArgb(42, 50, 92) }
    $b.Cursor = 'Hand'; $b.Font = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
}

[void](New-Label "Version (for releases):" 14 14 160)
$txtVer = New-Object System.Windows.Forms.TextBox
$txtVer.Text = $curVer; $txtVer.Location = New-Object System.Drawing.Point(176, 12)
$txtVer.Size = New-Object System.Drawing.Size(90, 24)
$txtVer.BackColor = [System.Drawing.Color]::FromArgb(18, 22, 44); $txtVer.ForeColor = [System.Drawing.Color]::White
$txtVer.BorderStyle = 'FixedSingle'
$form.Controls.Add($txtVer)
[void](New-Label "(current: $curVer - bump it for a new release)" 276 14 340)

[void](New-Label "Release notes (optional - blank = auto-generate from commits):" 14 44 460)
$txtNotes = New-Object System.Windows.Forms.TextBox
$txtNotes.Multiline = $true; $txtNotes.Location = New-Object System.Drawing.Point(14, 66)
$txtNotes.Size = New-Object System.Drawing.Size(618, 60); $txtNotes.Anchor = 'Top,Left,Right'
$txtNotes.BackColor = [System.Drawing.Color]::FromArgb(18, 22, 44); $txtNotes.ForeColor = [System.Drawing.Color]::White
$txtNotes.BorderStyle = 'FixedSingle'; $txtNotes.ScrollBars = 'Vertical'
$form.Controls.Add($txtNotes)

$chkFd = New-Object System.Windows.Forms.CheckBox
$chkFd.Text = "Also build the small runtime-dependent exe (publish\fd)"
$chkFd.Location = New-Object System.Drawing.Point(14, 134); $chkFd.Size = New-Object System.Drawing.Size(500, 22)
$chkFd.ForeColor = [System.Drawing.Color]::FromArgb(180, 190, 220)
$form.Controls.Add($chkFd)

# Row 1: build / run
$btnPublish = New-Object System.Windows.Forms.Button
$btnPublish.Text = "Publish standalone"; $btnPublish.Location = New-Object System.Drawing.Point(14, 164)
$btnPublish.Size = New-Object System.Drawing.Size(150, 34); Style-Button $btnPublish $false
$form.Controls.Add($btnPublish)

$btnRun = New-Object System.Windows.Forms.Button
$btnRun.Text = "Publish && Launch"; $btnRun.Location = New-Object System.Drawing.Point(170, 164)
$btnRun.Size = New-Object System.Drawing.Size(150, 34); Style-Button $btnRun $false
$form.Controls.Add($btnRun)

$btnDebug = New-Object System.Windows.Forms.Button
$btnDebug.Text = "Build Debug && Run"; $btnDebug.Location = New-Object System.Drawing.Point(326, 164)
$btnDebug.Size = New-Object System.Drawing.Size(150, 34); Style-Button $btnDebug $false
$form.Controls.Add($btnDebug)

# Row 2: releases
$btnRelease = New-Object System.Windows.Forms.Button
$btnRelease.Text = "Cut GitHub Release"; $btnRelease.Location = New-Object System.Drawing.Point(14, 206)
$btnRelease.Size = New-Object System.Drawing.Size(180, 34); Style-Button $btnRelease $true
$form.Controls.Add($btnRelease)

$btnPre = New-Object System.Windows.Forms.Button
$btnPre.Text = "Cut Pre-release (test)"; $btnPre.Location = New-Object System.Drawing.Point(200, 206)
$btnPre.Size = New-Object System.Drawing.Size(180, 34); Style-Button $btnPre $false
$form.Controls.Add($btnPre)

$btnReleases = New-Object System.Windows.Forms.Button
$btnReleases.Text = "Open Releases page"; $btnReleases.Location = New-Object System.Drawing.Point(390, 206)
$btnReleases.Size = New-Object System.Drawing.Size(160, 34); Style-Button $btnReleases $false
$btnReleases.Anchor = 'Top,Right'
$form.Controls.Add($btnReleases)

$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Multiline = $true; $logBox.ReadOnly = $true; $logBox.ScrollBars = 'Vertical'
$logBox.Location = New-Object System.Drawing.Point(14, 250)
$logBox.Size = New-Object System.Drawing.Size(618, 228); $logBox.Anchor = 'Top,Bottom,Left,Right'
$logBox.BackColor = [System.Drawing.Color]::FromArgb(12, 14, 30); $logBox.ForeColor = [System.Drawing.Color]::FromArgb(210, 220, 240)
$logBox.Font = New-Object System.Drawing.Font("Consolas", 9); $logBox.BorderStyle = 'FixedSingle'
$form.Controls.Add($logBox)

$status = New-Object System.Windows.Forms.Label
$status.Text = "Ready."; $status.Location = New-Object System.Drawing.Point(14, 486)
$status.Size = New-Object System.Drawing.Size(618, 20); $status.Anchor = 'Bottom,Left,Right'
$status.ForeColor = [System.Drawing.Color]::FromArgb(150, 200, 150)
$form.Controls.Add($status)

# ---- background job plumbing ----
$script:job = $null
$actionButtons = @($btnPublish, $btnRun, $btnDebug, $btnRelease, $btnPre)

function Start-Worker([hashtable]$jobArgs) {
    if ($script:job) { return }
    $actionButtons | ForEach-Object { $_.Enabled = $false }
    $status.Text = "Working..."; $status.ForeColor = [System.Drawing.Color]::FromArgb(242, 178, 74)
    $rs = [runspacefactory]::CreateRunspace(); $rs.Open()
    $rs.SessionStateProxy.SetVariable('sync', $sync)
    $rs.SessionStateProxy.SetVariable('jobArgs', $jobArgs)
    $psh = [powershell]::Create(); $psh.Runspace = $rs
    [void]$psh.AddScript($worker)
    $handle = $psh.BeginInvoke()
    $script:job = @{ PS = $psh; RS = $rs; Handle = $handle }
}

$btnPublish.Add_Click({ Start-Worker @{ Op = 'publish'; Fd = $chkFd.Checked } })
$btnRun.Add_Click({ Start-Worker @{ Op = 'run'; Fd = $chkFd.Checked } })
$btnRelease.Add_Click({
    $ver = $txtVer.Text.Trim()
    if ($ver -notmatch '^\d+\.\d+\.\d+$') {
        [System.Windows.Forms.MessageBox]::Show("Version must look like 2.1.0", "Invalid version", 'OK', 'Warning') | Out-Null
        return
    }
    $ok = [System.Windows.Forms.MessageBox]::Show("Cut release v$ver, push to GitHub, and mark it latest?", "Confirm release", 'YesNo', 'Question')
    if ($ok -ne 'Yes') { return }
    Start-Worker @{ Op = 'release'; Version = $ver; Notes = $txtNotes.Text.Trim(); Fd = $chkFd.Checked }
})
$btnDebug.Add_Click({ Start-Worker @{ Op = 'debug' } })
$btnPre.Add_Click({
    $ver = $txtVer.Text.Trim()
    if ($ver -notmatch '^\d+\.\d+\.\d+$') {
        [System.Windows.Forms.MessageBox]::Show("Version must look like 2.1.0", "Invalid version", 'OK', 'Warning') | Out-Null
        return
    }
    $ok = [System.Windows.Forms.MessageBox]::Show("Push a TEST pre-release based on v$ver? It shows under Releases but will NOT replace the 'latest' download.", "Confirm pre-release", 'YesNo', 'Question')
    if ($ok -ne 'Yes') { return }
    Start-Worker @{ Op = 'prerelease'; Version = $ver; Notes = $txtNotes.Text.Trim(); Fd = $chkFd.Checked }
})
$btnReleases.Add_Click({ Start-Process "https://github.com/$($sync.Repo)/releases" })

# ---- pump: drain the log queue and re-enable buttons when the job finishes ----
$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 150
$timer.Add_Tick({
    while ($sync.Log.Count -gt 0) { $logBox.AppendText([string]$sync.Log.Dequeue() + [Environment]::NewLine) }
    if ($script:job -and $script:job.Handle.IsCompleted) {
        try { [void]$script:job.PS.EndInvoke($script:job.Handle) } catch { $logBox.AppendText("ERROR: $($_.Exception.Message)`r`n") }
        $script:job.PS.Dispose(); $script:job.RS.Close(); $script:job = $null
        $actionButtons | ForEach-Object { $_.Enabled = $true }
        $status.Text = "Ready."; $status.ForeColor = [System.Drawing.Color]::FromArgb(150, 200, 150)
    }
})

# ---- tool checks ----
if (-not $dotnet) { $actionButtons | ForEach-Object { $_.Enabled = $false }; $logBox.AppendText("WARNING: .NET SDK not found - build/publish disabled.`r`n") }
if (-not $gh)     { $btnRelease.Enabled = $false; $btnPre.Enabled = $false; $logBox.AppendText("WARNING: gh CLI not found - releases disabled (publish still works).`r`n") }

if ($SelfTest) { Write-Host "publish-gui.ps1: form built OK ($($form.Controls.Count) controls)"; $form.Dispose(); return }

$timer.Start()
[void]$form.ShowDialog()
$timer.Stop()
