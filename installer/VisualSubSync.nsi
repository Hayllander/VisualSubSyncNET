; ---------------------------------------------------------------------------
; VisualSubSyncNET NSIS install script
; ---------------------------------------------------------------------------
; Segue o VisualSubSync.nsi do 1.0.1: as mesmas páginas, seções, atalhos e chaves do registro.
; A lista de arquivos (files.nsh) é gerada por build-installer.ps1 a partir da publicação
; autocontida, porque o .NET publica centenas de arquivos em vez dos seis do original.

!ifndef VERSION
  !define VERSION "2.0.0"
!endif
!ifndef INPUT_PATH
  !define INPUT_PATH "..\publish\VisualSubSyncNET-win-x64\"
!endif

!define NAME "VisualSubSyncNET"
!define OUTFILE "Redist\VisualSubSyncNET-${VERSION}-Setup.exe"
!define DIR_PRESETS "presets\"
!define UNINST_NAME "VisualSubSyncNET-uninstall.exe"

Unicode true
RequestExecutionLevel admin
SetCompressor /SOLID lzma

!include "x64.nsh"
!include "files.nsh"

;--------------------------------
;Include Modern UI

  !include "MUI.nsh"

;--------------------------------
;Configuration

  ;General
  Name "${NAME}"
  OutFile "${OUTFILE}"
  Caption "${NAME} ${VERSION}"
  BrandingText " "

  ;Folder selection page (programa de 64 bits: Program Files em vez de Program Files (x86))
  InstallDir "$PROGRAMFILES64\${NAME}"

  ;Get install folder from registry if available
  InstallDirRegKey HKCU "Software\${NAME}" ""

;--------------------------------
;Interface Settings

  !define MUI_ABORTWARNING

;--------------------------------
;Pages

  !insertmacro MUI_PAGE_COMPONENTS
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES

  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES

;--------------------------------
;Languages

  !insertmacro MUI_LANGUAGE "English"

;--------------------------------
;Installer Sections

Section "Main (required)" SecMain

  ; Make section read only as it's required
  SectionIn RO

  ; Programa, runtime do .NET, mpv, FFmpeg, web, help, jsplugin e dict
  !insertmacro VSS_INSTALL_FILES

  ; Presets
  SetOutPath "$INSTDIR\${DIR_PRESETS}"
  SetOutPath "$INSTDIR"

  ; Store install folder
  WriteRegStr HKCU "Software\${NAME}" "" $INSTDIR

  ; Create uninstaller
  SetRegView 64
  WriteRegStr HKEY_LOCAL_MACHINE "Software\Microsoft\Windows\CurrentVersion\Uninstall\${NAME}" "DisplayName" "${NAME} (remove only)"
  WriteRegStr HKEY_LOCAL_MACHINE "Software\Microsoft\Windows\CurrentVersion\Uninstall\${NAME}" "UninstallString" '"$INSTDIR\${UNINST_NAME}"'
  WriteUninstaller "$INSTDIR\${UNINST_NAME}"

SectionEnd

;--------------------------------

Section "Start Menu Shortcuts" SecStartShortcut

  SetShellVarContext all
  CreateDirectory "$SMPROGRAMS\${NAME}"
  CreateShortCut "$SMPROGRAMS\${NAME}\Uninstall.lnk" "$INSTDIR\${UNINST_NAME}" "" "$INSTDIR\${UNINST_NAME}" 0
  CreateShortCut "$SMPROGRAMS\${NAME}\Help.lnk" "$INSTDIR\help\index.html"
  CreateShortCut "$SMPROGRAMS\${NAME}\${NAME}.lnk" "$INSTDIR\${NAME}.exe" "" "$INSTDIR\${NAME}.exe" 0

SectionEnd

;--------------------------------
;Descriptions

  LangString DESC_SecMain ${LANG_ENGLISH} "The main program."
  LangString DESC_SecStartShortcut ${LANG_ENGLISH} "Create a shortcut in the start menu."

  !insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} $(DESC_SecMain)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecStartShortcut} $(DESC_SecStartShortcut)
  !insertmacro MUI_FUNCTION_DESCRIPTION_END

;--------------------------------
;Uninstaller Section

Section "Uninstall"

  ; Delete installer
  Delete "$INSTDIR\${UNINST_NAME}"

  ; Delete the installed files and their directories if empty (os dicionários e presets do usuário ficam)
  !insertmacro VSS_UNINSTALL_FILES

  ; Presets
  RMDir "$INSTDIR\${DIR_PRESETS}"

  ; Delete install directory if empty
  RMDir "$INSTDIR"

  ; Delete shortcuts
  SetShellVarContext all
  Delete "$SMPROGRAMS\${NAME}\Uninstall.lnk"
  Delete "$SMPROGRAMS\${NAME}\Help.lnk"
  Delete "$SMPROGRAMS\${NAME}\${NAME}.lnk"
  RMDir "$SMPROGRAMS\${NAME}"

  ; Delete registry keys
  DeleteRegKey /ifempty HKCU "Software\${NAME}"
  SetRegView 64
  DeleteRegKey HKEY_LOCAL_MACHINE "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${NAME}"

SectionEnd
