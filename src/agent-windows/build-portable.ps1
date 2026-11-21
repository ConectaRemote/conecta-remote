#requires -Version 5.1
<#
    Gera o pacote portátil do ConectaRemote para Windows 10 e 11 (64 bits).

    Uso:
        pwsh ./build-portable.ps1
        pwsh ./build-portable.ps1 -Version 1.0.1
        pwsh ./build-portable.ps1 -CertificatePfx C:\cert.pfx -CertificatePassword "senha"

    Saída:
        dist/ConectaRemotePortable-win-x64.zip
        dist/checksums.txt
        dist/version.txt
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$CertificatePfx = "",
    [string]$CertificatePassword = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "ConectaRemotePortable.csproj"
$dist = Join-Path $root "dist"
$publish = Join-Path $dist "publish"
$zip = Join-Path $dist "ConectaRemotePortable-win-x64.zip"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "O SDK do .NET 8 não foi encontrado. Instale em https://dotnet.microsoft.com/download/dotnet/8.0 e rode novamente."
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
New-Item -ItemType Directory -Path $publish -Force | Out-Null

Write-Host "Publicando self-contained (win-x64, arquivo único)..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:Version=$Version `
    -o $publish

if ($LASTEXITCODE -ne 0) { throw "A publicação falhou (código $LASTEXITCODE)." }

Get-ChildItem $publish -Include *.pdb, *.xml -File -Recurse | Remove-Item -Force
Copy-Item (Join-Path $root "appsettings.json") $publish -Force
Copy-Item (Join-Path $root "README.md") $publish -Force

$exe = Join-Path $publish "ConectaRemotePortable.exe"
if (-not (Test-Path $exe)) { throw "O executável não foi gerado em $exe." }

if ($CertificatePfx) {
    if (-not (Test-Path $CertificatePfx)) { throw "Certificado não encontrado: $CertificatePfx" }

    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1

    if (-not $signtool) { throw "signtool.exe não encontrado. Instale o Windows SDK ou compile sem assinatura." }

    Write-Host "Assinando o executável..." -ForegroundColor Cyan
    & $signtool.FullName sign /fd SHA256 /f $CertificatePfx /p $CertificatePassword `
        /tr http://timestamp.digicert.com /td SHA256 $exe

    if ($LASTEXITCODE -ne 0) { throw "A assinatura falhou (código $LASTEXITCODE)." }
}
else {
    Write-Warning "Pacote sem assinatura digital: o Windows vai exibir o aviso do SmartScreen para quem baixar."
}

Write-Host "Compactando..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publish "*") -DestinationPath $zip -Force

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
$size = (Get-Item $zip).Length

"$hash  $(Split-Path $zip -Leaf)" | Set-Content (Join-Path $dist "checksums.txt") -Encoding ascii
"version=$Version`nsize=$size`nsha256=$hash" | Set-Content (Join-Path $dist "version.txt") -Encoding utf8

Write-Host ""
Write-Host "Pacote gerado: $zip" -ForegroundColor Green
Write-Host "Tamanho: $([math]::Round($size / 1MB, 1)) MB"
Write-Host "SHA-256: $hash"
Write-Host ""
Write-Host "Publique o ZIP, o checksums.txt e o version.txt como release (tag portable-v...)." -ForegroundColor Yellow
Write-Host "O site libera o download sozinho quando o release existir e o hash for valido." -ForegroundColor Yellow