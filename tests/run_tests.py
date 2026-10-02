#!/usr/bin/env python3
"""Builds and runs the mod's tests with the C# compiler of the .NET SDK.

Every test is a small console program: tests/<Name>.cs together with the
files it checks (tests/test_sets.json) and its own stand-ins for Unity and
the game, so no game files are needed. Two checks read the built plugin and
the game's interop assemblies; they run only when --plugin and --bepinex are
given.

  python tests/run_tests.py                        all tests that need no game files
  python tests/run_tests.py KeyInteractionTests    only the named ones
  python tests/run_tests.py --plugin src/bin/Release/net6.0/XIII.XRBootstrap.dll --bepinex "D:/Games/XIII/BepInEx"

Needs: Python 3.8+, the .NET SDK 6 or newer (dotnet on PATH or DOTNET_ROOT).
The installer's checks are separate: pwsh tests/Installer.Tests.ps1
"""
import argparse, json, os, re, shutil, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / '.testbuild'
GAME_SETS = {'VerifyCompiledDll', 'InteropApiTests'}


def version(name):
    parts = re.findall(r'\d+', name)
    return tuple(int(p) for p in parts) if parts else (0,)


def find_dotnet():
    home = os.environ.get('DOTNET_ROOT')
    exe = None
    if home:
        for name in ('dotnet', 'dotnet.exe'):
            if (Path(home) / name).exists():
                exe = Path(home) / name
    if exe is None:
        found = shutil.which('dotnet')
        if not found:
            sys.exit('dotnet not found: install the .NET SDK (6 or newer) or set DOTNET_ROOT.')
        exe = Path(found).resolve()
    root = exe.parent
    sdks = sorted((d for d in (root / 'sdk').glob('*') if (d / 'Roslyn' / 'bincore' / 'csc.dll').exists()), key=lambda d: version(d.name))
    if not sdks:
        sys.exit('No .NET SDK with a C# compiler under ' + str(root / 'sdk'))
    csc = sdks[-1] / 'Roslyn' / 'bincore' / 'csc.dll'
    runtimes = sorted((d for d in (root / 'shared' / 'Microsoft.NETCore.App').glob('*') if d.is_dir()), key=lambda d: version(d.name))
    if not runtimes:
        sys.exit('No .NET runtime under ' + str(root / 'shared'))
    newest = version(runtimes[-1].name)[0]
    packs = []
    for pack in (root / 'packs' / 'Microsoft.NETCore.App.Ref').glob('*'):
        for ref in pack.glob('ref/net*'):
            major = version(ref.name)[0]
            if major <= newest and any(ref.glob('*.dll')):
                packs.append((version(pack.name), major, ref))
    if not packs:
        sys.exit('No Microsoft.NETCore.App reference pack under ' + str(root / 'packs'))
    _, major, refs = sorted(packs)[-1]
    return exe, csc, refs, major


def compile_set(dotnet, csc, refs, major, name, sources, extra, language):
    OUT.mkdir(exist_ok=True)
    out = OUT / (name + '.dll')
    args = ['-nologo', '-langversion:' + language.get('langversion', '10'), '-nullable:' + language.get('nullable', 'enable'),
            '-unsafe+', '-deterministic+', '-target:exe', '-out:' + str(out)]
    args += ['-r:' + str(p) for p in sorted(refs.glob('*.dll'))]
    args += ['-r:' + str(p) for p in extra]
    args += [str(p) for p in sources]
    rsp = OUT / (name + '.rsp')
    rsp.write_text('\n'.join('"' + a + '"' if ' ' in a else a for a in args) + '\n', encoding='utf-8')
    built = subprocess.run([str(dotnet), str(csc), '@' + str(rsp)], cwd=ROOT, capture_output=True, text=True)
    if built.returncode:
        return None, (built.stdout + built.stderr).strip()
    config = {'runtimeOptions': {'tfm': 'net%d.0' % major, 'rollForward': 'LatestMajor',
                                 'framework': {'name': 'Microsoft.NETCore.App', 'version': '%d.0.0' % major}}}
    (OUT / (name + '.runtimeconfig.json')).write_text(json.dumps(config), encoding='utf-8')
    for dll in extra:
        shutil.copy2(dll, OUT / Path(dll).name)
    return out, ''


def main():
    parser = argparse.ArgumentParser(description='Build and run the XIII VR tests.')
    parser.add_argument('names', nargs='*', help='test sets to run (default: all)')
    parser.add_argument('--plugin', help='the built XIII.XRBootstrap.dll (for VerifyCompiledDll and InteropApiTests)')
    parser.add_argument('--bepinex', help="the game's BepInEx folder (its core and interop folders)")
    parser.add_argument('--list', action='store_true', help='list the test sets')
    options = parser.parse_args()
    sets = json.loads((ROOT / 'tests' / 'test_sets.json').read_text(encoding='utf-8'))
    if options.list:
        for s in sets:
            print(s['name'] + (' (needs --plugin and --bepinex)' if s['name'] in GAME_SETS else ''))
        return 0
    unknown = set(options.names) - {s['name'] for s in sets}
    if unknown:
        sys.exit('Unknown test sets: ' + ', '.join(sorted(unknown)))
    dotnet, csc, refs, major = find_dotnet()
    print('compiler: %s; references: %s' % (csc, refs))
    # The tests write their files to xiii-xr/build and read the plugin from
    # xiii-xr/XIII.XRBootstrap.dll, relative to their working folder.
    work = OUT / 'work'
    (work / 'xiii-xr' / 'build').mkdir(parents=True, exist_ok=True)
    game = bool(options.plugin and options.bepinex)
    if game:
        shutil.copy2(options.plugin, work / 'xiii-xr' / 'XIII.XRBootstrap.dll')
    failed, skipped, passed = [], [], 0
    for s in sets:
        name = s['name']
        if options.names and name not in options.names:
            continue
        if name in GAME_SETS and not game:
            skipped.append(name)
            continue
        sources = [ROOT / 'tests' / (name + '.cs')] + [ROOT / f for f in s['files']]
        extra = [Path(options.bepinex) / 'core' / 'Mono.Cecil.dll'] if name in GAME_SETS else []
        exe, error = compile_set(dotnet, csc, refs, major, name, sources, extra, s)
        if exe is None:
            print('%s: COMPILE FAILED\n%s' % (name, error))
            failed.append(name)
            continue
        args = []
        if name in GAME_SETS:
            args = [str(Path(options.plugin).resolve()), str(Path(options.bepinex).resolve())]
        run = subprocess.run([str(dotnet), str(exe)] + args, cwd=work, capture_output=True, text=True)
        output = (run.stdout + run.stderr).strip()
        print('%s: %s' % (name, 'ok' if run.returncode == 0 else 'FAILED (exit %d)' % run.returncode))
        if run.returncode or os.environ.get('XIII_TEST_VERBOSE'):
            print(output)
        if run.returncode:
            failed.append(name)
        else:
            passed += 1
    print('\n%d passed, %d failed, %d skipped' % (passed, len(failed), len(skipped)))
    if skipped:
        print('skipped (need --plugin and --bepinex): ' + ', '.join(skipped))
    if failed:
        print('failed: ' + ', '.join(failed))
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
