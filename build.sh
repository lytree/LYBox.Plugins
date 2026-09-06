#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet build/build.cs -- "$@"
