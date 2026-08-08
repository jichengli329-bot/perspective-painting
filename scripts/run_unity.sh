#!/usr/bin/env bash
# Local helper to run the installed Unity editor from this repo.
# Used only for T-001 batch import/bootstrap verification.
set -euo pipefail
exec "C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe" "$@"
