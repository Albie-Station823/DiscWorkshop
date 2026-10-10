$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /out:"$PSScriptRoot\MakeAssets.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\Branding.cs" "$PSScriptRoot\MakeAssets.cs"
if ($LASTEXITCODE -ne 0) { throw 'Asset compilation failed' }
& "$PSScriptRoot\MakeAssets.exe" "$PSScriptRoot\assets"
if ($LASTEXITCODE -ne 0) { throw 'Asset generation failed' }
& $compiler /nologo /target:winexe /optimize+ /win32icon:"$PSScriptRoot\assets\DiscWorkshop.ico" /out:"$PSScriptRoot\DiscWorkshop-1.8.3.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\DiscWorkshop.cs" "$PSScriptRoot\DiscPlanning.cs" "$PSScriptRoot\MovieOptions.cs" "$PSScriptRoot\WorkshopUI.cs" "$PSScriptRoot\BreakEditor.cs" "$PSScriptRoot\PreviewPlayback.cs" "$PSScriptRoot\PreviewFrames.cs" "$PSScriptRoot\EncodingOptions.cs" "$PSScriptRoot\Branding.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
Write-Output "Built $PSScriptRoot\DiscWorkshop-1.8.3.exe"







