#!/bin/sh
# Builds openvr_api.dll (the runtime switch: SteamVR or OpenComposite, see shim.c) with clang + lld-link; no Windows SDK needed.
# gen.py (maps.json -> adapters.inc, thunks.S) runs first.
set -e
cd "$(dirname "$0")"
python3 gen.py
llvm-dlltool -m i386:x86-64 -d kernel32.def -l kernel32.lib
llvm-dlltool -m i386:x86-64 -d advapi32.def -l advapi32.lib
clang --target=x86_64-pc-windows-msvc -O2 -ffreestanding -fno-builtin -fno-stack-protector -fno-asynchronous-unwind-tables -Wall -Wextra -Werror -Wno-unused-parameter -Wno-missing-field-initializers -c shim.c -o shim.obj
clang --target=x86_64-pc-windows-msvc -c thunks.S -o thunks.obj
# A fixed link time: the same source and toolchain give the same DLL, byte for byte.
lld-link /timestamp:1790655352 /dll /noentry /nodefaultlib /machine:x64 /def:shim.def /out:openvr_api.dll shim.obj thunks.obj kernel32.lib advapi32.lib
rm -f shim.obj thunks.obj kernel32.lib advapi32.lib openvr_api.lib openvr_api.exp
mkdir -p ../../OpenXR && mv -f openvr_api.dll ../../OpenXR/openvr_api.dll
