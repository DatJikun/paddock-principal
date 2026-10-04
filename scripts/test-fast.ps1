# Local default: skip tests marked [Trait("Category", "Slow")]. CI runs the full suite.
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
dotnet test --filter "Category!=Slow"
