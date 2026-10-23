# Baixa a libmpv-2.dll (build de shinchiro/mpv-winbuild-cmake) para poc\lib.
# Uso: powershell -ExecutionPolicy Bypass -File poc\obter-libmpv.ps1
param([string]$Destino = (Join-Path $PSScriptRoot 'lib'))
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

New-Item -ItemType Directory -Force $Destino | Out-Null
$rel = Invoke-RestMethod 'https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest' -Headers @{ 'User-Agent' = 'VisualSubSync' }
$asset = $rel.assets | Where-Object { $_.name -match '^mpv-dev-x86_64-\d{8}-git-[0-9a-f]+\.7z$' } | Select-Object -First 1
if (-not $asset) { throw "Pacote mpv-dev-x86_64 não encontrado na versão $($rel.tag_name)." }

$arquivo = Join-Path $env:TEMP $asset.name
Write-Host "Baixando $($asset.name)..."
Invoke-WebRequest $asset.browser_download_url -OutFile $arquivo

$tmp = Join-Path $env:TEMP ('mpv-dev-' + [guid]::NewGuid())
New-Item -ItemType Directory $tmp | Out-Null
$seven = @("$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($seven) { & $seven x $arquivo "-o$tmp" -y | Out-Null } else { tar -xf $arquivo -C $tmp }

$dll = Get-ChildItem $tmp -Recurse -Filter 'libmpv-2.dll' | Select-Object -First 1
if (-not $dll) { throw 'libmpv-2.dll não encontrada no pacote. Instale o 7-Zip (winget install 7zip.7zip) e rode de novo.' }
Copy-Item $dll.FullName $Destino -Force
Remove-Item $tmp -Recurse -Force
Remove-Item $arquivo -Force
Write-Host "libmpv-2.dll ($($rel.tag_name)) copiada para $Destino"
