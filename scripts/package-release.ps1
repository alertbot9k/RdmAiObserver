$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot\..").Path
$release = Join-Path $root 'SamplePlugin\bin\x64\Release'
if (-not (Test-Path -LiteralPath (Join-Path $release 'SamplePlugin.dll'))) {
    throw 'Release plugin DLL is missing. Build SamplePlugin.slnx first.'
}
$props = [xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)
$version = $props.Project.PropertyGroup.Version
$artifactDirectory = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
$archive = Join-Path $artifactDirectory "RdmAiObserver-$version.zip"
$files = Get-ChildItem -LiteralPath $release -File | Where-Object { $_.Extension -in '.dll', '.json', '.pdb' }
Compress-Archive -LiteralPath $files.FullName -DestinationPath $archive -Force
Write-Host "Packaged $archive"
