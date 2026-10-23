# Auditoria de paridade: fatia "forms" (janelas secundárias)

Escopo: PreferencesFormUnit, StyleFormUnit, ErrorReportFormUnit, FindFormUnit, GotoFormUnit, DelayFormUnit,
TranslateFormUnit, SpellCheckFormUnit, SuggestionFormUnit, SilentZoneFormUnit, WAVExtractFormUnit,
PeakCreationProgressFormUnit, VerticalScalingFormUnit, LogWindowFormUnit, AboutFormUnit e DetachedVideoFormUnit
(.pas e .dfm do 1.0.1). Comparei com `src/VisualSubSync.App/Views/*Window*`, os ViewModels e os serviços
(StyleEditor, SpellCheckSession, HotkeyEditor, ErrorReportHtml, Translators, SilentZones). Para o layout usei
`docs/enhanced/T*FORM.txt` e, quando precisei, os fontes do Enhanced 1.2.33 (`enh-src/`).
Não repeti os itens A01-A23 de `docs/AUDITORIA-PARIDADE-2026-10-21.md` nem as diferenças que o
PLANO-DE-MIGRACAO.md já registra.

Caminhos .NET relativos a `<NET>/src/VisualSubSync.App/`.

---

### "Open backup dir" abre uma pasta fixa, que não é a pasta onde os backups são gravados
- Gravidade: média
- Original: PreferencesFormUnit.pas:1252-1257 (bttOpenBackupDirClick): chama `CheckBackupDirectory` e depois abre `g_BackupDirectory`, a mesma pasta que o backup usa. O Enhanced (enh-src/PreferencesFormUnit.pas:1922) faz igual.
- .NET: Views/PreferencesWindow.xaml.cs:231-236 (OnOpenBackupDir) usa `Path.Combine(AppContext.BaseDirectory, "Backup")`. O backup usa `StoragePaths.ResolveBackupDirectory(AppContext.BaseDirectory)` (ViewModels/MainViewModel.cs, `BackupDirectory`), que passa para `%APPDATA%\VisualSubSync\Backup` quando a pasta portátil não é gravável.
- Diferença: numa instalação em Program Files (o caso do instalador), os backups vão para o perfil do usuário. O botão, porém, tenta criar `...\Program Files\VisualSubSync\Backup`. Sem permissão de administrador, `Directory.CreateDirectory` lança `UnauthorizedAccessException`. Essa exceção não é tratada no handler, então o botão não abre nada e o programa pode cair. Quando a pasta já existe, ele abre uma pasta vazia, e não a dos backups.
- Correção sugerida: usar `StoragePaths.ResolveBackupDirectory(AppContext.BaseDirectory)` (o mesmo `BackupDirectory` do ViewModel, exposto ou passado para a janela). Tratar IOException/UnauthorizedAccessException ao criar a pasta.

### Janela "Creating peak file..." (TPeakCreationProgressForm) não foi portada
- Gravidade: média
- Original: PeakCreationProgressFormUnit.pas e .dfm (janela fsStayOnTop, 250x25, barra de progresso, sem fechar por CloseQuery, minimizar minimiza o aplicativo). Ela aparece em main.pas:1836-1856 (WAVDisplayer1OnPeakFileCreation: pfcevtStart/Progress/Stop), com `Self.Enabled := False` durante a criação. O Enhanced tem a mesma janela (TPEAKCREATIONPROGRESSFORM.txt, ClientWidth 261).
- .NET: ViewModels/MainViewModel.cs:1652-1666 (LoadPeaks) chama `wav.CreatePeaks()` de forma síncrona, na thread da interface, sem janela nem progresso. Não existe nenhuma classe equivalente em Views/.
- Diferença: a geração de picos a partir do WAV agora existe (A03), mas sem a janela de progresso. Com um WAV longo, a interface congela sem nenhum aviso. No original, a janela "Creating peak file..." mostrava a barra de progresso, e a principal ficava desabilitada.
- Correção sugerida: criar `PeakCreationProgressWindow` com o layout do Enhanced (título "Creating peak file...", ProgressBar de 261x25, Topmost, fechar bloqueado). Gerar os picos fora da thread da interface, com `IProgress`, e desabilitar a principal durante o trabalho.

### Janela Translate não é redimensionável
- Gravidade: baixa
- Original: TranslateFormUnit.dfm e TTRANSLATEFORM.txt: sem BorderStyle (bsSizeable), Width 608, Height 289, `Constraints.MinHeight = 280`, `MinWidth = 300`. Os controles têm Anchors (grupos, edits e botões acompanham a largura; OK/Cancel ficam presos à direita).
- .NET: Views/TranslateWindow.xaml:5-6 (`SizeToContent="WidthAndHeight"`, `ResizeMode="CanMinimize"`) e um Canvas de tamanho fixo.
- Diferença: no original e no Enhanced a janela se redimensiona e o campo do arquivo de destino cresce, o que ajuda com caminhos longos. No .NET o tamanho é fixo. Isso não está registrado.
- Correção sugerida: `ResizeMode="CanResize"`, Width/Height 608x289, MinWidth/MinHeight 300/280, e trocar o Canvas por um Grid com as mesmas âncoras (edTargetFile e edCustomText esticando, "..." e OK/Cancel à direita).

### Ordem de tabulação diferente do DFM em várias janelas
- Gravidade: baixa
- Original: TabOrder dos DFM do Enhanced:
  - TDELAYFORM: meDelay 0, rgApplyTo 1, rgType 2, Apply 3, Cancel 4, rgShift 5.
  - TTRANSLATEFORM: PanelBottom (OK, Cancel) antes de PanelClient; dentro deste, rgSelectionType, rgTextType, edCustomText, edTargetFile, "...".
  - TSILENTZONEFORM: lbSilentZones 0, depois o grupo (edThreshold, edDuration, ..., bttUpdate).
  - TEXTRACTWAVFORM: grupo de info, Extract, Cancel, barra, Settings, Stop, Debug.
  - TSPELLCHECKFORM: reSubtitleText com `TabStop = False`.
- .NET:
  - Views/DelayWindow.xaml: só meDelay tem TabIndex; o resto segue a ordem do XAML (Type → Apply to → Shift → Apply → Cancel).
  - Views/TranslateWindow.xaml:20-43: só o primeiro rádio de cada grupo tem TabIndex; os outros rádios ficam com o padrão (Int32.MaxValue) e caem depois de Cancel.
  - Views/SilentZoneWindow.xaml:26-38: o grupo é ordenado como um contêiner com bttUpdate (TabIndex 4) antes dos campos sem TabIndex, então a ordem é lista → Update → Threshold → Duration.
  - Views/ExtractWavWindow.xaml: sem TabIndex.
  - Views/SpellCheckWindow.xaml:33: o RichTextBox recebe foco por Tab (TabIndex 0).
- Diferença: Tab percorre os controles numa ordem diferente da do original. Além disso, no WPF cada rádio de um grupo é uma parada de Tab, enquanto no TRadioGroup o grupo inteiro é uma parada só (as setas trocam o item).
- Correção sugerida: definir TabIndex conforme o DFM. Nos grupos de rádio, usar `KeyboardNavigation.TabNavigation="Once"` num contêiner por grupo. No RichTextBox da verificação ortográfica, usar `IsTabStop="False"`.

### Suggestions: o memo e o duplo clique usam o primeiro item selecionado, não o focado
- Gravidade: baixa
- Original: SuggestionFormUnit.pas:338-349 (vtvSuggestionsLstFocusChanged): o memo mostra o texto do nó **focado**. SuggestionFormUnit.pas:247-255 (vtvSuggestionsLstDblClick): usa `FocusedNode`. A lista tem `toMultiSelect`.
- .NET: Views/SuggestionWindow.xaml.cs:38 e 43 usam `vtvSuggestionsLst.SelectedItem`, que com SelectionMode Extended é o primeiro item selecionado.
- Diferença: com várias sugestões selecionadas (Ctrl/Shift+clique), o memo continua mostrando a primeira selecionada, e não a que acabou de ser clicada. O duplo clique numa sugestão também seleciona na lista principal a legenda da primeira selecionada, e não a da clicada. A janela Error report já tem esse cuidado (`_focused` em ErrorReportWindow.xaml.cs:25-41).
- Correção sugerida: guardar o último item adicionado à seleção (como em ErrorReportWindow) ou usar o item sob o mouse no duplo clique. Mostrar no memo o texto desse item.

---

Rotinas conferidas (cerca de 190):
- GotoFormUnit (todas) e ActionGotoExecute.
- DelayFormUnit (todas).
- FindFormUnit (todas: Historize, Find, Change, MatchingOnly, MatchCase, Replace, ReplaceFind, ReplaceAll, RegExp, Close, SetMemoMode e KeyUp), com ResetFind e ShowMatchingSubOnly.
- TranslateFormUnit (todas, inclusive os tradutores e StripText).
- SpellCheckFormUnit (todas: Reset, UpdateButton, NextError, ReplaceWordBy, Replace, ReplaceAll, Ignore, IgnoreAll, Add e lbSuggestionsClick) e ActionSpellCheckExecute.
- SuggestionFormUnit (todas).
- SilentZoneFormUnit (todas) e ActionShowSilentZonesExecute.
- VerticalScalingFormUnit (todas).
- LogWindowFormUnit (todas).
- AboutFormUnit (todas).
- DetachedVideoFormUnit (todas).
- PeakCreationProgressFormUnit (todas) e WAVDisplayer1OnPeakFileCreation.
- WAVExtractFormUnit (todas).
- ErrorReportFormUnit (todas: DrawNode, AddError, DeleteError, FixSelectedErrors, menu de contexto, FixAll e SelectAll em XXX/YYY, Clear, GoToNext/Previous e ExportToHTML), com ActionFixSelectedErrors.
- StyleFormUnit (todas, inclusive TSSAStyle.Create/Assign/Equals, DataToForm, FormToData, UpdateSelectionData, CheckChange, HSL e ConfigureMode) e ActionStylesExecute.
- PreferencesFormUnit: TConfigObject.SetDefault/LoadIni/SaveIni, LoadConfig/SaveConfig, plugins, hotkeys (Set/Clear/ResetAll), edição de parâmetros (TPropertyEditLink), fontes, backup, associações e FormKeyDown.

Não conferidas a fundo:
- O layout pixel a pixel das 2050 linhas de TPREFERENCESFORM.txt (só conferi as rotinas e os controles usados por elas).
- TSSAStyle.getAsSSA/getAsASS, que pertencem à gravação de SSA/ASS (fatia model).
- O cálculo de DetectSilentZone e de ChangeColorHSL (fatia waveform/model).
