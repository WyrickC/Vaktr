param(
    [string]$Project = "C:\Repos\Vaktr\Vaktr.App\Vaktr.App.csproj",
    [string]$Platform = "x64",
    [switch]$StopVaktr
)

$ErrorActionPreference = "Stop"

if ($StopVaktr) {
    Get-Process Vaktr -ErrorAction SilentlyContinue | Stop-Process -Force
}

$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    throw "MSBuild.exe was not found at '$msbuild'."
}

& $msbuild $Project /p:Platform=$Platform /p:Restore=false /clp:ErrorsOnly
exit $LASTEXITCODE
