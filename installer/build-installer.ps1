# Publica o VisualSubSyncNET autocontido para Windows x64 e gera o instalador com o NSIS 3.
#   powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
# O instalador fica em installer\Redist\VisualSubSyncNET-<versão>-Setup.exe.
param(
    # makensis.exe; por padrão o da instalação do NSIS em Program Files (x86).
    [string]$MakeNsis = (Join-Path ${env:ProgramFiles(x86)} 'NSIS\makensis.exe'),
    # Pula o dotnet publish e usa a publicação que já está em publish\VisualSubSyncNET-win-x64.
    [switch]$NoPublish,
    # Permite empacotar uma publicação por versão sem sobrescrever outra em uso.
    [string]$PublishDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$appName = 'VisualSubSyncNET'
$publish = if (!$PublishDirectory) { Join-Path $root "publish\$appName-win-x64" }
           elseif ([System.IO.Path]::IsPathRooted($PublishDirectory)) { $PublishDirectory }
           else { Join-Path $root $PublishDirectory }
$publish = [System.IO.Path]::GetFullPath($publish).TrimEnd('\')

if (-not $NoPublish) {
    dotnet publish (Join-Path $root 'src\VisualSubSync.App') -c Release -r win-x64 --self-contained -o $publish
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou.' }
}
if (-not (Test-Path (Join-Path $publish 'libmpv-2.dll'))) {
    throw 'libmpv-2.dll não está na publicação. Rode poc\obter-libmpv.ps1 e publique de novo.'
}

# Versão do Directory.Build.props (o nome do instalador e a legenda da janela).
[xml]$props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ([string]$props.Project.PropertyGroup.Version).Trim()
$assembly = Join-Path $publish "$appName.dll"
if (!(Test-Path -LiteralPath $assembly)) { throw "Publicação incompleta: $assembly não encontrado." }
$publishedVersion = ([System.Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).ProductVersion -split '\+')[0]
if ($publishedVersion -ne $version) {
    throw "A publicação é $publishedVersion, mas o projeto é $version. Publique novamente ou informe -PublishDirectory correto."
}
if (-not (Test-Path $MakeNsis)) {
    throw "makensis.exe não encontrado em $MakeNsis. Instale o NSIS 3 (https://nsis.sourceforge.io) ou passe -MakeNsis."
}

# files.nsh: um File por arquivo publicado (sem .pdb) e, para desinstalar, um Delete por arquivo e
# um RMDir por pasta, das mais fundas para as de cima.
$files = Get-ChildItem $publish -Recurse -File |
    Where-Object { $_.Extension -ne '.pdb' } |
    ForEach-Object { $_.FullName.Substring($publish.Length + 1) } |
    Sort-Object
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("; Gerado por build-installer.ps1 a partir de publish\$appName-win-x64. Não editar.")
$lines.Add('!macro VSS_INSTALL_FILES')
$dirs = [System.Collections.Generic.SortedSet[string]]::new()
foreach ($group in ($files | Group-Object { Split-Path -Parent $_ } | Sort-Object Name)) {
    $dir = $group.Name
    $lines.Add('  SetOutPath "$INSTDIR' + $(if ($dir) { '\' + $dir } else { '' }) + '"')
    foreach ($f in $group.Group) { $lines.Add('  File "${INPUT_PATH}' + $f + '"') }
    while ($dir) { [void]$dirs.Add($dir); $dir = Split-Path -Parent $dir }
}
$lines.Add('!macroend')
$lines.Add('!macro VSS_UNINSTALL_FILES')
foreach ($f in $files) { $lines.Add('  Delete "$INSTDIR\' + $f + '"') }
foreach ($d in ($dirs | Sort-Object { ($_ -split '\\').Count } -Descending)) { $lines.Add('  RMDir "$INSTDIR\' + $d + '"') }
$lines.Add('!macroend')
[System.IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'files.nsh'), $lines, [System.Text.UTF8Encoding]::new($true))

New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'Redist') | Out-Null
& $MakeNsis "/DVERSION=$version" "/DINPUT_PATH=$publish\" (Join-Path $PSScriptRoot 'VisualSubSync.nsi')
if ($LASTEXITCODE -ne 0) { throw 'makensis falhou.' }
