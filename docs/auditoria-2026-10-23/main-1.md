# Auditoria main-1: main.pas linhas 1 a 3300 (FormCreate até ActionOpenProject)

Comparado com `src/VisualSubSync.App/ViewModels/MainViewModel*.cs`, `Views/MainWindow*.cs`,
`Services/DialogService.cs`, `Views/FormPlacement.cs` e `src/VisualSubSync.Core` (Settings, Projects,
Subtitles, Audio). Diferenças já registradas no PLANO-DE-MIGRACAO.md não foram repetidas.

### Fechar o projeto não fecha a mídia: Play, Timing e quadros continuam habilitados
- Gravidade: alta
- Original: main.pas:1631 (EnableControl), chamado com False por CloseProject (main.pas:4803), que também chama `AudioOnlyRenderer.Close`/`VideoRenderer.Close`
- .NET: MainViewModel.cs:1010 (`CanPlay => IsMediaOpen`), MainViewModel.cs:961-976 (`CanExecute = IsVideoOpen`), MainViewModel.cs:1327 (CloseProject)
- Diferença: no original, `ActionPlay.Enabled := Enable and (VideoRenderer.IsOpen or AudioOnlyRenderer.IsOpen)`, e o mesmo vale para Stop, Loop, Pause, as velocidades, Play 1s before, Timing mode (`bttWorkingMode`) e Show start/stop frame/frame at cursor (`Enable and VideoRenderer.IsOpen`). Com o projeto fechado, tudo isso fica desabilitado e a mídia é fechada. No .NET, o CanExecute depende só de `IsMediaOpen`/`IsVideoOpen`. O `CloseProject` não zera esses dois e não fecha o motor (`IMediaEngine` nem tem método de fechar). Depois de File > Close, ou ao abrir outro projeto sem mídia, o áudio e o vídeo do projeto anterior continuam carregados e podem ser tocados. O botão Normal/Timing e os comandos de quadro também continuam habilitados.
- Correção sugerida: no `CloseProject`, parar a reprodução, fechar/descarregar a mídia no motor (acrescentar `Close` ao `IMediaEngine`) e zerar `IsMediaOpen`, `IsVideoOpen`, `IsPlaying` e `VideoAspectRatio`. Fazer `CanPlay => HasProject && IsMediaOpen` e o CanExecute dos quadros `HasProject && IsVideoOpen`.

### LoadProject não esconde o vídeo quando o novo projeto não tem vídeo
- Gravidade: média
- Original: main.pas:3053-3055 (LoadProject, bloco `finally`: `if (not VideoRenderer.IsOpen) and ShowingVideo then ActionShowHideVideo.Execute`)
- .NET: MainViewModel.cs:352-363 (LoadProject)
- Diferença: o .NET só mexe em `ShowingVideo` quando `IsVideoOpen`. Se o projeto anterior estava com o vídeo à mostra e o novo não tem vídeo, ou o vídeo não abre, `ShowingVideo` continua verdadeiro. O painel de vídeo (ou a janela destacada) continua visível e `UpdateSubtitleForPreview` se comporta como se houvesse vídeo. O `ProjectProperties` (MainViewModel.cs:616) faz esse passo, mas o `LoadProject` não.
- Correção sugerida: no fim do `LoadProject`, também no caminho de erro, chamar `ShowHideVideo()` quando `!IsVideoOpen && ShowingVideo`.

### Forma de onda não é gerada a partir de um WAV sem .peak (LoadWAV / OnPeakFileCreation não portados)
- Gravidade: alta
- Original: main.pas:2893-2898 (LoadProject, modo `pwmExternal`: `WAVDisplayer.LoadWAV(CurrentProject.WAVFile)`) e main.pas:1834 (WAVDisplayer1OnPeakFileCreation: janela de progresso, formulário desabilitado durante a criação)
- .NET: MainViewModel.cs:1487-1554 (LoadPeaks: "aqui só se usa o .peak existente")
- Diferença: no modo "WAV file", o `TWAVDisplayer.LoadWAV` do original cria o `.peak` a partir do WAV quando ele não existe, mostrando o `TPeakCreationProgressForm`. Um projeto com um WAV externo escolhido em "Use existing WAV file", ou com o `.peak` apagado, abre normalmente com forma de onda. No .NET, `LoadPeaks` devolve `null` quando o `.peak` não existe: o projeto abre sem forma de onda e sem nenhum aviso. Isso não está registrado no plano como diferença. Já existem o `PeakBuilder` e o `WavFile` no código.
- Correção sugerida: em `LoadPeaks`, no modo External com o WAV presente e sem `.peak`, gerar os picos a partir do WAV (`WavFile` + `PeakBuilder`, como `CreatePeakFile`), com uma janela de progresso equivalente, gravar `ChangeExtension(wav, ".peak")` e carregar. Isso vale também para o `ProjectProperties`, que chama `LoadPeaks`.

### O tempo grande do cursor (plCursorPos) fica parado durante a reprodução
- Gravidade: alta
- Original: main.pas:1712 (WAVDisplayer1CursorChange: só atualiza com `not IsPlaying`) e main.pas:1720 (WAVDisplayer1PlayCursorChange: `plCursorPos.Caption := TimeMsToString(GetPlayCursorPos)`)
- .NET: MainViewModel.cs:244 (`CursorText => TimeFormat.ToVss(CursorMs)`), MainViewModel.cs:1043-1059 (OnPlayTimerTick só muda `PlayCursorMs`), MainWindow.xaml:647
- Diferença: no original, durante a reprodução o painel de 24 pt mostra a posição do cursor de reprodução a cada passo do timer. No .NET, `CursorText` depende só de `CursorMs` e não é avisado quando `PlayCursorMs` muda, então o número fica parado na posição do cursor de edição enquanto toca.
- Correção sugerida: atualizar o texto com o cursor de reprodução enquanto toca. Por exemplo, um campo próprio de texto que `OnPlayTimerTick` atualiza com `PlayCursorMs` e que a mudança de `CursorMs` só altera quando `!IsPlaying`, como os dois handlers do original.

### Show/Hide Video aceita mídia só de áudio
- Gravidade: média
- Original: main.pas:3262 (ActionShowHideVideoExecute: `if (not VideoRenderer.IsOpen) and (not ShowingVideo)` mostra "Video file is not opened...")
- .NET: MainViewModel.cs:941 (`if (!IsMediaOpen && !ShowingVideo)`)
- Diferença: no .NET, um projeto aberto só com o WAV (sem vídeo) tem `IsMediaOpen` verdadeiro. Por isso F4 / View > Show/Hide Video mostra um painel preto em vez da mensagem e grava `ShowVideo=1` no `.vssprj`.
- Correção sugerida: testar `!IsVideoOpen && !ShowingVideo`.

### Duplo clique na lista: não mostra o quadro inicial e não desliga o auto-scroll durante a reprodução
- Gravidade: média
- Original: main.pas:2767 (vtvSubsListDblClick); SelectNode (main.pas:4314) chama a mesma rotina com `Sender = nil`
- .NET: MainViewModel.cs:657 (ActivateSelectedSubtitle), MainWindow.xaml.cs:466
- Diferença: o original, depois de `ZoomAndSelectRange`, faz `WAVDisplayer.AutoScrolling := False` se estiver tocando. No duplo clique do usuário (`Sender` atribuído), com o vídeo à mostra e parado, ele também chama `ActionShowStartFrameExecute`. O .NET só faz o zoom/seleção: durante a reprodução, a onda continua rolando e sai da legenda escolhida, e com o vídeo à mostra o quadro não é atualizado.
- Correção sugerida: em `ActivateSelectedSubtitle`, receber um indicador "do usuário". Se `IsPlaying`, fazer `AutoScrollWavDisplay = false`. Se for do usuário, com `ShowingVideo && !IsPlaying`, chamar `ShowStartFrame()`. `SelectNodeAtIndex` passa "não é do usuário".

### Arrastar a borda na onda regrava a legenda do vídeo a cada movimento
- Gravidade: média
- Original: main.pas:1807-1810 (WAVDisplayer1SelectedRangeChange: `DisableVideoUpdatePreview := True` em volta de `IsDirty := True`) e main.pas:7648 (UpdateSubtitleForPreview sai na hora com `DisableVideoUpdatePreview`)
- .NET: MainViewModel.cs:684-699 (OnWaveformRangeChanging: `IsDirty = true`), MainViewModel.Preview.cs:23-39
- Diferença: no .NET, cada `IsDirty = true` chama `UpdateSubtitleForPreview(false)`. Durante o arraste de uma borda, com o vídeo à mostra e tocando ou pausado, a cópia temporária das legendas é gravada em disco e recarregada no mpv a cada movimento do mouse. O original suspende essa atualização durante o arraste (só marca que ela é necessária) e a faz depois, no `SetTimeTask`.
- Correção sugerida: acrescentar um `_disableVideoUpdatePreview` testado no início de `UpdateSubtitleForPreview` e ligá-lo em volta de `IsDirty = true` em `OnWaveformRangeChanging`.

### Ordem das colunas (ColumnsPosition) não é gravada nem restaurada
- Gravidade: média
- Original: main.pas:1236-1245 (SaveSettings: `[General] ColumnsPosition`) e main.pas:1344-1348 (LoadColumnsSettings)
- .NET: VssSettings.cs (só `ColumnsWidth`), MainWindow.xaml.cs:256-261 e 400-411, MainWindow.xaml:764 (`CanUserReorderColumns="True"`)
- Diferença: o cabeçalho deixa arrastar as colunas (`hoDrag` no original, `CanUserReorderColumns` no .NET), mas o .NET não grava nem lê `ColumnsPosition`. A ordem escolhida pelo usuário se perde ao reiniciar, e a ordem gravada pelo VisualSubSync original/Enhanced não é aplicada.
- Correção sugerida: ler e gravar `[General] ColumnsPosition` (uma posição por coluna, na ordem #, Start, Stop, Style, Text, VO e extras) e aplicá-la em `DataGridColumn.DisplayIndex`. Valor inválido usa MaxInt, ou seja, vai para o fim, como o `StrToIntDef(..., MaxInt)`.

### Save As não limpa o estado "alterado"
- Gravidade: média
- Original: main.pas:2758-2762 (SaveSubtitles: com `BackupOnly = False`, `ApplyAutoBackupSettings` e `CurrentProject.IsDirty := False`), usada por ActionSaveAsExecute (main.pas:3337)
- .NET: MainViewModel.cs:448-477 (SaveAs), MainViewModel.cs:1383-1407 (SaveTo)
- Diferença: no original, toda gravação bem-sucedida pelo `SaveSubtitles`, inclusive Save As para outro arquivo ou formato, tira o "*" do título e desabilita Save. No .NET, só o `Save()` faz `IsDirty = false`. Depois de Save As, o projeto continua alterado e o programa pergunta se salva ao fechar.
- Correção sugerida: em `SaveAs`, quando `SaveTo` devolver verdadeiro, fazer `IsDirty = false`, como o `SaveSubtitles`.

### Mensagem de extensão inválida para a legenda do projeto
- Gravidade: baixa
- Original: main.pas:2746-2750 (SaveSubtitles)
- .NET: MainViewModel.cs:1385-1388 (SaveTo)
- Diferença: quando o arquivo é a própria legenda do projeto, o original diz `Cannot save the file "X". Please, specify a valid file extension in the "Project Properties" dialog under the "File" menu.`. O .NET sempre mostra a forma curta, `... Please, specify a valid file extension.`
- Correção sugerida: em `SaveTo`, usar o texto longo quando `target == Project.SubtitlesFile`.

### Mudanças de cena do vídeo anterior ficam na onda (ClearVideoSceneChange não portado)
- Gravidade: baixa
- Original: main.pas:2804 (ClearVideoSceneChange), usada em ActionProjectPropertiesExecute quando o novo vídeo não abre (main.pas:3482)
- .NET: MainViewModel.cs:1127-1165 (OpenMediaAsync: sem fonte ou sem vídeo, `SceneChanges` não muda)
- Diferença: em Project Properties, ao trocar o vídeo por um vazio, inexistente ou só de áudio, as mudanças de cena do vídeo antigo continuam desenhadas e continuam valendo para o encaixe do mouse. O original as limpa.
- Correção sugerida: em `OpenMediaAsync`, fazer `SceneChanges = []` quando não houver vídeo aberto (fonte vazia, falha ou `!IsVideoOpen`).

### Mensagens de carregamento da barra de status
- Gravidade: baixa
- Original: main.pas:2939 e 2953 (LoadProject: "Loading audio file..." e "Loading video file..." sempre)
- .NET: MainViewModel.cs:1144 (só "Loading audio file...", e só quando há fonte de mídia)
- Diferença: o .NET não mostra "Loading video file..." e pula "Loading audio file..." quando o projeto não tem mídia.
- Correção sugerida: mostrar as duas mensagens na mesma ordem do original.

### Posição da janela principal: fora da tela, minimizada e sem ini
- Gravidade: baixa
- Original: main.pas:1103-1145 (LoadFormPosition aplicado ao MainForm; DFM com `Position = poDefault`)
- .NET: MainWindow.xaml.cs:235-250 (ApplySettings)
- Diferença: o .NET não aplica à janela principal a verificação de "fora de todas as telas, volta para 100,100" que usa nas outras janelas (FormPlacement.Restore). `MainForm_State=1` (minimizada) vira Normal, e sem `[Windows] MainForm_*` a janela é centralizada (`CenterScreen`), enquanto o original deixa o Windows escolher a posição (`poDefault`).
- Correção sugerida: usar a mesma verificação de `FormPlacement.Restore` para a janela principal e respeitar o estado 1. Sem ini, usar o posicionamento padrão do sistema (`WindowStartupLocation.Manual` sem Left/Top, ou o equivalente a CW_USEDEFAULT).

### Campos Sel/View não ficam desabilitados sem projeto
- Gravidade: baixa
- Original: main.pas:1688-1693 (EnableControl: `edSelBegin..edViewLength.Enabled := Enable`)
- .NET: MainWindow.xaml (PanelTimeSelView, por volta da linha 652: o Canvas e os TextBox não têm `IsEnabled`)
- Diferença: sem projeto, os seis campos ficam vazios mas com aparência de habilitados. No original eles ficam cinza.
- Correção sugerida: `IsEnabled="{Binding IsProjectOpen}"` no Canvas dos seis campos.

---
Rotinas conferidas: 67 (todas as procedures/functions de main.pas:873 a 3302: FormCreate, FormDestroy, Get/SetTextPipeAutoOption, Save/LoadFormPosition, GetIniFilename, GetVTVColumnByName, SaveSettings, LoadSettings, LoadColumnsSettings, ApplyMiscSettings, FinishLoadSettings, LoadAfterParamsSettings, InitVTV, InitVTVExtraColumns, InitGeneralJSPlugin, OnRecentMenuItemClick, SetStatusBarPrimaryText, UpdateStatusBar, ShowStatusBarMessage, TimerStatusBarMsgTimer, EnableStyleControls, EnableControl, os 11 handlers do WAVDisplayer1*, chkAutoScrollWAVDispClick, CurrentProjectOnDirtyChange, WAVDisplayPopup_DeleteRange, ActionSplitAtCursorExecute, LoadSubtitles, ShowColumn, LoadVO, LoadVOSubs, LoadSRT, LoadASS, GetVOText, UpdateMemoVO, vtvSubsListGetText, FormMouseWheel, TagHighlight, MemoSubtitleTextChange, MemoSubtitleTextSelectionChange, UpdateLinesCounter, SaveSubtitles, vtvSubsListDblClick, UpdateVideoRendererWindow, ClearVideoSceneChange, LoadVideoSceneChange, LoadProject, SaveProject, ActionZoomIn/Out/Selection/All, ActionPlay, ActionStop, ActionLoop, ActionExit, ActionNewProject, ActionShowHideVideo, ActionOpenProject), mais CheckSubtitlesAreSaved/CloseProject/SelectNode, que elas chamam. Não consegui conferir a pintura em si: o TagHighlight (cores e sublinhado no adorner do WPF), o encaminhamento do FormMouseWheel (roteamento do WPF) e as opções de pintura do InitVTV (grade, seleção "blended") dependem da tela e não foram verificados em execução.
