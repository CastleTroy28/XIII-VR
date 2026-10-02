#!/bin/sh
# Runs helpertest.c (Linux) against the built OpenXR/xiii_openxr.dll with a fake OpenXR runtime.
# The OpenXR header's function pointer types are made Microsoft-ABI for the test (a copy in a temp dir).
set -e
cd "$(dirname "$0")"
T=$(mktemp -d)
mkdir -p "$T/openxr"
cp include/openxr/openxr.h "$T/openxr/"
python3 - "$T/openxr/openxr_platform_defines.h" <<'PY'
import re,sys
t=open('include/openxr/openxr_platform_defines.h').read()
a=t.index('#if defined(_WIN32)\n#define XRAPI_ATTR');b=t.index('#endif',t.index('#define XRAPI_PTR\n'))+len('#endif')
t=t[:a]+'#define XRAPI_ATTR __attribute__((ms_abi))\n#define XRAPI_CALL\n#define XRAPI_PTR __attribute__((ms_abi))'+t[b:]
open(sys.argv[1],'w').write(t)
PY
clang -O1 -Wall -Wno-unused-function -I"$T" -o "$T/helpertest" helpertest.c -lm
for s in touch index vive; do "$T/helpertest" ../../GameFolder/XIII_Data/Plugins/xiii_openxr.dll $s; done
rm -rf "$T"
