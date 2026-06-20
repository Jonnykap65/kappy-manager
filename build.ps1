[CmdletBinding()]
param(
    [switch]$Run
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $projectRoot 'KappyManager.cs'
$iconSource = Join-Path $projectRoot 'IconGenerator.cs'
$iconGenerator = Join-Path $env:TEMP 'KappyManager.IconGenerator.exe'
$icon = Join-Path $projectRoot 'Kappy Manager.ico'
$output = Join-Path $projectRoot 'Kappy Manager.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The .NET Framework C# compiler was not found at '$compiler'."
}

Write-Host 'Building Kappy Manager...'
& $compiler /nologo /target:exe /optimize+ `
    /out:"$iconGenerator" `
    /reference:System.dll `
    /reference:System.Drawing.dll `
    $iconSource

if ($LASTEXITCODE -ne 0) {
    throw "Icon build failed with exit code $LASTEXITCODE."
}

& $iconGenerator $icon
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $icon)) {
    throw "Icon generation failed with exit code $LASTEXITCODE."
}

& $compiler /nologo /target:winexe /optimize+ /platform:x64 `
    /out:"$output" `
    /win32icon:"$icon" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.ServiceProcess.dll `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Built: $output"

if ($Run) {
    Start-Process -FilePath $output
}
