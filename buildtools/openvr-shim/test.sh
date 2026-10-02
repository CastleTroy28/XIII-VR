#!/bin/sh
# Runs shimtest.c (Linux) against the built OpenXR/openvr_api.dll: the runtime choice (SteamVR,
# OpenComposite, fallbacks) and every adapter slot for both OpenComposite generations (IVRCompositor_028/_027).
set -e
cd "$(dirname "$0")"
clang -O1 -Wall -Wno-unused-function -o shimtest shimtest.c
for s in oc oc027 steam xrenv forced nooc none; do ./shimtest ../../OpenXR/openvr_api.dll $s; done
rm -f shimtest
