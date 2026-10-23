# Auditoria de paridade — fatia main-2 (main.pas 3300–6600)

Caminhos .NET relativos a `<NET>/src/VisualSubSync.App/` salvo indicação.

### Fechar o projeto não fecha a mídia (Play e afins continuam ativos)
- Gravidade: alta
- Original: main.pas:4763 (CloseProject): `AudioOnlyRenderer.Close; VideoRenderer.Close;` e `EnableControl(False)`, que desabilita ActionPlay/Stop/Loop/Pause/Timing/PlaybackRate e o `PanelVideo`.
- .NET: ViewModels/MainViewModel.cs:1327 (CloseProject); `IMediaEngine` (src/VisualSubSync.Media/IMediaEngine.cs) não tem `Close`.
- Diferença: o .NET limpa lista, onda, VO e desfazer, mas não fecha o motor nem zera `IsMediaOpen`, `IsVideoOpen` e `ShowingVideo`. Como Play, Stop, Pause, Loop, Play to end, Playback rate e o botão Normal/Timing dependem só de `IsMediaOpen` (`CanPlay`), depois de File > Close eles continuam habilitados e tocam a mídia do projeto fechado. O painel de vídeo continua à mostra com o último quadro. Abrir depois um projeto sem vídeo também deixa `ShowingVideo` como estava, porque `LoadProject` só o redefine com vídeo aberto.
- Correção sugerida: acrescentar `Close()` ao `IMediaEngine`/`MpvMediaEngine` e chamá-lo em `CloseProject`, com `_playTimer.Stop()`, `IsPlaying = false`, `IsMediaOpen = false`, `IsVideoOpen = false` e `SceneChanges = null` (já existe). O painel de vídeo deve ficar desabilitado ou vazio, como o `PanelVideo.Enabled := False`.

### Auto-scroll "Subtitles" não faz nada (TimerAutoScrollSub não portado)
- Gravidade: alta
- Original: main.pas:4430 (chkAutoScrollSubClick) e main.pas:4438 (TimerAutoScrollSubTimer); main.dfm:4144 (Interval = 80).
- .NET: não portado. Só existe a propriedade `AutoScrollSubtitles` (MainViewModel.cs:172 e 1294), ligada à caixa em Views/MainWindow.xaml:608 e gravada no ini.
- Diferença: no original, com a caixa marcada e a onda tocando, a cada 80 ms a legenda sob o cursor de reprodução é rolada para a vista, focada e selecionada sozinha. Fora de qualquer legenda, o editor e o VO são esvaziados sem gerar desfazer. A mudança só vale quando o índice muda (`OldAutoScrollIdx`, zerado no clique da caixa). No .NET, marcar a caixa não tem efeito nenhum.
- Correção sugerida: criar um `DispatcherTimer` de 80 ms, ligado só enquanto `AutoScrollSubtitles` estiver marcado (e zerando o índice antigo a cada troca). Enquanto `IsPlaying`, chamar `GetRangeIdxAt(PlayCursorMs)`. Quando o índice mudar, focar e selecionar só essa legenda e rolar até ela. Com −1, limpar o editor e o MemoSubtitleVO sem gravar desfazer.

### Save As não tira o estado de "modificado"
- Gravidade: média
- Original: main.pas:3337 (ActionSaveAsExecute) → main.pas:2706 (SaveSubtitles), que no fim faz `CurrentProject.IsDirty := False` (e `ApplyAutoBackupSettings`) mesmo quando o arquivo gravado é outro.
- .NET: ViewModels/MainViewModel.cs:448 (SaveAs) → 1383 (SaveTo), que não mexe em `IsDirty`.
- Diferença: no original, depois de "Save As" o título perde o "*", Save fica desabilitado e fechar não pergunta mais nada. No .NET o projeto continua modificado.
- Correção sugerida: em `SaveAs`, quando `SaveTo` der certo, fazer `IsDirty = false`, como o `SaveSubtitles` do original. Se a ideia for não copiar esse comportamento, registrá-lo no plano.

### Project Properties: trocar o WAV num projeto sem vídeo não reabre o áudio
- Gravidade: média
- Original: main.pas:3473–3519 (ActionProjectPropertiesExecute). Quando WAVMode ou WAVFile mudam, faz `AudioOnlyRenderer.Open(CurrentProject.WAVFile)`. Se isso falhar, tenta o vídeo (`KillVideo`) e, por fim, `Close`. No modo pwmPeakOnly, abre o áudio do vídeo.
- .NET: ViewModels/MainViewModel.cs:564–590 (ProjectProperties). Recarrega só os picos (`LoadPeaks`) e a duração. O motor só é reaberto quando `VideoSource` muda (linha 561).
- Diferença: num projeto só com WAV, trocar o arquivo WAV (ou o modo) nas propriedades atualiza a onda, mas Play continua tocando o WAV antigo. Se o projeto não tinha mídia, Play continua desabilitado.
- Correção sugerida: no bloco `waveChanged`, quando não há vídeo aberto, chamar `OpenMediaAsync(project)` (que já escolhe vídeo e, na falta dele, o WAV) e depois `UpdateLength()`.

### Project Properties: mudanças de cena do vídeo antigo ficam na onda
- Gravidade: média
- Original: main.pas:3452–3468. Se o novo vídeo não abre: `VideoRenderer.Close; ClearVideoSceneChange; SetVideoWidth(0)/SetVideoHeight(0)`.
- .NET: ViewModels/MainViewModel.cs:553–562 → OpenMediaAsync (MainViewModel.cs:1127–1165).
- Diferença: o `OpenMediaAsync` só mexe em `SceneChanges` quando abre um vídeo. Se o campo de vídeo for apagado, o arquivo não existir ou não abrir (ou só um WAV abrir), as mudanças de cena do vídeo anterior continuam desenhadas na onda e usadas no encaixe e pelos plugins (`context.SceneChanges`).
- Correção sugerida: em `OpenMediaAsync`, fazer `SceneChanges = []` (ou null) sempre que `IsVideoOpen` terminar falso, inclusive nos dois `return` antecipados.

### Project Properties: o vídeo só é escondido quando não sobra nenhuma mídia
- Gravidade: média
- Original: main.pas:3557–3561: `if (not VideoRenderer.IsOpen) and ShowingVideo then ActionShowHideVideo.Execute`.
- .NET: ViewModels/MainViewModel.cs:616: `if (!IsMediaOpen && ShowingVideo)`.
- Diferença: o original esconde o painel quando o **vídeo** não ficou aberto. O .NET só esconde quando nenhuma mídia está aberta. Se o vídeo for retirado e o WAV abrir no lugar, o painel de vídeo continua à mostra, vazio ou com o quadro antigo.
- Correção sugerida: testar `!IsVideoOpen && ShowingVideo`.

### Project Properties: com o vídeo trocado, a legenda sobre o vídeo não volta a aparecer
- Gravidade: baixa
- Original: main.pas:3469–3470: `if not ShowingSubs then ActionShowHideSubsExecute(nil);` depois de trocar o vídeo.
- .NET: ViewModels/MainViewModel.cs:553–562 (não há o equivalente; só o `LoadProject` faz isso, linhas 359–362).
- Diferença: se as legendas estavam escondidas (View > Show/Hide subtitles), trocar o vídeo nas propriedades as mostra de novo no original. No .NET elas continuam escondidas.
- Correção sugerida: depois de `OpenMediaAsync` no bloco do vídeo, fazer `if (!ShowingSubs) ShowHideSubs();`.

### Project Properties: troca de formato e autocorreção não refletem no "modificado"
- Gravidade: baixa
- Original: main.pas:3427–3446. Na troca de extensão com arquivo novo inexistente, `SaveSubtitles` deixa `IsDirty := False`, e o `LoadSRT` chamado por `LoadSubtitles` marca o projeto como alterado quando corrige tempos.
- .NET: ViewModels/MainViewModel.cs:517–541. `SaveTo` não zera `IsDirty`, e depois de `LoadDocument` o `_document.AutoCorrected` não é aplicado (no `LoadProject` ele é, na linha 397).
- Diferença: se o usuário respondeu "No" na pergunta e trocou o formato, o .NET continua com "*" depois de gravar e recarregar. Um SRT novo que precisou de autocorreção não fica marcado como alterado.
- Correção sugerida: depois de `SaveTo` bem-sucedido nesse ponto, `IsDirty = false`. Depois de `LoadSubtitles(_document)`, `if (_document.AutoCorrected) IsDirty = true;`.

### Save: falta a mensagem própria para a legenda do projeto sem extensão válida
- Gravidade: baixa
- Original: main.pas:2742–2752 (SaveSubtitles, chamado por ActionSaveExecute em main.pas:3315). Quando o arquivo é a própria legenda do projeto, a mensagem é: `Cannot save the file "X". Please, specify a valid file extension in the "Project Properties" dialog under the "File" menu.`
- .NET: ViewModels/MainViewModel.cs:1385–1388 (SaveTo) mostra sempre `... Please, specify a valid file extension.`
- Diferença: o texto da mensagem é diferente no Save, em CheckSubtitlesAreSaved e nas propriedades.
- Correção sugerida: em `SaveTo`, quando `target == Project?.SubtitlesFile`, usar o texto longo.

### Pergunta ao fechar: o .NET promove para UTF-8 e avisa; o original grava direto
- Gravidade: baixa
- Original: main.pas:4252 (CheckSubtitlesAreSaved). Com IDYES chama `SaveSubtitles(..., CurrentProject.IsUTF8, ...)` e `SaveProject`, sem `DetectCharsetDataLoss` e sem aviso.
- .NET: ViewModels/MainViewModel.cs:1307–1324 chama `Save()`, que faz `PromoteToUtf8IfNeeded` e mostra o aviso (linhas 431–434).
- Diferença: ao fechar ou abrir outro projeto com caracteres fora do ANSI, o .NET mostra o aviso "Subtitles contain some characters..." e grava em UTF-8. O original gravava em ANSI, perdendo esses caracteres. O comportamento do .NET evita a perda, mas difere do 1.0.1.
- Correção sugerida: manter, mas registrar como diferença intencional no plano. Para fidelidade estrita, chamar o caminho de gravação sem a promoção.

### AutoFitSubsListColumns não portado
- Gravidade: baixa
- Original: main.pas:4649 (AutoFitSubsListColumns), chamado em ApplyFontSettings (main.pas:4674) e em ActionAddSubtitleExecute quando a lista passa a ter 1 legenda (main.pas:5235–5238).
- .NET: não portado (Views/MainWindow.xaml.cs:275 ApplyFontSettings e ViewModels/MainViewModel.Timing.cs:132 AddSubtitle não ajustam as colunas).
- Diferença: no original, as colunas Index, Start..Style e as que ficam depois do VO se ajustam ao conteúdo quando a fonte da lista muda e quando entra a primeira legenda. No .NET as larguras ficam como estavam.
- Correção sugerida: implementar o ajuste (largura pelo conteúdo, sem Text e VO) na janela e chamá-lo nesses dois pontos.

### Inserir legenda não corrige a posição da busca
- Gravidade: baixa
- Original: main.pas:5143–5145 (AddSubtitle): `if (SearchNodeIndex >= NewNode.Index) then Inc(SearchNodeIndex);`.
- .NET: ViewModels/MainViewModel.Edit.cs:210 (InsertSubtitle) não mexe em `_searchNodeIndex` (MainViewModel.Find.cs:17).
- Diferença: depois de um Find, inserir uma legenda antes da encontrada faz o próximo F3 recomeçar na legenda errada (uma antes) no .NET.
- Correção sugerida: em `InsertSubtitle`, `if (_searchNodeIndex >= index) _searchNodeIndex++;`.

### Replace All sem nenhuma ocorrência não marca o projeto como alterado
- Gravidade: baixa
- Original: main.pas:3869 (ReplaceAllTextSubs). Cria um `TChangeSubData` para cada legenda percorrida. Com `GetCount > 0`, empilha a tarefa e faz `CurrentProject.IsDirty := True`, mesmo sem nenhuma troca.
- .NET: ViewModels/MainViewModel.Find.cs:244–267. Empilha a tarefa, mas `ApplyTextChanges` só marca alterado por mudança de tempo, e o setter de `Text` ignora texto igual (SubtitleItemViewModel.cs:45).
- Diferença: com legendas na lista e zero trocas, o original passa a mostrar "*" e o .NET não (os dois empilham um desfazer sem efeito).
- Correção sugerida: depois de `DoAndPush(new MultiChangeTask(...))` em `ReplaceAll`, fazer `IsDirty = true`, como o original. Se isso não for desejado, registrar a diferença.

### Correção por plugin: valor "antigo" guardado na primeira mudança, não na última
- Gravidade: baixa
- Original: main.pas:5952, 5976 e 6000 (OnSubtitleRangeJSWrapperChangeStart/Stop/Text): `ChangeSubData.OldStart := SubtitleRange.StartTime` a cada chamada, e o mesmo para OldStop e OldText.
- .NET: ViewModels/MainViewModel.Errors.cs:243–268: `change.OldStart ??= sub.StartMs` (guarda só a primeira).
- Diferença: quando dois plugins mudam o mesmo campo da mesma legenda numa correção, desfazer no original volta ao valor intermediário. No .NET volta ao valor de antes da correção. O .NET está mais correto, mas é uma diferença que não está registrada.
- Correção sugerida: registrar no plano (seção de plugins) como correção deliberada, ou trocar `??=` por `=` para fidelidade estrita.

---

Rotinas conferidas: 128 das 128 da fatia (main.pas 3302–6588), comparadas com o .NET. Sem comparação detalhada linha a linha: `SetShortcut` (tabela de atalhos, área do M4), `Splitter1CanResize` e `SwapSubList` (só layout, conferido por alto), `ApplyFontSettings` (métricas de fonte), `MemoSubtitleTextMouseDown`/`TntFormActivate` (contorno do RichEdit sem equivalente no WPF), `MenuItemAboutClick`/`MenuItemHelpIndex*` (registrados no plano). Das rotinas de plugin (`ActionCheckErrors`, `FixErrorInList`, `CanFixError`), só conferi as condições de habilitação e o fluxo, não o motor JS.
