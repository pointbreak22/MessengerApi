<#
Generate-CodeMap.ps1
Generates CODEMAP.json summarizing repository structure for agent use.

Usage: run from repository root (or execute the script directly). It writes CODEMAP.json in the repo root.
#>

Param(
	[string]$RootPath = (Split-Path -Parent $MyInvocation.MyCommand.Path)
)

Write-Host "Generating CODEMAP.json in: $RootPath"

function FirstLine([string]$path){
	if (Test-Path $path){
		return (Get-Content $path -TotalCount 1 -ErrorAction SilentlyContinue) -join ''
	}
	return ''
}

$map = [ordered]@{
	repo = Split-Path $RootPath -Leaf
	generated = (Get-Date).ToString("o")
	projects = @()
}

$csprojs = Get-ChildItem -Path $RootPath -Recurse -Filter *.csproj -ErrorAction SilentlyContinue
foreach($p in $csprojs){
	$projPath = (Split-Path $p.FullName -Parent) -replace [regex]::Escape($RootPath+'\\'), ''
	$projName = $p.BaseName
	$type = if ($projName -match 'WebAPI') { 'web' } elseif ($projName -match 'Infrastructure') { 'infrastructure' } elseif ($projName -match 'Domain') { 'domain' } elseif ($projName -match 'Application') { 'application' } elseif ($projName -match 'Test' -or $projName -match 'Tests') { 'tests' } else { 'library' }

	$proj = [ordered]@{
		name = $projName
		path = $projPath
		type = $type
		keyFiles = @()
	}

	# collect some key files by patterns
	$patterns = @('Program.cs','**/Hubs/*.cs','**/Controllers/*.cs','**/Entities/*.cs','**/Persistence/*.cs','**/DependencyInjection.cs','**/Commands/*.cs','**/Handlers/*.cs')
	foreach($pat in $patterns){
		$files = Get-ChildItem -Path $RootPath -Recurse -Include $pat -ErrorAction SilentlyContinue | Select-Object -First 5
		foreach($f in $files){
			$rel = $f.FullName -replace [regex]::Escape($RootPath+'\\'), ''
			$proj.keyFiles += [ordered]@{ path = $rel; role = (Split-Path $f.DirectoryName -Leaf) ; head = (FirstLine $f.FullName) }
		}
	}

	$map.projects += $proj
}

# DI/MapHub summary (search Program.cs & DependencyInjection.cs)
$diSummary = @()
$programs = Get-ChildItem -Path $RootPath -Recurse -Filter Program.cs -ErrorAction SilentlyContinue
foreach($pc in $programs){
	$lines = Select-String -Path $pc.FullName -Pattern 'AddScoped|AddSingleton|AddSignalR|AddMediatR|MapHub|UseNpgsql|AppContext.SetSwitch' -SimpleMatch -AllMatches | ForEach-Object { $_.Line.Trim() }
	if ($lines) { $diSummary += [ordered]@{ file = ($pc.FullName -replace [regex]::Escape($RootPath+'\\'), ''); matches = $lines } }
}

$depFile = Join-Path $RootPath 'CODEMAP.json'

$out = [ordered]@{
	repo = $map.repo
	generated = $map.generated
	projects = $map.projects
	diSummary = $diSummary
}

$json = $out | ConvertTo-Json -Depth 7
[System.IO.File]::WriteAllText($depFile, $json, [System.Text.Encoding]::UTF8)

Write-Host "CODEMAP.json generated." -ForegroundColor Green
