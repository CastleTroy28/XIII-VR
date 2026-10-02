param([string]$GameFolder = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
try {
    $root = (Resolve-Path -LiteralPath $GameFolder).Path
    if (-not (Test-Path -LiteralPath (Join-Path $root 'XIII.exe'))) { throw 'Put both Collect-XIII-Logs files beside XIII.exe.' }
    $loader = Join-Path $root 'BepInEx'
    $ini = Join-Path $root 'doorstop_config.ini'
    if (Test-Path -LiteralPath $ini) {
        $match = [regex]::Match([IO.File]::ReadAllText($ini), '(?im)^\s*target_assembly\s*=\s*(.+?)\s*$')
        if ($match.Success) {
            $target = [Environment]::ExpandEnvironmentVariables($match.Groups[1].Value.Trim().Trim('"'))
            if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path $root $target }
            $target = [IO.Path]::GetFullPath($target)
            if ([IO.Path]::GetFileName($target) -eq 'BepInEx.Unity.IL2CPP.dll') { $loader = Split-Path (Split-Path $target -Parent) -Parent }
        }
    }
    $files = New-Object 'System.Collections.Generic.List[object]'
    function Include([string]$Path,[string]$Name) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) { $files.Add([pscustomobject]@{path=$Path;name=$Name}) }
    }
    Include (Join-Path $loader 'LogOutput.log') 'Active-loader-LogOutput.log'
    Include (Join-Path $loader 'config\xiii.vr.xrbootstrap.cfg') 'xiii.vr.xrbootstrap.cfg'
    # 0.1.149: the mod's frame timings (the two newest runs), for rare freezes.
    $perf = @(Get-ChildItem -LiteralPath (Join-Path $loader 'config') -Filter 'XIII-XR-performance-*.csv' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 2)
    foreach ($file in $perf) { Include $file.FullName ('Perf-' + $file.Name) }
    # 0.1.197: the weapon meshes the mod writes for checking (the Uzi's handle; 0.1.198: the zipline hook).
    $meshes = @(Get-ChildItem -LiteralPath (Join-Path $loader 'config') -Filter 'XIII-XR-mesh-*.txt' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 4)
    foreach ($file in $meshes) { Include $file.FullName ('Mesh-' + $file.Name) }
    $localLow = Join-Path (Split-Path $env:LOCALAPPDATA -Parent) 'LocalLow'
    $players = @(Get-ChildItem -LiteralPath $localLow -Filter 'Player*.log' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '(?i)[\\/]XIII([\\/]|$)' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 4)
    $index = 0
    foreach ($file in $players) { Include $file.FullName ('Player-' + $index + '-' + $file.Name); $index++ }
    # Only recent XIII Unity crash reports; do not sweep other applications.
    $crashRoot = Join-Path $env:TEMP 'Microids\XIII\Crashes'
    $reports = @(Get-ChildItem -LiteralPath $crashRoot -Directory -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 2)
    $crashIndex = 0
    foreach ($report in $reports) {
        foreach ($name in @('error.log','Player.log','crash.dmp')) {
            Include (Join-Path $report.FullName $name) ('Crash-' + $crashIndex + '-' + $name)
        }
        $crashIndex++
    }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $output = Join-Path $root ('XIII-VR-Logs-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.zip')
    $manifest = New-Object 'System.Collections.Generic.List[string]'
    $manifest.Add('Collected UTC: ' + [DateTime]::UtcNow.ToString('o'))
    $manifest.Add('Game: ' + $root); $manifest.Add('Effective loader: ' + $loader)
    $dlls = @(Get-ChildItem -LiteralPath (Join-Path $loader 'plugins') -Filter 'XIII.XRBootstrap.dll' -Recurse -File -ErrorAction SilentlyContinue)
    foreach ($dll in $dlls) { $manifest.Add('DLL: ' + $dll.FullName + ' SHA256=' + (Get-FileHash -LiteralPath $dll.FullName -Algorithm SHA256).Hash) }
    $zip = [IO.Compression.ZipFile]::Open($output,[IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            # FileShare.ReadWrite allows collection while Unity is still running.
            $input = [IO.File]::Open($file.path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
            try {
                $entry = $zip.CreateEntry($file.name);$destination = $entry.Open()
                try { $input.CopyTo($destination) } finally { $destination.Dispose() }
            } finally { $input.Dispose() }
            $info = Get-Item -LiteralPath $file.path
            $manifest.Add($file.name + ' UTC=' + $info.LastWriteTimeUtc.ToString('o') + ' source=' + $file.path)
        }
        $entry = $zip.CreateEntry('Collection.txt');$writer = [IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
        try { foreach ($line in $manifest) { $writer.WriteLine($line) } } finally { $writer.Dispose() }
    } finally { $zip.Dispose() }
    Write-Host ('Created: ' + $output)
    Write-Host 'Send this ZIP after reproducing the issue. It includes the effective loader log and file timestamps.'
} catch { Write-Host ('Log collection failed: ' + $_.Exception.Message) -ForegroundColor Red;exit 1 }
