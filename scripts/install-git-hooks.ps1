<#
Install git hooks by setting core.hooksPath to .githooks in the repository config.
Run: ./scripts/install-git-hooks.ps1
#>
param(
	[string]$RepoRoot = (Get-Location).Path
)

Set-Location -Path $RepoRoot

if (-not (Test-Path -Path (Join-Path $RepoRoot '.githooks\pre-commit'))) {
	Write-Host "No .githooks/pre-commit found. Ensure .githooks/pre-commit exists." -ForegroundColor Yellow
}

git config core.hooksPath .githooks
Write-Host "core.hooksPath set to .githooks. Pre-commit hook installed." -ForegroundColor Green
