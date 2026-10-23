# Auditoria: fatia waveform

Fontes conferidas: `WAVDisplayerUnit.pas`, `MiniScrollBarUnit.pas`, `PopupTrackbarUnit.pas`, `WAVFileUnit.pas`,
`SceneChangeUnit.pas`, `CursorManager.pas` e os handlers `WAVDisplayer1*` de `main.pas`, contra
`src/VisualSubSync.Waveform/WaveformControl.cs`, `src/VisualSubSync.Core/Waveform/WaveformMouse.cs`,
`Core/Audio/*`, `Core/Scenes/*`, `Media/FFmpeg/FFmpegKeyframeReader.cs`, `Media/FFmpeg/PeakBuilder.cs`,
`App/ViewModels/MainViewModel*.cs`, `App/Views/MainWindow.xaml(.cs)` e `App/Views/StyleWindow.xaml(.cs)`.

---

### Picos não são gerados a partir do WAV (CreatePeakTab não portado)
- Gravidade: alta
- Original: WAVDisplayerUnit.pas:3113-3137 (LoadWAV, ramo `CreatePeakFile`), 2809-2943 (CreatePeakTab), 2981 (o evento OnPeakFileCreation abre o PeakCreationProgressForm, main.pas:1834); main.pas:2890-2898 (LoadProject, modo pwmExternal)
- .NET: MainViewModel.cs:1482-1553 (LoadPeaks; o comentário diz "aqui só se usa o .peak existente")
- Diferença: no original, um projeto em modo "WAV file" cujo `.wav` existe sem o `.peak` (ou com um `.peak` de tamanho até o cabeçalho, linha 3086) lê o WAV (8 e 16 bits PCM, 32 bits float), monta a tabela de picos com a janela de progresso, normaliza e grava o `.peak`. No .NET, `LoadPeaks` devolve `null` quando o `.peak` não existe: o projeto abre sem forma de onda e o `.peak` nunca é criado. `TWAVFile.Duration`/`SamplesCount` também não foram portados. O plano (Mapa de módulos, linha "WAVFileUnit.pas") previa "geração de picos idêntica", e essa diferença não está registrada.
- Correção sugerida: portar CreatePeakTab (ler o WAV com `WavFile`, `SamplesPerPeak = SamplesPerSecond / 100`, `PeakTabSize = Ceil(SamplesCount / SamplesPerPeak / Channels)`, buffer zerado antes de cada leitura, conversões 8 e 32 bits como `Single2SmallInt`, fator `32768 / max(|PeakMaxMax|, |PeakMinMin|)`) com janela de progresso, e gravar o `.peak` com `PeakFile.Save`. Outra saída: gerar pelo `FFmpegAudioExtractor`/`PeakBuilder` a partir do WAV.

### Um `.peak` curto ou sem assinatura impede que o projeto abra
- Gravidade: alta
- Original: WAVDisplayerUnit.pas:3073-3111 (LoadWAV: só exige `PeakFS.Size > HDRSize`; não confere a assinatura nem a versão; se o arquivo for curto, gera os picos a partir do WAV)
- .NET: PeakFile.cs:30-45 (Read lança `InvalidDataException` se a assinatura ou a versão não baterem; um arquivo vazio cai no mesmo erro); MainViewModel.cs:331 e 404-408 (o catch de LoadProject fecha o projeto e mostra "Could not open")
- Diferença: um `.peak` com 0 bytes ou truncado (extração interrompida, por exemplo) faz o original regerar os picos ou abrir o projeto sem forma de onda. O .NET recusa o projeto inteiro. Um `.peak` com cabeçalho diferente é aceito pelo original e recusado pelo .NET.
- Correção sugerida: em LoadPeaks, tratar arquivo com `Length <= PeakFile.HeaderSize` como inexistente (e gerar pelo WAV, veja o item anterior) e não deixar a exceção de PeakFile derrubar LoadProject: seguir sem forma de onda.

### Fechar o projeto não para a reprodução nem fecha a mídia
- Gravidade: alta
- Original: WAVDisplayerUnit.pas:3829-3838 (`TWAVDisplayer.Close`: `FRenderer.Stop`, `FRenderer := nil`), main.pas:4779 (CloseProject chama `WAVDisplayer.Close`) e 4808-4809 (`AudioOnlyRenderer.Close`, `VideoRenderer.Close`)
- .NET: MainViewModel.cs:1326-1349 (CloseProject)
- Diferença: o CloseProject do .NET limpa as listas e a vista, mas não chama `_engine.Stop()` nem fecha a mídia, e não zera `IsMediaOpen`, `IsVideoOpen`, `IsPlaying` nem `PlayCursorMs`, nem para o `_playTimer`. Ao fechar o projeto durante a reprodução, o som continua, e Play/Stop/Pause/Loop continuam habilitados (`CanPlay => IsMediaOpen`) sem projeto aberto. Ao abrir outro projeto sem mídia, o motor continua com a mídia anterior.
- Correção sugerida: em CloseProject, parar o motor (`_engine?.Stop()`), descarregar a mídia (um `Close`/`stop` do mpv), parar `_playTimer` e zerar `IsPlaying`, `PlayCursorMs`, `IsMediaOpen` e `IsVideoOpen`.

### O painel de posição do cursor não mostra a posição de reprodução
- Gravidade: alta
- Original: main.pas:1712-1723 (WAVDisplayer1CursorChange só atualiza `plCursorPos` quando não está tocando; WAVDisplayer1PlayCursorChange escreve a posição de reprodução a cada passo do timer)
- .NET: MainViewModel.cs:146 e 244 (`CursorText => TimeFormat.ToVss(CursorMs)`, notificado só pela mudança de `CursorMs`); MainWindow.xaml:647
- Diferença: no original, o painel grande "Cursor position" acompanha a reprodução (a cada 40 ms) e, depois de parar, continua mostrando a última posição tocada até o cursor mudar. No .NET ele fica parado no cursor de edição durante toda a reprodução.
- Correção sugerida: guardar o texto num campo próprio: em `OnPlayTimerTick`, quando `PlayCursorMs` muda, escrever o tempo de reprodução; em `OnCursorMsChanged`, escrever o cursor só quando `!IsPlaying`.

### Rolar a onda à mão não desliga o "Auto-scroll WAV display"
- Gravidade: média
- Original: WAVDisplayerUnit.pas:3220-3227 (OnScrollBarChange: `SetAutoScroll(False)` ao clicar ou arrastar na mini barra) e 2572-2580 (arraste com o botão do meio: `SetAutoScrollInternal(False)`); main.pas:1861-1864 (OnAutoScrollChange desmarca `chkAutoScrollWAVDisp`)
- .NET: WaveformControl.cs:88 (ScrollRequested → SetPosition), 960-967 (arraste do polegar), 1080-1096 (paginação da barra), 1099 (SetPosition)
- Diferença: no original, usar a mini barra ou arrastar com o botão do meio desmarca a caixa Auto-scroll, e a vista para de saltar de volta para o cursor de reprodução. No .NET, `AutoScrollWavDisplay` continua ligado: durante a reprodução, o próximo tick do timer leva a vista de volta ao cursor de reprodução e desfaz a rolagem do usuário.
- Correção sugerida: o controle deve expor um evento ou uma propriedade `AutoScrolling` de mão dupla e desligá-la nos três pontos (paginação, arraste do polegar, botão do meio), como SetAutoScroll e SetAutoScrollInternal.

### A rolagem automática ignora o mouse pressionado e passa do fim do arquivo
- Gravidade: média
- Original: WAVDisplayerUnit.pas:3580-3585 (SetPlayCursorPos: só rola com `FMouseIsDown = False`) e 3242-3244 (SetPositionMs: `Constrain(NewPosition, 0, FLengthMs - FPageSizeMs)`)
- .NET: MainViewModel.cs:1054-1058 (OnPlayTimerTick)
- Diferença: (1) o original não rola a vista enquanto um botão do mouse está pressionado na onda (arrastar uma borda ou uma seleção durante a reprodução); o .NET rola e a vista se move por baixo do mouse durante o arraste. (2) o .NET só limita a posição por baixo (`Math.Max(0, …)`): perto do fim, `PositionMs` passa de `LengthMs - PageSizeMs`, e a vista mostra o tempo depois do fim (a onda repete o último pico e o polegar fica preso no limite). O original prende a posição em `[0, Length - PageSize]`.
- Correção sugerida: expor `MouseIsDown` do `WaveformMouse` pelo controle e não rolar enquanto ele for verdadeiro; usar `Math.Clamp(position - PageSizeMs / 8, 0, Math.Max(0, LengthMs - PageSizeMs))`.

### Falta a detecção de vídeo só com quadros-chave na extração de mudanças de cena
- Gravidade: média
- Original: SceneChangeUnit.pas:91-104 (ExtractKeyFramesAVI: com 100 quadros-chave consecutivos, esvazia a lista)
- .NET: FFmpegKeyframeReader.cs:10-38
- Diferença: num vídeo intra-only (MJPEG, DV, HuffYUV e outros comuns em AVI), o original não grava mudanças de cena. O .NET grava uma mudança de cena por quadro no `.scenechange`. A onda fica coberta de faixas `$36C9FF`, e o encaixe do mouse passa a ocorrer em todo lugar.
- Correção sugerida: repetir a regra (contar quadros-chave seguidos no fluxo de vídeo e, ao chegar a 100, devolver lista vazia), ao menos para `.avi` ou para todos os formatos.

### Texto desenhado na onda também para legendas com karaokê
- Gravidade: média
- Original: main.pas:6919-6925 (WAVDisplayer1CustomDrawRange: `if Length(Range.SubTime) > 0 then // TODO : karaoke support`, ou seja, não desenha texto)
- .NET: WaveformControl.cs:583-594 (PaintRange desenha `r.Text` sem olhar `SubTimes`)
- Diferença: o original não escreve o texto dentro das legendas que têm marcadores `{\k}`. O .NET escreve o texto bruto, com as tags `{\kNN}`, sobre os marcadores de karaokê.
- Correção sugerida: só adicionar o `TextItem` quando `r.SubTimes` for nulo ou vazio.

### A mini barra de rolagem não repete a paginação com o botão pressionado
- Gravidade: baixa
- Original: MiniScrollBarUnit.pas:113-131 (OnRepeatTimer, 150 ms) e 133-148 (MouseDown liga o timer; para quando o mouse chega ao polegar); MouseUp desliga o timer
- .NET: WaveformControl.cs:1080-1096 (ScrollBarMouseDown pagina uma vez)
- Diferença: segurar o botão no trilho da barra continua paginando a cada 150 ms no original, até o polegar chegar ao mouse. No .NET a página muda só uma vez por clique. O original também pagina com qualquer botão; o .NET só com o esquerdo.
- Correção sugerida: um `DispatcherTimer` de 150 ms ligado no clique no trilho, que acompanha o X do mouse e para quando o X está sobre o polegar ou no MouseUp.

### Linha zero desenhada quando não há picos
- Gravidade: baixa
- Original: WAVDisplayerUnit.pas:1190-1196 (PaintWavOnCanvas: sem `FPeakDataLoaded`, só pinta o fundo de preto e sai, sem linha zero nem PaintAlternateWavColor; a régua é desenhada depois)
- .NET: WaveformControl.cs:267-276 (PaintBase sempre desenha a linha zero `$00518B0A`)
- Diferença: num projeto sem WAV nem `.peak` (duração vinda do vídeo ou das legendas), o original mostra a área toda preta. O .NET mostra a linha verde no meio.
- Correção sugerida: desenhar a linha zero e a cor alternada só quando `Peaks` tem dados.

### Cursor do mouse sobre a borda
- Gravidade: baixa
- Original: WAVDisplayerUnit.pas:2389 (CheckSubtitleForDynamicSelection: `Cursor := crHSplit`)
- .NET: WaveformControl.cs:986 (`Cursors.SizeWE`)
- Diferença: o original mostra o cursor de divisória da VCL (`crHSplit`: duas barras com setas). O .NET mostra a seta dupla horizontal comum. O plano diz "cursor crHSplit".
- Correção sugerida: carregar um cursor `.cur` equivalente ao `crHSplit` (recurso da VCL) e usá-lo no lugar de `SizeWE`.

### Sem ampulheta ao abrir o projeto e na janela de extração
- Gravidade: baixa
- Original: CursorManager.pas (TCursorManager troca `Screen.Cursor` e restaura ao sair do escopo), usado em main.pas:2862 (LoadProject) e WAVExtractFormUnit.pas:143 (FormActivate); os outros usos (ErrorReport e Export to WAV) estão portados com `IDialogService.WaitCursor`
- .NET: MainViewModel.cs:312-408 (LoadProject) e ExtractWavWindow.xaml.cs (OnLoaded): nenhum `WaitCursor`
- Diferença: durante LoadProject (leitura dos picos, das legendas e da mídia, extração dos quadros-chave) e durante a contagem de fluxos de áudio da janela de extração, o original mostra a ampulheta. O .NET mantém a seta.
- Correção sugerida: envolver o corpo de LoadProject e o OnLoaded da ExtractWavWindow em `using var cursor = _dialogs.WaitCursor();`.

### Tempo dos quadros-chave arredondado em vez de truncado
- Gravidade: baixa
- Original: SceneChangeUnit.pas:89, 183 e 233 (`Trunc(...)` em AVI, MKV e MP4)
- .NET: FFmpegKeyframeReader.cs:24 (`Math.Round(input.ToMs(ts))`)
- Diferença: o tempo de cada mudança de cena pode sair 1 ms acima do que o original grava no `.scenechange`, o que muda a posição exata das faixas e do encaixe.
- Correção sugerida: usar `(int)Math.Floor(input.ToMs(ts))`.

---

Conferido sem diferença real (resumo): MouseDownCoolEdit, MouseDownSSA, MouseMove (arraste CoolEdit e SSA, botão do meio, seleção dinâmica nas duas passadas e na seleção), MouseUp (descarte abaixo de 40 ms, reordenação, OnKaraokeChanged), DblClick (borda para plugins, trecho de karaokê, legenda inteira), CheckSubtitleForDynamicSelection, FindSnappingPoint e FindCorrectedSnappingPoint, IsFilteredSceneChange, SetMinBlankAt e SetMinBlankOnIdx, anti-sobreposição (ClipCursor −2/+3 e limites de seleção), SelectNext/PreviousKaraoke, SelecteKaraoke, ClearSelection, SetSelectedRangeEx, GetDisplayRangeListAt e ShowVO (duas faixas, VO em cima, cor alternada `$587F27` na faixa ímpar), PaintWavOnCanvas (fórmula dos picos por pixel e da altura), PaintRange (cores, pontilhado, borda cheia sob o mouse, traços de 2 px, marcadores de karaokê), PaintSceneChange, PaintMinimumBlank, PaintSelection (InvertRect e linha XOR), PaintCursor, PaintPlayCursor, PaintRulerOnCanvas e TimeMsToShortString, UpdateView com LengthMs = 0 (cinza), cores de WAVDisplayerUnit e MiniScrollBarUnit, ZoomRange, ZoomIn, ZoomOut, ZoomAll, ZoomCenteredOn (sem uso direto), ZoomAndSelectRange, Scroll, DoMouseWheelUp/Down (80%/125%, ±5 dentro das faixas 0..395 e 5..400, 1/4 de página, ordem H/V/tempo), Pause, PlayRange, UpdatePlayRange, timer de 40 ms, InternalOnStopPlaying, DoContextPopup (SSA sem Shift), UpdateThumb e OnThumbMouseMove da mini barra, NormalizePeakTab (PeakFile.Normalize e PeakBuilder), DetectSilentZone (SilentZones.Detect), TWAVFile.Open (inclusive o aviso "is damaged" no Export) e ExtractToStream, Load/SaveSceneChange e LoadVideoSceneChange, TPopupTrackbar (faixas 0..360 e −100..100, captura no MouseMove, MouseDown fecha sem disparar OnValueChanged, legenda arredondada, destaque à esquerda), WAVDisplayer1SelectedRange, SelectedRangeChange (inclui UpdateMemoVO via RefreshRanges), SelectedRangeChanged, SelectedKaraokeRange, StartPlaying (AutoSaveWhenPlaying), RangeStart/StopDblClick e o alinhamento do texto em WAVDisplayer1CustomDrawRange.

Rotinas conferidas: cerca de 150 (TWAVDisplayer ~80, TRange/TRangeList/TMinBlankInfo ~25, TMiniScrollBar 14, TPopupTrackbar 12, TWAVFile 12, SceneChangeUnit 7, TCursorManager 3 e ~12 handlers de main.pas). Não conferi: KaraSplit/KaraSplit2/TRange.UpdateSubTimeFromText e TRange.Save/LoadFromStream (texto do karaokê e área de transferência, de outras fatias); a fórmula exata do `AlphaBlend` (bmConstantAlphaAndColor) do VirtualTrees, porque o fonte não está no repositório; a fonte usada pela régua (`'Time New Roman'`, com erro de grafia, depende da substituição do GDI); a codificação gravada por `TTntStringList.SaveToFile` no `.scenechange`, porque o fonte da Tnt não está no repositório; e TWAVRenderer (sem uso no original).
