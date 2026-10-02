# Optional developer checks: run on Windows PowerShell 5.1 or newer.
# Executes helper functions in a temporary fixture directory, not the installer.
$ErrorActionPreference='Stop'; Set-StrictMode -Version Latest
$tokens=$null; $errors=$null
$scriptPath=Join-Path $PSScriptRoot 'Install-XIII-VR.ps1'
if (-not (Test-Path -LiteralPath $scriptPath)) { $scriptPath=Join-Path (Split-Path $PSScriptRoot -Parent) 'Install-XIII-VR.ps1' }
$ast=[Management.Automation.Language.Parser]::ParseFile($scriptPath,[ref]$tokens,[ref]$errors)
if ($errors.Count) { throw ($errors | Out-String) }
$helpers=@('Hash','Under','Resolve-IniPath','Read-Ini','Assert-X64','Assert-Dll64','Test-OpenComposite','Test-Doorstop','Extract-Safe','Restore-Entry','Replace-StagedFile','Get-RetiredFiles')
foreach ($definition in $ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$true)) {
    if ($helpers -contains $definition.Name) { . ([scriptblock]::Create($definition.Extent.Text)) }
}
function Check([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
$root=Join-Path ([IO.Path]::GetTempPath()) ('XIII-Installer-Test-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
try {
    $game=Join-Path $root 'Game with spaces';[void][IO.Directory]::CreateDirectory($game)
    Check (Under (Join-Path $game 'BepInEx\plugins\XIII.dll') $game) 'Valid child rejected'
    Check (-not (Under (Join-Path $root 'Game with spaces-other\file') $game)) 'Sibling prefix accepted'
    Check (-not (Under (Join-Path $game '..\escape') $game)) 'Traversal accepted'
    # 0.1.174: OpenComposite's DLL must be a 64-bit Windows DLL.
    $fake=Join-Path $root 'openvr_api.dll';$bytes=[byte[]]::new(70000);$bytes[0]=0x4D;$bytes[1]=0x5A;$bytes[0x3C]=0x80;$bytes[0x80]=0x50;$bytes[0x81]=0x45;$bytes[0x84]=0x64;$bytes[0x85]=0x86
    [IO.File]::WriteAllBytes($fake,$bytes);Assert-Dll64 $fake
    $bytes[0x84]=0x4C;$bytes[0x85]=0x01;[IO.File]::WriteAllBytes($fake,$bytes);$rejected=$false;try{Assert-Dll64 $fake}catch{$rejected=$true};Check $rejected '32-bit OpenComposite accepted'
    [IO.File]::WriteAllText($fake,'<html>not found</html>');$rejected=$false;try{Assert-Dll64 $fake}catch{$rejected=$true};Check $rejected 'a web page accepted as OpenComposite'
    # 0.1.180: a kept or downloaded OpenComposite is OpenComposite's own DLL (not Valve's, not the mod's switch, not a page).
    $bytes[0x84]=0x64;$bytes[0x85]=0x86;[IO.File]::WriteAllBytes($fake,$bytes);Check (-not (Test-OpenComposite $fake)) 'a 64-bit DLL without OpenComposite in it taken for OpenComposite'
    $marker=[Text.Encoding]::ASCII.GetBytes('OpenComposite DLLMain ERROR');[Array]::Copy($marker,0,$bytes,0x2000,$marker.Length);[IO.File]::WriteAllBytes($fake,$bytes);Check (Test-OpenComposite $fake) 'OpenComposite not recognised'
    Check (-not (Test-OpenComposite (Join-Path $root 'missing.dll'))) 'a missing file taken for OpenComposite'
    [IO.File]::WriteAllText($fake,'<html>OpenComposite</html>');Check (-not (Test-OpenComposite $fake)) 'a web page taken for OpenComposite'
    # 0.1.204: a winhttp.dll left from an earlier BepInEx (Doorstop, its strings ASCII or UTF-16) is taken over; another program's is not.
    $proxy=Join-Path $root 'winhttp.dll';$bytes=[byte[]]::new(70000);$bytes[0]=0x4D;$bytes[1]=0x5A;$bytes[0x3C]=0x80;$bytes[0x80]=0x50;$bytes[0x81]=0x45;$bytes[0x84]=0x64;$bytes[0x85]=0x86
    [IO.File]::WriteAllBytes($proxy,$bytes);Check (-not (Test-Doorstop $proxy)) 'another program''s winhttp.dll taken for Doorstop'
    $marker=[Text.Encoding]::Unicode.GetBytes('doorstop_config.ini');[Array]::Copy($marker,0,$bytes,0x3000,$marker.Length);[IO.File]::WriteAllBytes($proxy,$bytes);Check (Test-Doorstop $proxy) 'Doorstop (UTF-16 strings) not recognised'
    $bytes=[byte[]]::new(70000);$bytes[0]=0x4D;$bytes[1]=0x5A;$bytes[0x3C]=0x80;$bytes[0x80]=0x50;$bytes[0x81]=0x45;$bytes[0x84]=0x64;$bytes[0x85]=0x86
    $marker=[Text.Encoding]::ASCII.GetBytes('DOORSTOP_ENABLED');[Array]::Copy($marker,0,$bytes,0x3000,$marker.Length);[IO.File]::WriteAllBytes($proxy,$bytes);Check (Test-Doorstop $proxy) 'Doorstop (ASCII strings) not recognised'
    [IO.File]::WriteAllText($proxy,'<html>doorstop</html>');Check (-not (Test-Doorstop $proxy)) 'a text file taken for Doorstop'
    Check (-not (Test-Doorstop (Join-Path $root 'missing-winhttp.dll'))) 'a missing file taken for Doorstop'
    # 0.1.209: the sound extractors and the audio collector an earlier install left in the game folder go; the log collector and other files stay.
    $retired=@('Extract-XIII-SFX.cmd','Extract-XIII-SFX.cs','Extract-XIII-Weapon-Sounds-All.cmd','Collect-XIII-Audio.ps1','Audio-Catalog.html')
    foreach ($name in $retired + @('Collect-XIII-Logs.cmd','Collect-XIII-Logs.ps1','XIII.exe','Extract-Other.cmd')) { [IO.File]::WriteAllText((Join-Path $game $name),'x') }
    [void][IO.Directory]::CreateDirectory((Join-Path $game 'Extract-XIII-SFX.ps1'))
    $found=@(Get-RetiredFiles $game | ForEach-Object { Split-Path $_ -Leaf } | Sort-Object)
    Check (($found -join ',') -eq (($retired | Sort-Object) -join ',')) ('retired helpers found: '+($found -join ','))
    Check (@(Get-RetiredFiles (Join-Path $root 'missing-game')).Count -eq 0) 'retired helpers found in a missing folder'
    $ini=Join-Path $root 'doorstop.ini'
    [IO.File]::WriteAllText($ini,"[General]`nEnabled = true`nTarget_Assembly = BepInEx/core/BepInEx.Unity.IL2CPP.dll`n[Il2Cpp]`nCoreClr_Path = dotnet/coreclr.dll`n")
    $parsed=Read-Ini $ini
    Check ($parsed['general/enabled'] -eq 'true') 'INI enabled parsing failed'
    Check ((Resolve-IniPath $parsed['general/target_assembly'] $game) -eq [IO.Path]::GetFullPath((Join-Path $game 'BepInEx\core\BepInEx.Unity.IL2CPP.dll'))) 'Portable loader resolution failed'
    $external=Join-Path $root 'Rai Pal external\BepInEx\core\BepInEx.Unity.IL2CPP.dll'
    Check ((Resolve-IniPath $external $game) -eq $external) 'Existing external loader redirected'
    $backup=Join-Path $root 'backup';[void][IO.Directory]::CreateDirectory($backup)
    $old=Join-Path $backup '0.bak';$dest=Join-Path $game 'plugin.dll'
    [IO.File]::WriteAllText($old,'previous');[IO.File]::WriteAllText($dest,'installed')
    $entry=[pscustomobject]@{destination=$dest;existed=$true;backup='0.bak';previousHash=(Hash $old);installedHash=(Hash $dest)}
    Check (Restore-Entry $entry $backup) 'Unchanged installation not restored'
    Check (([IO.File]::ReadAllText($dest)) -eq 'previous') 'Previous content lost'
    [IO.File]::WriteAllText($dest,'user update')
    Check (-not (Restore-Entry $entry $backup)) 'Modified user file overwritten'
    Check (([IO.File]::ReadAllText($dest)) -eq 'user update') 'User update lost'
    $entry.existed=$false;$entry.installedHash=Hash $dest
    Check (Restore-Entry $entry $backup) 'New owned file not removed';Check (-not (Test-Path -LiteralPath $dest)) 'New file remained'
    # Exercise the exact replacement helper under PowerShell (including binding).
    $tmp=$dest+'.tmp';$swap=$dest+'.previous'
    [IO.File]::WriteAllText($dest,'before');[IO.File]::WriteAllText($tmp,'after')
    Replace-StagedFile $tmp $dest $swap
    Check (([IO.File]::ReadAllText($dest)) -eq 'after') 'Replacement did not install update'
    Check (([IO.File]::ReadAllText($swap)) -eq 'before') 'Replacement lost old file'
    $rejected=$false;try { Replace-StagedFile $tmp $dest $null } catch { $rejected=$true }
    Check $rejected 'Empty backup path accepted'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive=Join-Path $root 'traversal.zip'
    $zip=[IO.Compression.ZipFile]::Open($archive,[IO.Compression.ZipArchiveMode]::Create)
    try { $entryZip=$zip.CreateEntry('../escape.txt');$writer=[IO.StreamWriter]::new($entryZip.Open());$writer.Write('escape');$writer.Dispose() } finally { $zip.Dispose() }
    $unpack=Join-Path $root 'unpack';[void][IO.Directory]::CreateDirectory($unpack)
    $rejected=$false;try { Extract-Safe $archive $unpack } catch { $rejected=$true }
    Check $rejected 'Archive traversal not rejected';Check (-not (Test-Path -LiteralPath (Join-Path $root 'escape.txt'))) 'Archive wrote outside staging'
    Write-Host 'PASS: parser; OpenComposite DLL check (64-bit, its own DLL); leftover Doorstop winhttp.dll recognised, another program''s refused; old extractor and audio helpers retired, the log collector kept; path boundaries; portable/external INI; rollback; user modification preservation; archive traversal.'
    Write-Host 'Does not launch the game, access the network or test the full Windows installer.'
} finally { Remove-Item -LiteralPath $root -Recurse -Force }
