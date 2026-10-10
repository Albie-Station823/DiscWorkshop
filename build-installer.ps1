$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build.ps1"
$compiler = Join-Path $PSScriptRoot 'vendor\inno\ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Install Inno Setup and set the compiler path in build-installer.ps1.' }
& $compiler "$PSScriptRoot\installer.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
$package=Join-Path $PSScriptRoot 'release\DiscWorkshop-Setup-1.8.3.exe'
$hash=Get-FileHash -LiteralPath $package -Algorithm SHA256
Set-Content -LiteralPath "$package.sha256" -Value ($hash.Hash.ToLower()+'  '+[IO.Path]::GetFileName($package)) -Encoding ascii



