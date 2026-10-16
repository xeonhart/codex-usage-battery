$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework C# compiler was not found.'
}

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    "/win32icon:$root\CodexUsageBattery.ico" `
    "/out:$root\CodexUsageBattery.exe" `
    /reference:System.Windows.Forms.dll `
    /reference:System.Drawing.dll `
    /reference:System.Web.Extensions.dll `
    "$root\CodexUsageBattery.cs"

if ($LASTEXITCODE -ne 0) { throw "C# build failed with exit code $LASTEXITCODE." }
Write-Host "Built $root\CodexUsageBattery.exe"
