param([switch]$SkipPluginBuild)
$ErrorActionPreference = 'Stop'
$props = [xml](Get-Content -LiteralPath "$PSScriptRoot\..\Directory.Build.props" -Raw)
$version = $props.Project.PropertyGroup.Version
$milestone = Get-Content -LiteralPath "$PSScriptRoot\..\SamplePlugin\BuildInfo.cs" -Raw
if ([string]::IsNullOrWhiteSpace($version)) { throw 'Central Version is missing.' }
if ($milestone -notmatch 'Milestone = "[^"]+"') { throw 'Build milestone is missing.' }
dotnet test "$PSScriptRoot\..\RdmAiObserver.Core.slnx" --configuration Release --no-restore
if (-not $SkipPluginBuild) {
    dotnet build "$PSScriptRoot\..\SamplePlugin.slnx" --configuration Release --no-restore
    $dll = "$PSScriptRoot\..\SamplePlugin\bin\x64\Release\SamplePlugin.dll"
    $actual = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).FileVersion
    if (-not $actual.StartsWith($version)) { throw "DLL version $actual does not match $version." }
}
Write-Host "Release verification passed for $version."
