# Auditoria main-3: main.pas 6577–9886

Escopo: rotinas de `main.pas` da linha 6577 (ActionShowFrameAtCursor) até o fim, comparadas com
`src/VisualSubSync.App/ViewModels/*.cs`, `Views/MainWindow*.cs` e `src/VisualSubSync.Core`.
Diferenças já registradas em `docs/PLANO-DE-MIGRACAO.md` não foram repetidas, exceto quando o código
não faz o que o plano diz.

### Atraso com "ambos os tempos" não desloca os marcadores de karaokê
- Gravidade: alta
- Original: main.pas:8027-8046 (ApplyDelay / DelaySub)
- .NET: src/VisualSubSync.App/ViewModels/MainViewModel.Undo.cs:97-131 (ApplyDelay)
- Diferença: com `dstBothTime` (janela Delay, tudo, selecionadas ou do cursor até o fim), o original
  soma o atraso também a cada `Range.SubTime[i]`, de modo que os marcadores de karaokê acompanham a
  legenda. O .NET só muda `StartMs`/`StopMs`; `Subtitle.SubTimes` continua com os tempos absolutos
  antigos. Resultado: depois do atraso, os marcadores aparecem fora da legenda na onda (ou em outra
  legenda). Um arraste de marcador ou um "Place karaoke cursors at end" seguinte recalcula os `\k` a
  partir desses tempos errados, e isso estraga o texto. O plano (seção karaokê) diz "Atrasar ou mudar os
  tempos da legenda não move os marcadores, como no original", mas no `DelaySub` do original eles se
  movem quando os dois tempos mudam.
- Correção sugerida: em `ApplyDelay`, quando `shiftType == DelayShiftType.BothTime`, somar `delayMs` a
  cada item de `model.SubTimes`, como o `DelaySub`. Depois, corrigir a frase do plano.

### Mudar início/fim de uma legenda com karaokê não reescreve os `\k`
- Gravidade: média
- Original: main.pas:8252-8286 (SetSubtitleTime) e main.pas:8066-8076 (ApplyDelay, trecho do SelectedRange)
- .NET: src/VisualSubSync.App/ViewModels/MainViewModel.Undo.cs:73-91 (SetSubtitleTime) e :97-131 (ApplyDelay)
- Diferença: em `SetSubtitleTime` (Set subtitle time, desfazer/refazer de arraste e de Split), quando a
  legenda tem `SubTime`, o original chama `CalculateNewTextFromSubTime` e grava o texto novo, porque a
  duração do primeiro `\k` (início até o primeiro marcador) e a do último (último marcador até o fim)
  mudam. Depois chama `vtvSubsListFocusChanged` para o editor mostrar esse texto. Em `ApplyDelay` com
  `dstStartTimeOnly`/`dstStopTimeOnly` sobre a legenda presa à seleção, o original chama
  `WAVDisplayer1KaraokeChanged(..., -1, -1)` com o mesmo efeito. O .NET não recalcula: o texto fica com
  os `\k` antigos, que já não somam a duração da legenda.
- Correção sugerida: em `SetSubtitleTime` e, para início/fim isolados da legenda presa à seleção, em
  `ApplyDelay`: se `SubTimes.Length > 0`, aplicar `Karaoke.TextFromSubTimes(model)` e, quando o texto
  mudar, gravá-lo no item (sem gerar tarefa de desfazer, como no original) e atualizar o editor.

### Apagar as últimas legendas da lista leva o foco para a primeira
- Gravidade: média
- Original: main.pas:8128-8145 (DeleteSubtitles)
- .NET: src/VisualSubSync.App/ViewModels/MainViewModel.Undo.cs:153-154 (DeleteSubtitles)
- Diferença: o original foca o nó que vem depois da legenda anterior à primeira apagada
  (`GetNext(RangeList[Idx-1].Node)`), ou a primeira legenda quando `Idx = 0`. Quando as apagadas eram as
  últimas, esse `GetNext` dá `nil` e nenhum nó novo é focado. O .NET usa
  `Subtitles[first]` se existir e, se não, `Subtitles[0]`. Assim, apagar a última legenda (Del na lista,
  menu da onda, desfazer de Add/colar) leva o foco e a rolagem da lista para o começo.
- Correção sugerida: quando `first >= Subtitles.Count`, não trocar `SelectedSubtitle` para a primeira
  (seguir o comportamento do original de não focar nenhum nó novo), em vez de cair em `Subtitles[0]`.

### Apagar legendas não ajusta a posição da busca (F3)
- Gravidade: média
- Original: main.pas:8116-8120 (DeleteSubtitles)
- .NET: src/VisualSubSync.App/ViewModels/MainViewModel.Undo.cs:137-156; estado em MainViewModel.Find.cs:17-25
- Diferença: para cada legenda apagada, o original faz `if SearchNodeIndex > Idx then Dec(SearchNodeIndex)`
  e `if SearchNodeIndex >= Idx then SearchPos := 1`. O .NET não mexe em `_searchNodeIndex` nem em
  `_searchPos`. Depois de apagar uma legenda antes do último resultado, Find next continua uma legenda
  adiante, pulando a que ocupou o lugar. Depois de apagar a própria legenda do resultado, a busca
  continua na seguinte a partir da posição de caractere antiga, em vez de 1.
- Correção sugerida: no laço de `DeleteSubtitles` (do maior índice para o menor), aplicar as mesmas duas
  regras a `_searchNodeIndex`/`_searchPos`.

### Texto de legenda com karaokê desenhado na forma de onda
- Gravidade: baixa
- Original: main.pas:6913-6949 (WAVDisplayer1CustomDrawRange)
- .NET: src/VisualSubSync.Waveform/WaveformControl.cs:583-593
- Diferença: o original não desenha o texto quando `Length(Range.SubTime) > 0` (ramo "TODO : karaoke
  support" vazio). O .NET desenha o texto de toda legenda, com ou sem marcadores, por cima dos
  marcadores pontilhados.
- Correção sugerida: não acrescentar o `TextItem` quando `r.SubTimes` não estiver vazio.

### Texto na forma de onda ignora itálico da fonte do editor
- Gravidade: baixa
- Original: main.pas:6932 (`ACanvas.Font.Assign(MemoSubtitleText.Font)`)
- .NET: src/VisualSubSync.Waveform/WaveformControl.cs:591; src/VisualSubSync.App/Views/MainWindow.xaml.cs:289-291
- Diferença: o original usa a fonte inteira do editor (nome, tamanho, negrito, itálico, sublinhado). O
  .NET passa família, tamanho e peso, mas cria o `Typeface` sempre com `FontStyles.Normal`. Com uma fonte
  itálica nas preferências, o texto na onda sai sem itálico.
- Correção sugerida: passar também `MemoSubtitleText.FontStyle` para o controle e usá-lo no `Typeface`.

### Combo de estilos sem legenda selecionada usa a legenda focada
- Gravidade: baixa
- Original: main.pas:7498-7529 (UpdateStylesComboboxFromSelection)
- .NET: src/VisualSubSync.App/ViewModels/MainViewModel.Styles.cs:66-88
- Diferença: sem nenhuma legenda selecionada, o original não muda `cbStyles.ItemIndex` (`GetFirstSelected`
  é `nil`). O .NET troca a lista vazia pela legenda focada e põe no combo o estilo dela.
- Correção sugerida: com `_selectedItems.Count == 0`, manter `SelectedStyleIndex`, sem usar
  `SelectedSubtitle`.

---

Rotinas conferidas: 112 (todas as procedures/functions de 6577 a 9861: ShowFrameAtCursor,
DetectCharsetDataLoss, SaveSubtitlesAsSRT/SSA/ASS/CUE/TXT/CSV, SelectedKaraokeRange, CustomDrawRange,
ExportToWAV, PlaySelStart/End, PlaceKaraokeCursorsAtEnd, PlayToEnd, Play1sBefore(ToEnd),
SelectNext/PreviousSub, PanelVideoResize/DblClick, SetSubtitleStartTime, SetFocusedSubtitleStopTime,
AdvanceToNextSubtitleAfterFocus, cbStylesSelect, UpdateStylesComboBox(FromSelection), ActionStyles,
RenameStyle, Pause, ToggleTimingMode, Volume, SetEditorFocus(AndSelect), UpdateSubtitleForPreview,
CurrentProjectOnDirtySet/SetDirty, NextError, CallJSOnSubtitleModification/OnSelectedSubtitle,
RangeStart/StopDblClick, OnJsSetStatusBarText, ProcessParams, ApplyDelay, DeleteSubtitle(s),
Clone/RestoreSubtitles, SplitSubtitle, SetSubtitleTime, MergeSubtitles, OnUndo, ProcessMultiChangeSub,
PushUndoableTask, Undo/Redo, ScrollLeft/Right, ShowHideSceneChange, TagText/TagTextMemo/TagTextLines,
ActionText*, BeforeCellPaint/PaintText, StripTags, pmiToggleColumn, OpenProjectFolder,
Insert/DeleteSceneChange, Reload, MergeWithPrevious/Next/Dialog/OnOneLine, OnJavascriptAction,
UpdateAfterJSChange, RegisterJavascriptAction, os acessores do VSSCore, presets, Copy/Paste(AtCursor) e
os Update deles, SetSelection, ShowSilentZones, LoadDict e o resto do corretor ortográfico, InsertSceneChange,
ToggleVO/TurnOn/TurnOff/AdjustShowVO, ShowHideSubs, UpdateDurationWithSubOnly, TranslationTemplate,
GetTextSelection*/GetAudioCursorPosition).
Não conferidas: `GetVideoRendererFiltersList` (DirectShow, sem equivalente) e
`MenuItemHelpCodecsInstallationClick` (fora do menu por decisão registrada no plano). Ficou sem confirmação
o comportamento interno do VirtualTreeView (foco depois de `DeleteNode` do nó focado), porque a fonte dele
não está na pasta. Por isso o achado do foco ao apagar compara só o que `DeleteSubtitles` faz
explicitamente.
