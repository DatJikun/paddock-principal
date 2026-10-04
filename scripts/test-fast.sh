#!/usr/bin/env bash
# Local default: skip tests marked [Trait("Category", "Slow")]. CI runs the full suite.
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
dotnet test --filter "Category!=Slow"
