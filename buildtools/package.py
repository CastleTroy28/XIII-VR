#!/usr/bin/env python3
"""Builds the player package: the zip attached to a GitHub release.

  python buildtools/package.py --plugin src/bin/Release/net6.0/XIII.XRBootstrap.dll

The mod's two native DLLs (xiii_openxr.dll, openvr_api.dll) are built first
with buildtools/*/build.sh (clang and lld-link) unless --no-native is given
and they are already built. Writes dist/XIII-VR-<version>.zip with the
installer, the plugin (as BepInEx-plugins/XIII.XRBootstrap.dll.bin, which
BepInEx never loads by itself), the files for the game folder, the licenses
and SHA256.txt.
"""
import argparse, hashlib, re, shutil, subprocess, sys, zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NATIVE = {
    'buildtools/openxr-helper/build.sh': 'GameFolder/XIII_Data/Plugins/xiii_openxr.dll',
    'buildtools/openvr-shim/build.sh': 'OpenXR/openvr_api.dll',
}
FILES = ['Install-XIII-VR.cmd', 'Install-XIII-VR.ps1', 'Restore-XIII-VR.cmd', 'README.md', 'CHANGELOG.md',
         'LICENSE', 'LICENSE-Valve.txt', 'THIRD-PARTY.txt']
FOLDERS = ['Fresh-config', 'GameFolder', 'OpenXR']


def version_of(path, pattern):
    match = re.search(pattern, path.read_text(encoding='utf-8-sig'))
    if not match:
        sys.exit('No version found in ' + str(path))
    return match.group(1)


def main():
    parser = argparse.ArgumentParser(description='Build the XIII VR player package.')
    parser.add_argument('--plugin', required=True, help='the built XIII.XRBootstrap.dll')
    parser.add_argument('--no-native', action='store_true', help='use the native DLLs already built')
    parser.add_argument('--out', default=str(ROOT / 'dist'), help='output folder (default: dist)')
    options = parser.parse_args()

    version = version_of(ROOT / 'Install-XIII-VR.ps1', r"\$Version = '([0-9.]+)'")
    for path, pattern in ((ROOT / 'src' / 'Plugin.cs', r'BepInPlugin\("[^"]+", "[^"]+", "([0-9.]+)"\)'),
                          (ROOT / 'src' / 'XIII.XRBootstrap.csproj', r'<Version>([0-9.]+)</Version>')):
        if version_of(path, pattern) != version:
            sys.exit('Version mismatch: %s says %s, Install-XIII-VR.ps1 says %s' % (path.name, version_of(path, pattern), version))
    plugin = Path(options.plugin)
    if not plugin.is_file():
        sys.exit('Plugin not found: ' + str(plugin))

    for script, output in NATIVE.items():
        if options.no_native and (ROOT / output).is_file():
            continue
        subprocess.run(['sh', str(ROOT / script)], check=True)
        if not (ROOT / output).is_file():
            sys.exit(script + ' did not produce ' + output)

    stage = Path(options.out) / ('XIII-VR-' + version)
    if stage.exists():
        shutil.rmtree(stage)
    stage.mkdir(parents=True)
    for name in FILES:
        shutil.copy2(ROOT / name, stage / name)
    for name in FOLDERS:
        shutil.copytree(ROOT / name, stage / name)
    (stage / 'BepInEx-plugins').mkdir()
    shutil.copy2(plugin, stage / 'BepInEx-plugins' / 'XIII.XRBootstrap.dll.bin')

    files = sorted(p for p in stage.rglob('*') if p.is_file())
    lines = ['%s  %s' % (hashlib.sha256(p.read_bytes()).hexdigest(), p.relative_to(stage).as_posix()) for p in files]
    (stage / 'SHA256.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')

    archive = Path(options.out) / ('XIII-VR-%s.zip' % version)
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as z:
        for p in sorted(p for p in stage.rglob('*') if p.is_file()):
            z.write(p, p.relative_to(stage).as_posix())
    print('%s (%d files, plugin SHA256 %s)' % (archive, len(files) + 1, hashlib.sha256(plugin.read_bytes()).hexdigest()))
    return 0


if __name__ == '__main__':
    sys.exit(main())
