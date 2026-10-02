#!/bin/sh
# Builds xiii_openxr.dll (the mod's OpenXR input/tracking helper, see helper.c) with clang + lld-link;
# no Windows SDK, no C runtime, no imports. Output: ../../GameFolder/XIII_Data/Plugins/xiii_openxr.dll
set -e
cd "$(dirname "$0")"
clang --target=x86_64-pc-windows-msvc -O2 -ffreestanding -fno-builtin -fno-stack-protector -fno-asynchronous-unwind-tables -Iinclude -Wall -Wextra -Werror -Wno-unused-parameter -Wno-missing-field-initializers -c helper.c -o helper.obj
# A fixed link time: the same source and toolchain give the same DLL, byte for byte.
lld-link /timestamp:1790657681 /dll /noentry /nodefaultlib /machine:x64 /def:xiii_openxr.def /out:xiii_openxr.dll helper.obj
rm -f helper.obj xiii_openxr.lib xiii_openxr.exp
mkdir -p ../../GameFolder/XIII_Data/Plugins && mv -f xiii_openxr.dll ../../GameFolder/XIII_Data/Plugins/xiii_openxr.dll
