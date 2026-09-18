param(
    [string]$Configuration = "Release",
    [string]$Output = "dist"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\KlondaikTweaker\KlondaikTweaker.csproj"
$dist = Join-Path $root $Output

Write-Host "Generating data files" -ForegroundColor Cyan
Push-Location (Join-Path $root "tools")
python gen_tweaks.py
python gen_wizard.py
python gen_software.py
Pop-Location

Write-Host "Publishing $Configuration" -ForegroundColor Cyan
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
dotnet publish $project -c $Configuration -o $dist --nologo

$exe = Join-Path $dist "KlondaikTweaker.exe"
if (-not (Test-Path $exe)) { throw "publish produced no exe" }

Get-ChildItem $dist -Filter *.pdb | Remove-Item -Force -ErrorAction SilentlyContinue
$vmTest = Join-Path $dist "VM-test"
New-Item -ItemType Directory -Path $vmTest -Force | Out-Null
Copy-Item $exe $vmTest -Force
Copy-Item (Join-Path $root "tools\RUN-TEST.bat") $vmTest -Force

$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host "Done: $exe ($size MB)" -ForegroundColor Green
Write-Host "VM test package: $vmTest" -ForegroundColor Green
