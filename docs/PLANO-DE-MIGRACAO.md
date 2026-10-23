# Plano de migração do VisualSubSync

## Decisões já tomadas

| Camada | Escolha |
| --- | --- |
| Plataforma | Windows 10 e 11 |
| Linguagem e runtime | C# com .NET 10 LTS |
| Interface | WPF |
| Organização da interface | MVVM com CommunityToolkit.Mvvm |
| Reprodução de mídia | libmpv para reprodução; FFmpeg para áudio e quadros-chave (M2) |
| Forma de onda | Controle próprio, com desenho otimizado |
| Compatibilidade | Preservar arquivos, projetos e funcionalidades do original |

Todo o trabalho é local, em `C:\Projetos\VisualSubSync.NET`. O original em
`C:\Projetos\VisualSubSync` é só leitura e serve de especificação de comportamento.

## Princípios

1. O código Delphi é a especificação. Cada função portada cita a unidade e a rotina de origem.
2. Arquivos do usuário (`.vssprj`, `.peak`, `.scenechange`, legendas, `VisualSubSync.ini`,
   plugins, dicionários) abrem sem conversão e, ao salvar, continuam abrindo no original.
   Chaves e seções desconhecidas são preservadas.
3. Núcleo sem dependência de interface nem de Windows, testado por arquivos de referência
   gerados pelo próprio VisualSubSync 1.0.1.
4. O motor de mídia fica atrás de `IMediaEngine`; trocar de motor não toca a interface.

## Estrutura da solução

| Projeto | Alvo | Conteúdo |
| --- | --- | --- |
| `VisualSubSync.Core` | net10.0 | Modelo de legendas, formatos, projeto, picos, mudanças de cena, preferências, undo, verificação de erros |
| `VisualSubSync.Media` | net10.0 | Contratos `IMediaEngine`, `IAudioExtractor`, `IKeyframeReader`; depois, a implementação escolhida |
| `VisualSubSync.Waveform` | net10.0-windows | Controle WPF da forma de onda |
| `VisualSubSync.App` | net10.0-windows | Janelas, view models, serviços, composição |
| `VisualSubSync.Core.Tests` | net10.0 | Testes xUnit v3 |
| `VisualSubSync.App.Tests` | net10.0-windows | Regressões dos comandos WPF em thread STA, com diálogos e motor substituídos por doubles |
| `VisualSubSync.Media.Tests` | net10.0 | Regressões nativas com FFmpeg/libmpv e áudio sintético, sem áudio/vídeo no dispositivo |
| `poc\` | | Provas de conceito dos motores de mídia, fora da solução principal |

## Mapa de módulos

| Original | Destino | Abordagem |
| --- | --- | --- |
| `SubtitleModel.pas`, `SubStructUnit.pas` | Core/Subtitles | Classes C#; testes portados de `dunit\SubtitleModelTests.pas` |
| `SRTParserUnit.pas` | Core/Subtitles/Formats/SrtFormat | Iniciado; faltam as tolerâncias do parser original |
| `SSAParserUnit.pas`, `StyleFormUnit.pas` | Core/Formats/SsaAssFormat + janela de estilos | Preservar campos e seções desconhecidos |
| `SaveSubtitlesAsCUE/TXT/CSV` | Core/Formats (somente gravação) | Saída byte a byte igual ao original |
| `ProjectUnit.pas`, `SaveProject` | Core/Projects | **Feito**: leitura e gravação compatíveis, testadas |
| Picos em `WAVDisplayerUnit.pas` | Core/Audio/PeakFile | **Feito**: formato binário compatível e normalização (`NormalizePeakTab`), testados |
| `WAVFileUnit.pas` | Core/Audio/WavReader | Leitura PCM 8, 16 e 32 bits float e geração de picos idêntica |
| `SceneChangeUnit.pas` (arquivo) | Core/Scenes | **Feito**: `.scenechange` compatível, testado |
| `SceneChangeUnit.pas`, `MatroskaHelper.pas`, `MP4File.pas`, `VfW.pas` | Media/IKeyframeReader | Ler quadros-chave pelo motor escolhido ou por FFmpeg; comparar com o resultado do original |
| `WAVDisplayerUnit.pas`, `MiniScrollBarUnit.pas` | Waveform | **Iniciado**: duas camadas (onda e sobreposição), uma coluna por pixel físico. Faltam régua, zonas de silêncio, mudanças de cena, karaokê, arrastar bordas, mini barra, modos de mouse |
| `Renderer.pas`, `DetachedVideoFormUnit.pas` | Media (mpv) + VideoSurface e DetachedVideoWindow | **Feito no código**; falta verificar na tela |
| `WAVExtractFormUnit.pas`, `WavWriter\` | Media/IAudioExtractor | Decodificação pelo motor ou FFmpeg; elimina o filtro COM |
| `DirectVobsubInterface.pas` | `IMediaEngine.SetSubtitleOverlay` | Renderização de legendas do próprio motor (libass no mpv ou FFmpeg) |
| `UndoableTaskUnit.pas`, `UndoableSubTaskUnit.pas` | Core/Undo | Padrão Command, mesmas granularidades |
| `main.pas` (ações) | App/ViewModels + `RelayCommand` | Uma ação por comando, com os mesmos nomes; atalhos configuráveis nos modos normal e timing |
| `main.dfm`, demais `.dfm` | App/Views (XAML) | Mesma disposição e textos; o inventário de menus em `VisualSubSync\modern\migration\main-form.json` pode ser reaproveitado |
| Virtual Treeview | `DataGrid` virtualizado ou `ListView` com `GridView` | Medir com 5.000 legendas |
| `TntRichEditCustomUndoUnit.pas`, `tom_TLB.pas` | Editor de texto | AvalonEdit (MIT) ou `RichTextBox`; decidir pela verificação ao digitar |
| `JavaScriptPluginUnit.pas`, SpiderMonkey | Core/Plugins com Jint (motor JavaScript em .NET puro) | Mesma API (VSSCore, VSSPlugin, SceneChange, Subtitle); rodar os plugins de `Release\jsplugin` sem alteração |
| `ErrorReportFormUnit.pas` | App/Views/ErrorReport | Depende dos plugins |
| `LibHunspellUnit.pas`, `SpellCheckFormUnit.pas` | Core/Spelling com WeCantSpell.Hunspell (.NET puro) | Mesmos `.aff`/`.dic` e `perso.dic` |
| `ServerUnit.pas`, `PageProcessorUnit.pas`, `DynamicPageProcessorUnit.pas` | App/WebServer com `HttpListener` ou Kestrel | Servir `Release\web` e as mesmas páginas `.shtml` |
| `PreferencesFormUnit.pas`, `MRUListUnit.pas` | Core/Settings + janela de preferências | Ler e gravar o mesmo `VisualSubSync.ini` |
| `VSSClipboardUnit.pas` | App/Services/Clipboard | Mesmo formato `CF_VisualSubSync` |
| `FindFormUnit`, `GotoFormUnit`, `DelayFormUnit`, `SilentZoneFormUnit`, `VerticalScalingFormUnit`, `TranslateFormUnit`, `SuggestionFormUnit`, `AboutFormUnit`, `LogWindowFormUnit` | App/Views | Diálogos com MVVM |
| `VisualSubSync.nsi` (NSIS) | Publicação | `dotnet publish` autocontido; instalador definido no fim |
| FastMM, JCL, DeCAL, RegExpr, WinSock2 | Biblioteca padrão do .NET | Sem migração |

## Prova de conceito do motor de mídia

### O que o motor precisa fazer (extraído de `Renderer.pas` e `main.pas`)

1. Tocar um trecho e parar exatamente no fim (Play, Loop, Play selection), com erro abaixo de 1 quadro.
2. Informar a posição com frequência suficiente para o cursor amarelo andar sem saltos.
3. Mostrar o quadro exato de um tempo (Show start frame, Show stop frame) em vídeo CFR e VFR.
4. Velocidade de 60% a 100%.
5. Vídeo embutido na janela e em janela destacada.
6. Legendas sobrepostas atualizadas durante a edição, incluindo ASS.
7. Decodificar o áudio para gerar `.peak` e WAV.
8. Ler quadros-chave para as mudanças de cena.
9. Abrir AVI, MKV, MP4 e os demais formatos comuns sem codecs instalados à parte.

### Candidatos

| Candidato | Pontos fortes | Riscos |
| --- | --- | --- |
| **libmpv** (via P/Invoke ou Mpv.NET) | Busca precisa por quadro (`hr-seek`), passo de quadro, libass para ASS, controle de trecho com `ab-loop`, ampla compatibilidade de formatos | Integração WPF por `HwndHost` (airspace: nada de WPF desenhado sobre o vídeo); DLL de cerca de 30 MB |
| **LibVLCSharp** (VLC 3) | Maduro, pacote NuGet oficial, controle WPF pronto | Busca por quadro menos precisa, posição atualizada em intervalos grandes, mesmo problema de airspace |
| **FFmpeg** (Sdcb.FFmpeg ou FFmpeg.AutoGen) com saída própria (WASAPI e D3D11 ou `WriteableBitmap`) | Controle total, decodificação de áudio e quadros-chave necessária de qualquer forma | Mais código: sincronização áudio/vídeo e legendas ficam por nossa conta |
| **Media Foundation** (Windows, via Vortice ou `MediaPlayer` do WinRT) | Nativo, sem DLL extra, aceleração por hardware | Depende das extensões de codec do Windows (HEVC pago, alguns MKV), busca exata limitada |

O `MediaElement` do WPF fica fora: usa o Windows Media Player antigo e não oferece busca precisa.

### Método

1. Um projeto por candidato em `poc\`, todos implementando `IMediaEngine`.
2. Mesmo conjunto de vídeos locais: AVI XviD, MKV H.264, MP4 H.264 e HEVC, um CFR e um VFR,
   com legenda ASS.
3. Medir: erro de parada em 200 trechos aleatórios, erro do quadro exibido após busca (comparando
   com o quadro decodificado pelo FFmpeg), latência de busca, uso de CPU, tamanho distribuído.
4. Registrar os números neste documento e decidir.

### Resultados da prova de conceito

Implementação em `poc\` (ver `poc\README.md`). Foram feitas duas rodadas, ambas com relatório
completo em `docs\medicoes\`.

Primeira rodada **fora do Windows** (Linux, mpv 0.37, FFmpeg 6.1, sem vídeo nem áudio reais), com
vídeos sintéticos de 60 s:

| Requisito | MKV 25 fps | MP4 23,976 fps | MKV VFR 25/50 fps |
| --- | --- | --- | --- |
| Quadro exibido após busca exata, dentro de 1 quadro | 100% | 100% | 100% |
| Erro da busca, p95 | 33 ms | 35 ms | 31 ms |
| Latência da busca, p95 | 35 ms | 29 ms | 31 ms |
| Parada no fim do trecho, p95 (positivo = depois do fim) | 19 ms | 21 ms | 39 ms |
| Atualizações de posição por segundo | 25 | 25 | 25 a 51 |
| Velocidade 60% / 80% (tempo real para 2 s de mídia) | 3303 / 2502 ms | | |
| Picos do arquivo inteiro (FFmpeg) | 241 ms | 148 ms | 195 ms |
| Quadros-chave do arquivo inteiro (FFmpeg) | 14 ms | 16 ms | 18 ms |
| Legenda ASS carregada e recarregada | sim | | |

Segunda rodada **no Windows** (.NET 10, libmpv de 2026-10-18, FFmpeg 7.1), com vídeos reais e
legenda ASS real, em [`windows-avi-xvid.md`](medicoes/windows-avi-xvid.md) e
[`windows-mp4-h264.md`](medicoes/windows-mp4-h264.md):

| Requisito | AVI XviD 29,97 fps (23 min) | MP4 H.264 29,97 fps (97 min) |
| --- | --- | --- |
| Quadro exibido após busca exata, dentro de 1 quadro | 100% | 100% |
| Erro do quadro exibido, p95 | 26,6 ms | 26,9 ms |
| Latência da busca, p95 | 113 ms | 90 ms |
| Parada no fim do trecho, p95 (positivo = depois do fim) | 13 ms | 17 ms |
| Atualizações de posição por segundo | 30,4 a 31,2 | 30,5 a 31,6 |
| Velocidade 60% / 80% (tempo real para 2 s de mídia) | 3309 / 2511 ms | 3336 / 2522 ms |
| Picos do arquivo inteiro (FFmpeg) | 3131 ms (44,1 kHz, 2 canais) | 27601 ms (48 kHz, 6 canais) |
| Quadros-chave do arquivo inteiro (FFmpeg) | 231 ms (452) | 906 ms (1390) |
| Legenda ASS carregada e recarregada | sim | sim |

Leitura até aqui:

- A busca exata do motor atende às medições registradas, inclusive em vídeos reais no Windows
  (AVI XviD e MP4 H.264). Isso não valida por si só as ações "Show start/stop frame" da janela:
  elas precisam ser conferidas na integração com vídeo e áudio reais (ver revisão de 20/10/2026).
- A parada no fim do trecho errou no máximo ~17 ms, cerca de meio quadro a 30 fps. Continua sendo
  detectada pela posição informada a cada quadro. Se no som real isso se mostrar perceptível, a
  alternativa é usar `ab-loop-b` do mpv ou pausar antecipando a latência de áudio medida. A
  confirmação depende da escuta no player de teste.
- O FFmpeg resolve os quadros-chave em menos de 1 s para o arquivo inteiro. Os picos custam uma
  passagem completa pelo áudio: 3 s para 23 min em estéreo e 28 s para 97 min em 5.1. Para arquivos
  longos com áudio multicanal, vale reaproveitar o `.peak` existente ou gerar em segundo plano.
- Pendentes no Windows: HEVC; integração do vídeo na janela WPF (airspace, escala de tela) e
  sincronia do cursor com o som real, ambos a avaliar no player de teste; tamanho final da
  distribuição.

### Recomendação inicial

Testar primeiro **libmpv para reprodução** junto com **FFmpeg para extração de áudio e quadros-chave**.
É a combinação com mais chance de cumprir os itens 1, 3 e 6, e o FFmpeg já resolve os itens 7 e 8
para qualquer motor. LibVLCSharp entra como comparação. Media Foundation só se a distribuição sem
DLLs extras pesar mais que a compatibilidade de formatos.

Licenças: o VisualSubSync é GPL v2 (`Copying.txt`); mpv, VLC e FFmpeg (LGPL ou GPL) são compatíveis; o WeCantSpell.Hunspell tem licença tripla MPL, GPL ou LGPL.

## Diretriz de fidelidade

Tudo o que for possível (interface, layout, textos, atalhos, cores, comportamento e fluxo) deve
ficar igual ao VisualSubSync original, tendo como referência o código em `C:\Projetos\VisualSubSync`,
o `INVENTARIO.md` e este plano. Diferenças deliberadas devem ser pequenas, justificadas e
registradas aqui. Funções ainda não portadas aparecem no lugar original, desabilitadas.
A mesma regra está em `docs/Visual_Projeto.md`.

### Diferenças registradas na janela principal

- A janela segue o `TMainForm` do VisualSubSync Enhanced 1.2.29 (`docs/enhanced/TMAINFORM.txt`), que é a
  versão em uso: aba "Subtitle", painel de controle de 88 px, rótulos Begin/End/Length em destaque,
  botão Project Properties, painéis Quick & Smart Delay, Silent Zone e Smart Desynch (visíveis conforme
  `[Misc] QuickSmartSilentZonePanels`) e os menus extras. O comportamento continua vindo do código 1.0.1.
- Funções exclusivas do Enhanced (ajuste rápido de atraso, zonas de silêncio, ferramenta de
  dessincronização, adaptação de fps, correção da forma de onda, dicionários on-line, Zip Subtitle)
  aparecem no lugar, desabilitadas, até serem portadas e verificadas. Desde 21/10/2026 há fonte
  1.2.33.0 em `C:\Projetos\VisualSubSyncEnh\fontes`; o executável de referência continua sendo 1.2.29.
- A coluna RS da lista vem de um plugin JavaScript (`VisibleExtraColumns`) e entra com os plugins (M6).
- Reprodução, vídeo e o modo Normal/Timing dependem do motor de mídia e de outros marcos:
  aparecem no lugar original, desabilitados. Sem vídeo aberto o painel de vídeo fica oculto.
- Atalhos são os do formulário; os personalizados em `[Hotkeys]` do `VisualSubSync.ini` entram com as
  preferências (M7).
- Ícones de menu desabilitados ficam esmaecidos (opacidade) em vez do relevo cinza que a VCL desenha.
- O TUpDown dos campos do Silent Zone é desenhado com dois botões de repetição.
- Posição e tamanho da janela no `VisualSubSync.ini` são gravados em unidades do WPF, que coincidem
  com pixels a 100% de escala.

### Diferenças registradas no motor de mídia e no vídeo (M3)

Fatias 2 e 3 do M3: motor ligado à janela, reprodução, cursor e painel de vídeo.

- **Motor:** libmpv em processo (`libmpv-2.dll` ao lado do executável), como recomendado na prova de
  conceito. Quando a DLL não está presente, `AttachMedia` só registra um aviso e o programa continua
  abrindo projetos, editando legendas e salvando (sem reprodução nem vídeo).
- **Painel de vídeo:** embutido por `HwndHost` sobre a janela nativa do mpv, e não por um
  `TWinControl` do VCL. O resto segue o DFM: `PanelVideo` preto, `alRight`, oculto até "Show video" e
  `SplitterWAVDisplay_Video` de 4 px (`clGray`). O `PanelVideo.Enabled := Enable` do legado vira
  visibilidade do painel.
- **Largura inicial do painel:** igual ao legado (`UpdateVideoRendererWindow`): usa
  `CurrentProject.VideoPanelWidth` quando maior que zero; senão calcula pela proporção real do vídeo
  (`largura/altura`) com reserva de 201 px quando o tamanho ainda não é conhecido.
- **"Detach video window" (`ActionDetachVideo`, `TDetachedVideoForm`):** a janela onde o mpv desenha
  é uma só e muda de pai entre o `PanelVideo` e a janela destacada, como o `SetDisplayWindow` do legado.
  A janela destacada é criada com a principal e segue o DFM: sem título, borda de redimensionar, fundo
  preto, sempre por cima, 320x240 e sem tomar o foco ao aparecer. Arrastar com o botão esquerdo a move,
  o duplo clique volta o vídeo para o painel e o botão direito abre o menu Fullscreen/Normal size (tela
  cheia no monitor da janela, com a posição normal guardada e gravada no projeto em
  `VideoWindowNormalLeft/Top/Width/Height`). O estado vai para `DetachedVideo` no projeto; a posição da
  janela vai para `[Windows] DetachedVideoForm_*` e o estado final para `[Windows] DetachedVideo` e
  `ShowVideo`, como no `SaveSettings`. Mostrar ou esconder o vídeo também grava `ShowVideo` no projeto.
  Para os cliques chegarem à janela, as janelas que o mpv cria dentro da superfície ficam desabilitadas
  (no legado, o `PanelVideo` também era desabilitado); por isso o duplo clique no painel, que no legado
  não disparava por o painel estar desabilitado, continua sem efeito. Fechar a janela destacada com
  Alt+F4 só a esconde, como um formulário do Delphi. Ainda não verificado na tela.
- **`UpdatePlayRange` / `TPlayingModeType`:** portados no M4 (fatia 1): a mudança da seleção durante a
  reprodução atualiza o trecho conforme o modo (All, Selection, SelectionStart, SelectionEnd).
- **Fim do arquivo:** com `keep-open`, o mpv pausa no último quadro sem chegar ao ponto de parada; o motor
  observa `eof-reached` e encerra (ou repete, em Loop) o trecho como o `OnStopPlaying` do legado.
- **Velocidade (Playback Rate):** 60 a 100%, como no legado, aplicada no motor; o áudio é corrigido em
  tom (`audio-pitch-correction`) para o mesmo efeito do DirectShow.

### Diferenças registradas no fluxo de projeto e na extração (M3)

Fatia 4 do M3: novo projeto, propriedades do projeto e extração de WAV/picos.

- **`TProjectForm` (`ProjectUnit.pas`):** portado em `ProjectFormWindow` (XAML sobre `Canvas` com as
  coordenadas do DFM do Enhanced). Os dois modos ("New Project" e "Project Properties"), as mensagens
  de `CheckData`, a confirmação de sobrescrita do `.vssprj` e o rodapé "Leave empty for extraction" são
  os do original.
- **Criação do `.vssprj`:** `ActionNewProjectExecute` grava o arquivo com onze chaves e só depois chama
  `LoadProject`. Aqui o projeto é montado e gravado por `VssProjectFile.Save` (que escreve todas as
  chaves conhecidas de uma vez); como `SaveProject` também regrava tudo na primeira gravação, o
  resultado converge para o mesmo arquivo. Os padrões `DetachedVideo`, `ShowVideo` e `Dictionnary` vêm
  do INI (`StartupDetachVideo`/`StartupShowVideo`/`StartupDictionnary`), como no legado.
- **IsUTF8:** no legado fica em `CurrentProject.IsUTF8`; aqui continua em `SubtitleDocument.IsUtf8`
  (o `.vssprj` não tem essa chave). O valor marcado no formulário é aplicado ao documento depois de
  `LoadProject`, exatamente na ordem do original.
- **"Project Properties":** portado de `ActionProjectPropertiesExecute`, comparando campo a campo:
  arquivo de legendas (com troca de formato do mesmo arquivo e `PreviousExtension`), VO, fonte de
  vídeo (reabre a mídia e zera a largura do painel), modo/arquivo WAV e arquivo de picos (recarrega os
  picos), nome do projeto e codificação; grava só quando algo mudou e esconde o vídeo quando nenhum
  ficou aberto.
- **Legenda de referência (VO):** trocar o arquivo no formulário recarrega o VO (M7; veja "Diferenças
  registradas no VO e na tradução").
- **`TExtractWAVForm` (`WAVExtractFormUnit.pas`):** portado em `ExtractWavWindow`, com o memo de
  informações, a barra de progresso (0–1000, como `ProgressBar1`), as três opções de conversão e as
  mensagens do original. A extração usa o `FFmpegAudioExtractor` (contagem de fluxos + picos) no lugar
  do grafo DirectShow `WavWriter`, e o botão "Debug" mostra o backend FFmpeg em vez da lista de
  filtros. O `.peak` é gravado por `PeakFile.Save` em `ChangeFileExt(destino, '.peak')`, como no
  legado. "Stop" cancela por `CancellationTokenSource` (o original abortava o grafo).

### Diferenças registradas na forma de onda (M4)

Fatia 1 do M4: mouse do `TWAVDisplayer` (`WAVDisplayerUnit.pas`), portado em `WaveformMouse` (Core, testado
sem WPF) e ligado ao `WaveformControl`.

- **Comportamento igual ao original:** clique simples move o cursor e limpa a seleção (seleções menores
  que 40 ms são descartadas no MouseUp); duplo clique seleciona a legenda inteira; Shift+clique estende o
  lado mais próximo; borda dinâmica a 4 px (depois 2 px) com cursor `crHSplit` e linha cheia; arrasto da
  borda altera os tempos ao vivo e, no MouseUp, reordena a lista (`FullSortTreeAndSubList`); encaixe
  (`EnableMouseSnapping`, desligado com Ctrl, 8 px) no espaço mínimo das vizinhas e nas mudanças de cena
  (offsets 130/130, filtro 250); anti-sobreposição (`ClipCursor` com folga de −2/+3 px); botão do meio
  rola a página (×4 com Shift); modo SSA (esquerdo = início, direito = fim).
- **Mudanças de cena:** carregadas depois de abrir o vídeo (`.scenechange` ao lado do vídeo ou quadros-chave
  extraídos pelo FFmpeg e gravados), pintadas na cor `$36C9FF` com faixas alfa 80; "Show/Hide scene change"
  ligado ao menu e a `[WAVDisplay] ShowSceneChange`.
- **`ClipCursor`/`SetCursorPos`:** feitos em pixels do dispositivo, convertidos a partir das coordenadas do
  WPF; a 100% de escala coincidem com o original.
- **Karaokê:** os marcadores e o arraste chegaram no M5 (fatia 7, abaixo).

### Diferenças registradas no modo Normal/Timing e nos atalhos (M4)

Fatia 2 do M4: `bttWorkingMode`/`ActionToggleTimingMode`, `SetShortcut` e as ações de sincronização.

- **Igual ao original:** o botão mostra "Normal" ou "Timing" (vermelho, afundado) e fica habilitado junto
  com Play; no modo Timing a seleção do mouse vira SSA se `[Mouse] EnableSSATimingMode` estiver ligado, o
  editor fica desabilitado com `[Misc] DisableSubtitleEdition` (padrão) e Play/Loop/Play to end/Loop
  selection start/end põem o foco na forma de onda.
- **Atalhos:** a tabela vem das 127 ações com `Tag = 1` do `TMainForm` do Enhanced, com o atalho do
  formulário no modo Normal e `DefaultTimingShortcuts` no Timing (Esc, F1, F4, Ctrl+S, Ctrl+Z, Ctrl+Y);
  `[Hotkeys] Acao[Normal]`/`Acao[Timing]` do `VisualSubSync.ini` sobrepõem, em texto (`ShortCutToText`) ou
  inteiro (`UseIntegerShortcut`). A tecla é testada antes do controle focado, como o `IsShortCut` da VCL, e
  só a primeira ação com o atalho conta. Os nomes de teclas especiais vêm do `GetKeyNameText` do Windows,
  como no Delphi. Os menus mostram o atalho do modo atual.
- **Ações portadas:** Clear selection, Set subtitle time, Set subtitle start/stop time (e "go to next"),
  Shift start/stop ±100/±10 ms, Select next/previous sub, Play 1s before (e até o fim), WAVDisplay scroll
  left/right, Add subtitle, Start/Stop/Stop and start sub, Set editor focus (e select), Pause, e a criação
  ou ajuste de legendas segurando Espaço (`EnableToggleCreation`, `SpaceKeyModifyTiming`,
  `SpaceKeyCPSTarget`, `SpaceKeyMinimalDuration`, `SpaceKeyBlankBetweenSubtitles`). "Play next/previous
  sub" agora param, selecionam e tocam, como `ActionNextSub`/`ActionPreviousSub`.
- **Diferenças:** com um trecho de karaokê selecionado, essas ações agem sobre ele desde a fatia 7 do
  M5. Ações sem porte não reagem
  ao atalho, como uma ação desabilitada. O editor de atalhos está na aba Hotkeys das preferências (M7).

### Diferenças registradas no desfazer/refazer (M5)

Fatia 1 do M5: `UndoableTaskUnit.pas`, `UndoableSubTaskUnit.pas` e `TntRichEditCustomUndoUnit.pas`.

- **Igual ao original:** uma pilha de desfazer e uma de refazer (`PushUndoableTask` limpa o refazer e
  tenta juntar com o topo); as duas são limpas ao fechar, recarregar ou abrir. Entram no desfazer o arraste
  dos tempos na forma de onda e Set subtitle time (`TUndoableSetTimeTask`), Shift start/stop
  (`TUndoableDelayTask` só no início ou só no fim), Add subtitle (`TUndoableAddTask`) e a edição de texto
  no editor (`TUndoableSubTextTask`), com digitação de 1 caractere, Backspace e Delete seguidos juntos numa
  tarefa só; uma tarefa desfeita não junta mais nada. Set subtitle start/stop time, Stop sub e a criação
  com Espaço não geram tarefa, como no original. Desfazer texto foca a legenda e restaura a seleção.
- **Diferenças:** o original junta digitação mesmo se a legenda focada mudou entre as teclas; aqui só se
  junta na mesma legenda (no original isso desfaria o texto da legenda errada). A seleção depois de refazer
  é calculada pelo fim do trecho novo, em vez de lida do editor após a mudança; para digitar, colar e
  apagar o resultado é o mesmo. O desfazer próprio do `TextBox` fica desligado, como no `TTntRichEditCustomUndo`
  (que ignora `WM_UNDO`), para que Ctrl+Z use a pilha do programa.

### Diferenças registradas em apagar, mesclar e dividir (M5)

Fatia 2 do M5: `pmiSubListDeleteClick`, `WAVDisplayPopup_DeleteRange`, `TMainForm.Merge`/`MergeSubtitles`,
`ActionSplitAtCursor`/`SplitSubtitle` e as tarefas `TUndoableDeleteTask`, `TUndoableMergeTask` e
`TUndoableSplitTask`.

- **Igual ao original:** os menus de contexto da lista (`SubListPopupMenu`) e da forma de onda
  (`WAVDisplayPopupMenu`), sem imagens, com os itens habilitados pelas mesmas regras de
  `SubListPopupMenuPopup` e `WAVDisplayPopupMenuPopup`; no modo SSA o menu da forma de onda só abre com
  Shift (`DoContextPopup`). Del apaga as selecionadas na lista. A mesclada é uma cópia da primeira (estilo,
  ator...) com o fim mais tardio; Merge dialog põe "- " em cada linha e Merge on 1 line junta numa linha
  só, como `MergeSubtitles`. Dividir no cursor usa `SpaceKeyBlankBetweenSubtitles` como intervalo e a
  segunda parte repete o texto. Merge with previous/next, Merge on 1 line e Split at cursor respondem aos
  atalhos de `[Hotkeys]`. Tudo entra no desfazer.
- **Diferenças:** sem nenhuma legenda selecionada, Merge não faz nada (o original leria fora da lista).
  Desfazer a mescla apaga a mesclada pela posição em que ela entrou, e não pela da primeira original; só
  difere quando há legendas com o mesmo início. Os itens ainda sem porte (Fix errors, estilos do
  texto, Strip tags, Save selected As, Create from VO, Wrap around Scenechange,
  inserir/apagar mudança de cena) aparecem desabilitados; o karaokê chegou na fatia 7.

### Diferenças registradas na janela de atraso (M5)

Fatia 3 do M5: `TDelayForm` (`DelayFormUnit.pas`) e `ActionDelayExecute`, no menu Edit, no menu de
contexto da lista e em Ctrl+D.

- **Igual ao original:** a janela tem o tamanho e a posição dos controles do formulário; o campo de tempo
  segue a máscara `!99:99:99.999;1;0` (só dígitos, escritos por cima, posição apagada volta a "0"); Esc
  fecha e não há botão padrão para Enter. Com mais de uma legenda selecionada a janela abre em "Selected
  subtitles" e, com seleção na forma de onda, o atraso já vem com a duração dela. "All subtitles from
  cursor to end" vai da legenda focada até a última. O atraso entra no desfazer (`TUndoableDelayTask`) e
  não limita tempos negativos, como no original.
- **Diferenças:** o separador de horas é sempre ":" (o original usa o `TimeSeparator` do Windows na tela
  e troca de volta por ":" ao ler; em português o resultado é o mesmo). O atraso dos tempos de karaokê
  (`SubTime`) entra com o karaokê.

### Diferenças registradas na busca e no Go to (M5)

Fatia 4 do M5: `TFindForm` (`FindFormUnit.pas`), `TGotoForm` (`GotoFormUnit.pas`), `ActionFind`,
`ActionFindNext`, `SearchSubtitles`, `ReplaceTextSubs`, `ReplaceAllTextSubs`, `ShowMatchingSubOnly` e
`ActionGoto`, com `WideStringFind`, `WideStringCount` e `Tnt_WideStringReplace` em `TextSearch` (Core).

- **Igual ao original:** a janela Find tem o layout do Enhanced, fica sempre por cima, não é modal e,
  fechada, só se esconde (histórico da busca e opções ficam); o título é "Find subtitle". Find/F3 seguem
  da legenda e posição do último resultado (ou da focada, com "From cursor"), focam a legenda e
  selecionam o trecho no editor; sem resultado, "No more items." na barra de status. Replace troca o
  trecho selecionado (com desfazer de texto); Replace All troca em todas (ou da focada até o fim) numa
  tarefa de desfazer e informa "N item(s) replaced." contando como `WideStringCount` (ocorrências que se
  sobrepõem contam). "Whole Word" fica desabilitado com expressão regular. "Show matching sub only"
  esconde as que não casam e informa "N subtitle(s) match."; ao fechar a janela todas voltam. A janela
  Go to aceita linha ou tempo (máscara `!99:99:99.999`), Enter confirma e Esc fecha; pelo tempo, vai para
  a legenda que contém o tempo ou a primeira depois dele e move o cursor; sem legendas, mostra 20 s em
  volta do tempo.
- **Diferenças:** a expressão regular é a do .NET, e não a do `TRegExpr`. Os modificadores padrão são os
  mesmos (ponto pega quebra de linha; "i" sem "Case sensitive"), e `$1` funciona igual na troca. Uma
  expressão inválida mostra a mensagem de erro e não busca, em vez da exceção do original. "Disable VO
  search" é só do Enhanced (o 1.0.1 não busca no VO) e fica marcado e desabilitado. A busca no text pipe ("Find text") chegou
  com a fatia 6 (abaixo). Go to pela linha sem legendas não faz nada (o original leria fora da lista). O editor
  mostra o trecho achado mesmo sem foco, para que ele apareça com a janela Find ativa.

### Diferenças registradas na área de transferência (M5)

Fatia 5 do M5: `VSSClipboardUnit.pas`, `ActionCopy`, `ActionPaste` e `ActionPasteAtCursor`.

- **Igual ao original:** com a lista focada, Ctrl+C copia as selecionadas no formato `CF_VisualSubSync`
  (mesmo layout binário: versão 1, quantidade, início, fim e os nove textos em UTF-16, por isso dá para
  copiar do programa original e colar aqui, e o contrário) e como texto SRT numerado pela lista; Ctrl+V
  cola o formato próprio ou, na falta dele, texto SRT ou SSA. "Paste at cursor" (menu da forma de onda)
  desloca as coladas para a primeira começar no cursor. Colar entra no desfazer
  (`TUndoableMultiAddTask`). No editor, copiar e colar são os do próprio campo de texto.
- **Igual ao original (23/10/2026):** o texto SRT ou SSA colado fica na ordem em que veio, e "Paste at cursor"
  alinha a primeira do texto ao cursor; no SSA, Marked e Layer são lidos cada um quando existem no `Format:`.
- **Diferenças:** ao desfazer uma colagem, as legendas são apagadas em ordem.

### Diferenças registradas no text pipe (M5)

Fatia 6 do M5: `MemoTextPipe`, `ActionShowHideTextPipe`, `ActionLoadTextPipe`, `ActionClearTextPipe`,
`ActionSaveTextPipeAs`, `ActionAddSubFromPipe`, `ActionReplaceFromPipe`, `ColorizeOrDeleteTextPipe`,
`TUndoablePipeTask`, `TUndoableCompositeTask`, `SearchMemo`, `ReplaceTextMemo`, `ReplaceAllTextMemo` e
o `.vssscript` do projeto.

- **Igual ao original:** o painel fica à esquerda da forma de onda (250 px, splitter de 4 px, mínimo de
  50 px), usa a fonte do editor e mostra a seleção mesmo sem foco. Carregar aceita texto, RTF, SRT e
  "SRT stripped of timings" (só os textos); como no RichEdit, um arquivo que começa por `{\rtf` é lido
  como RTF, seja qual for a extensão. "Save text pipe as" grava texto (filtro 1) ou RTF (filtro 2), e
  carregar e salvar guardam o arquivo em `TextPipeSource` e salvam o projeto. Com texto no painel, salvar
  o projeto grava o `.vssscript` (RTF) e `TextPipePosition`; abrir o projeto carrega o script e põe essa
  posição no alto do painel. Fechar o projeto não limpa o painel. "Add subtitle from pipe" cria a legenda
  com a seleção da onda e o trecho selecionado (com `Trim`) e aplica a opção marcada: colorir (vermelho
  `$003333FF`, ou azul `$00FF8000` quando o número de legendas é ímpar), apagar o trecho ou apagar tudo até
  o fim dele; tudo numa tarefa de desfazer. Os itens do menu seguem `MemoTextPipePopupMenuPopup`. As
  opções `[TextPipe] ShowTextPipe` e `TextPipeAutoOption` ficam no `.ini`. Com o painel focado, Find abre
  "Find text" (sem "Show matching sub only") e busca, troca e troca tudo no texto do painel; trocar tudo
  regrava o texto e perde as cores, como no original.
- **Diferenças:** "Replace subtitle from pipe" do 1.0.1 cria a tarefa do pipe mas nunca a executa e troca
  o texto sem desfazer; aqui a opção marcada é aplicada e a troca entra no desfazer junto com ela.
  Desfazer a coloração devolve a cor de cada pedaço (o original devolvia uma cor só). Trocar tudo sem
  nenhuma ocorrência não regrava o texto (o original perdia as cores mesmo assim). O texto simples é
  gravado em UTF-16 com BOM, que é o que o TntRichEdit faz (inferido da biblioteca, não testado no
  original). O menu tem o layout do Enhanced: Cut, Copy, Paste, Delete e Select All funcionam; "Undo" do
  próprio painel fica desabilitado (Ctrl+Z é o desfazer geral, como no original). O RTF é lido e
  gravado pelo `RichTextBox` do WPF, que pode escrever o arquivo de forma diferente do RichEdit, mas os
  dois leem o do outro.

### Diferenças registradas no karaokê (M5)

Fatia 7 do M5: `TRange.SubTime`, `AddSubTime`, `GetSubTimeRange(At)`, `UpdateSubTimeFromText`,
`KaraSplit`/`KaraSplit2`, o modo `demKaraoke` do `TWAVDisplayer`, `SelectNextKaraoke`/`SelectPreviousKaraoke`,
`pmiCreateKaraokeClick`, `pmiClearKaraokeClick`, `ActionInsertKaraokeMarker`,
`ActionPlaceKaraokeCursorsAtEnd`, `WAVDisplayer1KaraokeChanged`, `WAVDisplayer1SelectedKaraokeRange` e o
karaokê em `OffsetCurrentSubtitleStartTime`/`StopTime`.

- **Igual ao original:** os marcadores vêm dos `{\k}` do texto (ao abrir, ao editar e ao colar SSA) e são
  desenhados pontilhados entre as linhas da legenda; o que está sob o mouse fica cheio e pode ser
  arrastado (só na faixa entre 1/10 e 9/10 da altura, sem encaixe, preso entre os vizinhos). Soltar
  reescreve os `\k` e entra no desfazer. Duplo clique nessa faixa seleciona o trecho de karaokê, foca a
  legenda e seleciona só ela; com um trecho selecionado, "Select/Play next/previous sub" andam pelos
  trechos e Shift start/stop ±10/±100 movem o marcador do trecho (a 10 ms dos vizinhos). Um clique na onda
  solta o trecho. Create Karaoke divide o texto nos espaços, quebras de linha e `/` (que some) e dá a cada
  pedaço um `\k` proporcional às letras; Clear Karaoke tira os `\k`; os dois entram no desfazer.
  "Insert karaoke marker" põe um marcador no cursor sem mexer no texto e sem desfazer, como no original.
  "Place karaoke cursors at end" junta os marcadores no fim, 100 ms cada. Atrasar os dois tempos desloca
  também os marcadores (`DelaySub`); mudar só o início ou o fim mantém os marcadores e refaz os `\k`
  (`SetSubtitleTime`), como no original.
- **Diferenças:** o destaque das tags no editor (`TagHighlight`) entrou com a verificação ortográfica
  (M7); veja "Diferenças registradas na verificação ortográfica". Create Karaoke numa legenda sem letras não faz
  nada (o original dividiria por zero).

### Diferenças registradas no menu do editor e nos estilos de texto (M5)

Fatia 8 do M5: `MemoSubPopupMenu`, `TagText`, `TagTextMemo`, `TagTextLines`, `ActionTextItalic`,
`ActionTextBold`, `ActionTextUnderline`, `ActionTextColor`, `ActionTextSize` e `ActionStripTags`.

- **Igual ao original:** as tags dependem da extensão do arquivo de legendas (`.srt`: `<i>`, `<b>`, `<u>`,
  `<font color="#FFFFFF">`, `<font size="16">`; `.ass`/`.ssa`: `{\i1}{\i0}`, `{\b1}{\b0}`, `{\u1}{\u0}`,
  `{\1c&HFFFFFF&}{\1c}`, `{\fs16}{\fs}`; outras extensões: nada). Com o editor focado, a tag envolve a
  seleção (que continua selecionada com as tags), o texto todo quando o cursor está no começo, ou entra
  no cursor com ele entre as duas; a troca entra no desfazer como uma edição do editor. Fora do editor,
  envolve cada legenda selecionada numa tarefa só. "Strip tags" tira as tags das selecionadas, também
  numa tarefa só. Ctrl+I é o atalho padrão de "Text in italic". O menu de contexto do editor tem a ordem
  do Enhanced (Undo, Cut, Copy, Paste, Delete, Select All, Text in italic, Other styles).
- **Diferenças:** as sugestões do corretor no topo do menu do editor entraram no M7. Os itens de posição
  e de maiúsculas/minúsculas de "Other styles" na lista são só do Enhanced e continuam desabilitados.
- **Correção junto com esta fatia:** os itens de menu ligados a ações que pedem projeto aberto (Find,
  Goto, Delay, Merge, karaokê e outros) agora são reavaliados quando um projeto abre ou fecha; antes
  podiam ficar desabilitados depois de abrir um projeto.

### Diferenças registradas em cenas, Reload e Insert file (M5)

Fatia 9 do M5: `ActionInsertSceneChange`, `ActionDeleteSceneChange` (com `TUndoableInsertSceneChange`,
`TUndoableDeleteSceneChange` e `TSceneChangeWrapper.Insert/Delete/Contains`), `ActionReload`,
`ActionOpenProjectFolder` e `ActionInsertTextFile`.

- **Igual ao original:** "Insert scene change" (menu da onda, só sem seleção) põe uma mudança de cena no
  cursor, se ainda não houver uma ali; "Delete scene change" (só com seleção) apaga as que estão dentro
  dela. Os dois entram no desfazer e regravam o `.scenechange` do vídeo quando ele existe, sem marcar o
  projeto como alterado. Reload pergunta se salva, relê as legendas, volta à legenda focada, limpa o
  desfazer e deixa o projeto sem alterações. Open Project Folder abre a pasta do projeto no Explorer.
  Insert file aceita vários arquivos: um `.srt` entra com os próprios tempos; outro arquivo vira uma
  legenda de 1 s por linha, a cada 2 s, começando 2 s depois do fim da última legenda (ou em 99:00:00
  com a lista vazia); cada arquivo é um passo do desfazer.
- **Diferenças:** Reload relê também o VO, como no original (M7). O
  `TSceneChangeWrapper.Insert` do original lia além do fim da lista em alguns casos; aqui a junção é a
  mesma sem esse erro. Um arquivo que não abre em Insert file mostra a mensagem e os outros continuam.

### Diferenças registradas nas janelas Vertical scaling, Silent zones e Log (M4)

Fatia 3 do M4: `TVerticalScalingForm`, `TSilentZoneForm` (com `TWAVDisplayer.DetectSilentZone` e
`TMainForm.SetSelection`), `TLogForm`, `ActionZoomVertical`, `ActionShowSilentZones`, `ActionShowHideLogs`
e `LoadFormPosition`/`SaveFormPosition` dessas janelas e da janela Find.

- **Igual ao original:** View > Zoom Vertical e o botão Zoom Vertical (`bttZoomVertical`) alternam a janela "Vertical scaling" (sempre por cima,
  centrada na principal); a barra vai de 50 a 800, de 10 em 10 pelas setas e de 50 em 50 por Page Up/Down,
  com Reset em 100; ao abrir, ela mostra a escala atual da onda (presa entre 50 e 800). View > Show silent
  zones abre "Silent zones" e já atualiza a lista: janelas de 100 ms com RMS abaixo do limiar se juntam
  em zonas, só as de duração mínima aparecem, como "início -> fim (RMS médio)", e o duplo clique seleciona
  a zona na onda mostrando 20 s em volta. O cálculo repete o do original, inclusive a soma da janela
  dividida pelo número de picos menos um (a primeira janela tem um pico a mais). Os campos com setas
  andam de 10 em 10 (limiar, 0 a 32767) e de 100 em 100 (duração, 100 a 9999), com separador de milhar,
  e um texto inválido mantém o último valor válido, como o TUpDown. View > Show/Hide Logs alterna a
  janela de log, com o menu Window (Clear, Stay hidden, Close). Esc fecha as duas primeiras. Find,
  Silent zones e Log guardam posição e tamanho em `[Windows]` do `VisualSubSync.ini`
  (`FindForm_*`, `SilentZoneForm_*`, `LogForm_*`), e uma janela fora de todas as telas volta para 100,100.
- **Diferenças:** Debug > "List used DirectShow filters" fica desabilitado, porque o motor de mídia não é
  mais o DirectShow. A posição da janela de log só é gravada depois que ela foi aberta uma vez (no
  original ela existe desde o início e sempre grava). A verificação de "fora da tela" usa a área de
  todas as telas juntas, não cada monitor.

### Diferenças registradas na legenda sobre o vídeo (M3)

Fatia 5 do M3: `UpdateSubtitleForPreview`, `VideoPreviewNeedSubtitleUpdate`, `CurrentProjectOnDirtySet`,
`VideoRenderer.SetSubtitleFilename`/`HideSubtitle` e `ActionShowHideSubs`.

- **Igual ao original:** ao abrir o projeto, depois de salvar e depois de mudar o vídeo ou a legenda nas
  propriedades, o vídeo carrega o próprio arquivo de legendas. Cada mudança no projeto (inclusive
  digitação) grava, com o vídeo à mostra e tocando ou pausado, uma cópia das legendas em
  `%TEMP%\VisualSubSync\<nome do arquivo>` e a recarrega no vídeo, de modo que a edição aparece sem salvar;
  com o vídeo escondido, a cópia é refeita na próxima vez que tocar ou mostrar um quadro. View >
  Show/Hide subtitles esconde ou mostra a legenda no vídeo e, com o vídeo parado, mostra o quadro do
  início da legenda focada; abrir um projeto com vídeo volta a mostrar a legenda.
- **Diferenças:** a legenda é desenhada pelo próprio mpv (no lugar do DirectVobSub), também na janela
  de vídeo destacada.

### Diferenças registradas em Open Recent e no arquivo da linha de comando

`TMRUList` (MRUListUnit.pas), `OnRecentMenuItemClick` e `TMainForm.ProcessParams`.

- **Igual ao original:** File > Open Recent lista até 8 projetos, o último aberto primeiro (abrir de
  novo um que já está na lista o leva ao topo, sem distinguir maiúsculas), seguidos de "Clear list" e
  "Clear dead entries"; sem projetos o submenu fica desabilitado. A lista fica em `[MRUList]`
  `Recent0`..`Recent7`, com UTF-8 dentro do ini ANSI, como o `UTF8Encode` do original. Um único arquivo
  na linha de comando: `.vssprj` abre; `.srt`, `.ssa` ou `.ass` abre o `.vssprj` de mesmo nome ou cria
  um com a legenda, o `.avi`/`.mkv` e o `.peak` de mesmo nome que existirem (modo "peak only" com o
  `.peak`, sem forma de onda sem ele) e as opções de vídeo destacado e mostrar vídeo do ini. Sem esse
  arquivo, `[Misc] LoadMostRecentProjectOnStartup=1` abre o projeto mais recente.
- **Diferenças:** o `.vssprj` criado pela linha de comando é gravado em UTF-16 como os demais projetos
  desta versão. A opção LoadMostRecentProjectOnStartup é mudada na aba General das preferências (M7).

### Diferenças registradas em Export to WAV

`ActionExportToWAVExecute`, `TWAVFile.ExtractToStream` (WAVFileUnit.pas) e `TPageProcessor`
(PageProcessorUnit.pas).

- **Igual ao original:** sem WAV no projeto aparece o aviso "You need to create a project with a WAV
  file"; senão, depois de escolher a pasta, cada legenda vira `000001.wav`, `000002.wav`... com o trecho
  alinhado às amostras e o cabeçalho de 18 bytes do `WAVEFORMATEX`, e `index_wav.html` é gerado de
  `web\index_wav_export.shtml`. O processador de páginas trabalha sobre os bytes como o original: o
  modelo é copiado sem conversão, os campos terminados em 8 saem em UTF-8 e os outros em ANSI, campos
  desconhecidos ficam como estão e blocos desconhecidos geram a mesma mensagem de erro. A pasta `web` do
  original vai junto do executável. "Export to SSA" continua desabilitado porque não tem ação no 1.0.1.
- **Diferenças:** a exportação roda fora da thread da interface, com o cursor de espera, para a janela
  não congelar. Se o modelo `index_wav_export.shtml` faltar, os WAVs são gravados e a página não é
  criada, em vez do erro de arquivo do original. O campo `version` mostra a versão desta migração.

### Diferenças registradas no servidor web e na janela Suggestions (M8)

`THTTPServer`/`THTTPRequestThreadProcessor` (ServerUnit.pas), `TSuggestionForm` (SuggestionFormUnit.pas)
e `StartStopServer`.

- **Igual ao original:** Web server > Start/Stop liga e desliga um servidor HTTP/1.0 em todas as
  interfaces, na porta `[WebServer] Port` (80 por padrão), servindo a pasta `web` ao lado do executável.
  As páginas `.shtml` passam pelo mesmo processador do Export to WAV, com os parâmetros da URL, os
  cabeçalhos (`HEADER[...]`) e os campos do formulário (`POST[...]`) como variáveis; `/wav/inicio-fim.wav`
  recorta o WAV do projeto; o POST com `vss-command=SUGGESTION` decodifica o texto (URLDecode e UTF-8) e
  o coloca na janela Suggestions, com a mensagem "We just received a new suggestion from the network.
  (Total : n)". Cabeçalhos, páginas de erro (403, 404, 501), tipos MIME, a lista de índices de pasta e a
  compressão deflate (`[WebServer] EnableCompression`) seguem o original. A janela Suggestions tem o
  layout do dfm, mostra "From ... to ..." e o texto com `|` no lugar das quebras, o duplo clique
  seleciona a legenda com os mesmos tempos, "Clear selected" e "Clear all" poupam as sugestões dos
  últimos 2 s, Esc fecha e a posição fica em `[Windows] SuggestionForm_*`. Fechar o projeto limpa a lista
  e fechar o programa para o servidor.
- **Diferenças:** em vez do `g_WebRWSynchro`, cada pedido tira uma cópia das legendas na thread da
  interface. O corpo do POST é lido até o Content-Length (o original fazia um único `recv`). O cabeçalho
  Date usa a hora UTC (o original escrevia a hora local com "GMT"). O caminho pedido é resolvido com
  `Path.GetFullPath` antes da verificação de que fica dentro da pasta `web`. Se a porta não puder ser
  aberta, o motivo vai para o Log (o original não dizia nada). A porta e a compressão ficam na aba
  General das preferências; como no original, a nova porta só vale no próximo Start.

### Diferenças registradas nos plugins JavaScript e no Error report (M6)

`JavaScriptPluginUnit.pas` (TJavaScriptPlugin, TVSSCoreWrapper, TSceneChangeWrapper), `TErrorReportForm`
(ErrorReportFormUnit.pas), `ActionCheckErrors`, `ActionFixErrorMain`, `FixErrorInList` e `CanFixError`.

- **Igual ao original:** os plugins de `Release\jsplugin` são copiados sem alteração para a pasta
  `jsplugin` ao lado do executável e carregados em ordem de nome, com `LoadScript` relativo ao arquivo,
  os objetos `VSSCore`, `SceneChange` e `ScriptLog`, os parâmetros (`Param*`, com unidade e descrição) e
  as preferências em `VisualSubSync.ini` (uma seção com o nome do plugin, `Enabled` e um valor por
  parâmetro; texto em UTF-8; `[Misc] MaximumSubtitleDuration`). Tempos vindos do JavaScript são
  arredondados como no SpiderMonkey (`floor(x + 0,5)`). Erros de script vão para o Log no formato
  "Nome: mensagem in arquivo at line n" e interrompem só aquele plugin. Check errors limpa o Log, roda
  os plugins ativados em todas as legendas, mostra a janela quando acha algo e escreve "n error(s) found"
  ou "No error found" com "(in n ms)." na barra da janela. A janela Error report tem o menu File (Check
  errors, Clear all, Export to HTML..., Close), três linhas por erro com a bolinha da cor do plugin, e
  o menu de contexto com Fix selected errors, Fix all errors of same type (color) / with same error
  msg, Select all errors of same type (color) / with same error msg, Clear e Clear all, ativados pelas
  mesmas regras. Focar ou dar duplo clique num erro seleciona a legenda; Next error anda na lista; Esc
  fecha; a posição fica em `[Windows] ErrorReportForm_*`. Corrigir gera uma tarefa de desfazer com os
  tempos e textos antigos e marca o projeto como alterado, sem reordenar a lista (como o original);
  desfazer e refazer reordenam. "Fix errors" no menu da lista roda todos os plugins ativados nas
  legendas selecionadas. Apagar uma legenda apaga os erros dela; fechar o projeto ou trocar o arquivo
  de legendas limpa a lista. O HTML exportado segue o modelo escrito em ErrorReportFormUnit.pas (mensagens e
  texto em UTF-8, o resto em ANSI); ainda não foi comparado com um arquivo gerado pelo original.
- **Diferenças:** o motor JavaScript é o Jint (.NET puro) no lugar do SpiderMonkey; as mensagens de erro
  de sintaxe vêm do Jint e podem ter outro texto (o formato da linha é o mesmo). Cada chamada usa um
  objeto novo para a legenda, em vez do conjunto fixo de 10 objetos do original; os 7 plugins de erro do
  original não dependem disso (verificado nos testes). Recarregar as legendas (Reload) também limpa a lista de erros, para não
  ficarem erros de legendas que não existem mais (o original deixava). "Error preferences" abre as
  preferências na aba Error checking. Os itens do menu de contexto que só existem no Enhanced
  (Clear && Forget, Clear all errors with different/same error msg) aparecem desativados.
- **Plugin geral, igual ao original:** `jsplugin\general\general_plugin.js` é carregado na abertura,
  antes dos atalhos. `SetStatusBarText` aparece na barra de status junto das mensagens temporárias
  ("mensagem (texto do plugin)"); OnSelectedSubtitle roda quando a legenda focada muda e
  OnSubtitleModification sempre que o projeto é marcado como alterado; o duplo clique no início ou no
  fim de uma legenda na onda chama OnRangeStartDblClick/OnRangeStopDblClick, e o que o script mudar vai
  para o desfazer. As colunas extras (a coluna RS do original) entram depois das colunas do núcleo, com
  título, largura mínima, texto e cor de fundo do script (a cor não se mistura com a seleção e o texto
  fica na cor padrão). Elas só aparecem quando estão em `[General] VisibleExtraColumns`; o menu do
  cabeçalho da lista mostra ou esconde qualquer coluna e grava a escolha em `VisibleExtraColumns` e
  `HiddenByUserCoreColumns`. As ações registradas com `VSSCore.RegisterJavascriptAction` (Quick stats,
  Ctrl+M, e Split at cursor) aparecem no menu JS Tools, entram na tabela de atalhos (com o atalho do
  script no modo Normal, que o `[Hotkeys]` do ini pode trocar) e podem ser desfeitas.
- **Plugin geral, diferenças:** a coluna VO entrou no M7 e aparece no menu do cabeçalho, como no
  original; a largura dela volta a ser gravada em `ColumnsWidth`. A posição das colunas
  (`ColumnsPosition`) ainda não é gravada. Um segundo registro de ação com o mesmo nome é ignorado (na
  VCL daria erro de nome duplicado).

### Diferenças registradas na janela Preferences (M7)

- **Comportamento portado:** `TPreferencesForm` com o layout do Enhanced (`TPREFERENCESFORM.txt`) e o
  `LoadConfig`/`SaveConfig` do 1.0.1. Funcionam as opções que existem no 1.0.1: servidor web, associação
  de `.vssprj`, `.srt`, `.ssa` e `.ass`, troca da lista, projeto mais recente, backup (cópia `.bak`,
  salvar a cada N minutos, salvar ao tocar, abrir as pastas de backup), as durações e o CPS da aba
  Subtitle, os plugins de erro (marcar, Select all/Unselect all, descrição, cor e edição dos parâmetros
  com duplo clique ou F2), a aba Hotkeys (modo Normal/Timing, Set tira o atalho da primeira ação que já o
  usa, Clear, Reset all), a roda do mouse, SSA mouse mode, encaixe e anti-sobreposição, as duas fontes
  (diálogo de fonte do Windows), as três opções do modo Timing e a aba WAV display (mudança de cena,
  offsets e texto na onda). OK aplica na hora como `ShowPreferences` (troca da lista, editor no modo
  Timing, atalhos dos menus, roda, backup, fontes, mudança de cena, compressão do servidor); Esc ou Cancel
  descartam. Sem aba pedida, a janela reabre na última aba vista, na posição gravada em
  `[Windows] PreferencesForm_*`. Ao sair, todas as preferências vão para o ini como no `SaveIni`, e os
  atalhos passam a ser gravados em número (`UseIntegerShortcut=1`).
- **Correção de fidelidade:** a roda do mouse agora segue `[Mouse] Wheel*Modifier` com os padrões do
  `TConfigObject` (roda sozinha = zoom horizontal, Ctrl = rolar, Shift = zoom vertical). Antes esta versão
  usava os padrões do componente (roda = rolar, Ctrl = zoom).
- **Diferenças:** as abas e opções que só existem no Enhanced (Playback, Desynch Tools, Dictionary, vídeo,
  cores da onda, caminho padrão, UTF-8 padrão, atualizações, painéis quick/smart etc.) aparecem
  desabilitadas. Desfazer a associação do `.srt` apaga a chave `VisualSubSync.Document.srt`; o original
  apagava por engano `VisualSubSync.Document`, que é a do `.vssprj`. O ícone registrado é o índice 0 do
  executável, que só tem um ícone (o original usava os índices 1 e 2). O campo de atalho imita o
  controle do Windows ("Ctrl + Shift + A", Backspace limpa). Um valor de parâmetro que não converte
  (número com letra, por exemplo) mostra o erro do Delphi e deixa a edição aberta, em vez de deixar o
  campo num estado inconsistente. A lista de atalhos e a de plugins voltam para as preferências por
  nome; o original copiava por posição entre listas ordenadas de formas diferentes, o que só mudava a
  ordem interna. A família da fonte da janela Suggestions passou a acompanhar a da lista na
  revisão de 21/10/2026, como `ApplyFontSettings` do original; tamanho e estilo não foram alterados.

### Diferenças registradas nos presets (M7)

- **Comportamento portado:** File > Load presets lista os `*.ini` da pasta `presets` ao lado do
  executável, em ordem alfabética sem distinguir maiúsculas, seguidos de um separador e "Browse...". Um
  preset é lido por cima das preferências atuais como `ConfigObject.LoadIni(IniFile, True)`: só as chaves
  presentes mudam, e servidor web, backup e fontes ficam como estão. Depois vêm `SetShortcut`, a roda do
  mouse, as fontes e as opções da onda. O item do arquivo carregado fica marcado. Escolher um item grava
  o nome em `Presets` do projeto e marca o projeto como modificado; "Browse..." só carrega. Ao abrir um
  projeto com `Presets` preenchido, o arquivo é carregado depois da seleção da legenda focada.
- **Diferenças:** sem projeto aberto, escolher um preset só carrega o arquivo; o original acessava o
  projeto inexistente. A lista é montada uma vez, na abertura da janela, como no original.

### Diferenças registradas na janela Styles (M7)

- **Comportamento portado:** `TStyleForm` com o layout de `TSTYLEFORM.txt` e a lógica de
  `StyleFormUnit.pas`. A janela abre pelo menu Edit > Styles, pelo botão Styles do painel e pelo
  atalho, só com `.ass` ou `.ssa` carregado, e edita os estilos do documento no lugar, sem cancelar,
  como o original. New cria "New Style" com os padrões de `TSSAStyle.Create`, Copy acrescenta " Copy",
  Delete deixa ao menos um "Default". Apply recusa vírgula no nome e nome repetido (sem distinguir
  maiúsculas), troca o nome nas legendas que usavam o antigo e marca o projeto. Apply e Reset só ficam
  habilitados quando algum campo difere do estilo (`CheckChange`), e Reset volta aos valores do estilo.
  O tamanho da fonte é arredondado como no original, então um tamanho fracionário já habilita Apply.
  Os botões H/S/L abrem a barra à direita do botão: mover o mouse muda matiz, saturação ou
  luminosidade das quatro cores ao mesmo tempo, e o clique fecha a barra. Scale X/Y, Spacing, Angle e
  os efeitos do diálogo de fonte só ficam habilitados no ASS. O combo ao lado do botão mostra o estilo
  das legendas selecionadas (vazio quando são diferentes) e, ao escolher um, troca o estilo de todas
  elas, sem desfazer, como no original. A janela lembra o estilo selecionado até outro arquivo de
  legendas ser carregado.
- **Diferenças:** os painéis de cor mostram só o RGB; no original, uma cor com alfa acima de 127
  aparecia preta, porque a VCL a lia como cor do sistema. O valor do alfa continua guardado e só some
  quando a cor é escolhida no diálogo, como no original. O combo é atualizado a cada mudança de
  seleção na lista; o original atualizava quando a legenda focada mudava. Um `.srt` salvo como `.ass`
  usa os estilos do próprio documento (um "Default"); no original vinham os estilos do último `.ass`
  aberto na sessão.

### Diferenças registradas na verificação ortográfica (M7)

- **Comportamento portado:** `LibHunspellUnit.pas`, `SpellCheckFormUnit.pas` e as partes de `main.pas`
  (menu Edit > Spell check, `LoadDict`, `TagHighlight`, `MemoSubPopupMenuPopup`). Os dicionários são os
  `*.dic` da pasta `dict` ao lado do executável que têm o `.aff` de mesmo nome; cada um vira um item de
  rádio no menu, ou "No dictionnary" desabilitado quando não há nenhum. O dicionário carrega em segundo
  plano, com os itens desabilitados enquanto isso; ao terminar vira o padrão (`[General] Dictionnary`
  no ini) e é gravado no projeto aberto, que é salvo. Abrir um projeto carrega o dicionário dele; sem
  projeto, o do ini. O dicionário pessoal `dict\perso.dic` é lido ao abrir e gravado ao fechar em UTF-16
  com BOM; "Add" acrescenta nele e "Ignore all"/"=> Ignore" valem até fechar. A janela Spell check
  percorre as legendas a partir da primeira, mostra a palavra recusada em vermelho, as sugestões (a
  primeira já em "Replace by") e foca a legenda na lista; Replace all troca as próximas ocorrências sem
  parar. Palavras com dígito ou todas em maiúscula são aceitas. As trocas da janela entram no desfazer
  como uma tarefa só. Com "Live spell check" marcado (padrão, não gravado), o editor sublinha em
  vermelho ondulado as palavras recusadas, mostra as tags em cinza e a tag do trecho de karaokê
  selecionado em vermelho; o clique direito numa palavra recusada põe as sugestões (ou "(No
  suggestions)"), "=> Add to dictionary" e "=> Ignore" acima de Undo, e uma sugestão troca a palavra
  mantendo a posição do cursor.
- **Diferenças:** o Hunspell é o WeCantSpell.Hunspell (C#), que lê os mesmos `.aff`/`.dic` e trabalha
  em Unicode, sem a conversão pela página de código do dicionário; as sugestões podem vir numa ordem
  um pouco diferente da biblioteca nativa. A lista de dicionários sai em ordem alfabética (o
  `FindFirst` do NTFS também costuma dar essa ordem). Com várias legendas selecionadas, a verificação
  fica só nelas; no original, focar a primeira legenda com erro desfazia a seleção e a verificação
  seguia por todas. O editor continua um TextBox e as cores são desenhadas por cima dele; o texto comum
  sai em clWindowText, como no original, que repintava o editor a cada mudança e assim ignorava a cor
  da fonte das preferências. A cor do sublinhado (índice 5 do RichEdit) não foi conferida na tela;
  usamos vermelho. A tag de karaokê só fica vermelha quando o trecho selecionado na onda é da legenda
  em edição; no original o índice ficava valendo também ao digitar em outra legenda. "Go to First" e o
  submenu "Custom dictionary" são só do Enhanced e ficam desabilitados.

### Diferenças registradas no VO e na tradução (M7)

- **Comportamento portado:** `LoadVO`/`LoadVOSubs`, `ActionToggleVO`, `TurnOnVO`/`TurnOffVO`,
  `GetVOText`, `UpdateMemoVO` e `ActionTranslationTemplate` de `main.pas`, `TTranslateForm` e as faixas
  de `TWAVDisplayer` (`FDisplayRangeLists`, `ShowVO`). O VO (SRT, SSA ou ASS) carrega ao abrir o projeto,
  ao trocar o arquivo no formulário do projeto e no Reload. View > Show/Hide Reference VO mostra ou
  esconde e grava `ShowVO` no projeto; sem VO, tenta o arquivo do projeto e depois pergunta qual usar.
  Com o VO à mostra, a onda fica em duas faixas: a de cima com o VO em cinza (sem edição, sem seleção
  dinâmica, sem espaço mínimo; o encaixe e o bloqueio de sobreposição usam a lista da faixa sob o
  mouse) e a de baixo com as legendas e a onda em verde escuro (`PaintAlternateWavColor`); o traço de
  cima só aparece no VO e o de baixo só nas legendas, e o texto da faixa de baixo fica alinhado embaixo.
  A coluna VO mostra o VO da legenda com "[<<<]" e "[>>>]", e o MemoSubtitleVO, à direita do editor, mostra
  o anterior e o seguinte em cinza e o atual quebrado em até 4 linhas; a largura dele vai para
  `[Windows] MemoSubtitleVO_Width`. File > Translate grava "nome.translated.ext" com o texto escolhido
  (vazio, original, só as tags ou um texto fixo), passa a usá-lo como legenda do projeto e põe a legenda
  antiga como VO; "Missing only" (só com VO) cria, com desfazer, as legendas que faltam onde o VO não tem
  legenda no meio.
- **Diferenças:** no MemoSubtitleVO o original contava cada quebra de linha como dois caracteres ao
  pintar, então o começo da parte atual podia sair em cinza; aqui as cores caem no lugar certo. O
  splitter do VO é uma barra de 4 px que redimensiona o memo, como o `TSplitter` com `MinSize` 30.

### Diferenças registradas no menu de ajuda e na janela About

- **Comportamento portado:** `MenuItemHelpIndexClick`, `MenuItemHelpIndexWAVDisplayControlClick` e
  `MenuItemAboutClick` de `main.pas` e `TAboutForm`. ? > Help > Index e WAV Display Control abrem
  `help\index.html` e `help\wavdisplaycontrol.html` no navegador padrão; a pasta `help` é a
  `Release\help` do original, sem alteração, e vai junto do executável. A janela About mostra a versão,
  abre o site ao clicar no título e um e-mail com a versão no assunto ao clicar no endereço; Esc fecha.
- **Diferenças:** a janela About usa o layout do 1.0.1, porque o do Enhanced só acrescenta os créditos
  dos autores e dos componentes do Enhanced. A lista de componentes agradecidos é a dos que este
  programa usa (mpv, FFmpeg, Jint, Hunspell pelo WeCantSpell.Hunspell e CommunityToolkit.Mvvm), no lugar
  de Virtual Treeview, Tnt, SpiderMonkey e VSFilter, que não fazem parte dele; as dicas mostram as
  versões do Jint e do WeCantSpell.Hunspell. O endereço aparece como `christophe.paris@free.fr`, igual ao
  Enhanced e ao `mailto` do original (o rótulo do 1.0.1 dizia `free.com`). O painel invisível de teste de
  falha (`pnlCrashTest`) não foi portado. "Codecs installation" não está no menu do Enhanced e ficou de
  fora; "Recent changes" e "Google+ page" são do Enhanced e continuam desabilitados. "Check for updates"
  era do Enhanced e ficou desabilitado; foi portado em 23/10/2026 (ver a seção seguinte).

### Diferenças registradas no "Check for updates" (23/10/2026)

- **Comportamento portado:** `CheckUpdates`, `MenuItemCheckUpdatesClick` e `TimerChkUpdatesTimer` de
  `main.pas`, e `CheckUpdates`/`ChkUpdates` do `PreferencesFormUnit.pas`. O menu ? > Check for updates
  procura a última versão no repositório e, se houver uma mais nova, mostra a nota da versão e pergunta
  se quer baixá-la, abrindo o endereço do instalador no navegador; sem versão nova, avisa "No new updates
  are available." ao ser chamado pelo menu (a procura automática fica em silêncio). A procura automática
  ao iniciar tem 500 ms de espera, como o `TimerChkUpdates.Interval` do Enhanced, e só roda com
  "Check for updates on start" ligado nas Preferences (`[Misc] CheckUpdates` no `VisualSubSync.ini`,
  gravado como antes).
- **Diferenças:** a consulta usa a API pública de Releases do GitHub
  (`https://api.github.com/repos/Hayllander/VisualSubSyncNET/releases`) e lê o primeiro lançamento devolvido,
  com o campo de download do primeiro anexo `-Setup.exe`. O endereço é fixo no programa; o original lia o
  host de um `CheckUpdates` próprio do site do Enhanced, que não existe para este projeto. A comparação
  de versões normaliza a etiqueta do lançamento (tira o `v` inicial e separa os sufixos de pré-lançamento,
  como `-alpha.N`), porque as tags deste repositório levam esses prefixos; uma versão final (sem sufixo)
  ordena depois das pré-lançamentos, como esperado. Uma falha de rede, de JSON ou de leitura só vai para
  o Log, sem incomodar quem está editando.

### Diferenças registradas no instalador (M9)

- **Portado:** `installer\VisualSubSync.nsi` segue o `VisualSubSync.nsi` do 1.0.1 com o NSIS 3 e o mesmo
  Modern UI: páginas de componentes, pasta e instalação; seção "Main (required)" e "Start Menu
  Shortcuts" (VisualSubSync, Help e Uninstall); pasta `presets` vazia; a pasta escolhida em
  `HKCU\Software\VisualSubSync`; "VisualSubSync (remove only)" em Adicionar ou remover programas; o
  desinstalador apaga só os arquivos instalados e as pastas que ficarem vazias, então dicionários e
  presets do usuário ficam. `installer\build-installer.ps1` publica o programa autocontido, gera
  `installer\files.nsh` (um `File` e um `Delete` por arquivo publicado, sem os `.pdb`) e chama o
  `makensis`; o instalador sai em `installer\Redist\VisualSubSync-<versão>-Setup.exe`.
- **Diferenças:** a lista de arquivos é gerada porque a publicação do .NET tem centenas de arquivos
  (runtime, WPF, mpv, FFmpeg) no lugar dos seis do original; vão junto as pastas `web`, `help`,
  `jsplugin` e `dict` (com o `_about_dictionaries_.txt` do original), como antes. A pasta padrão é
  `Program Files` e não `Program Files (x86)`, e a chave de desinstalação vai para a visão de 64 bits do
  registro, porque o programa é de 64 bits. O instalador pede administrador e os atalhos vão para o menu
  Iniciar de todos os usuários, porque instalar em `Program Files` exige administrador e, com o UAC,
  os atalhos do usuário corrente iriam para o perfil do administrador. O script é Unicode e usa
  compressão LZMA sólida. Falta instalar e rodar em Windows 10 e 11 limpos.

### Comparação com o executável do Enhanced (18/10/2026)

Medição feita com o executável de referência (`C:\Projetos\VisualSubSyncEnh\VisualSubSyncEnh.exe`) e com
o build Release atual, ambos a 96 dpi. Artigos em `docs\medicoes`:

- `comparativo-enhanced-vs-novo.png` — captura das duas janelas, Enhanced em cima, novo app embaixo.
- `uia-enhanced.txt` — árvore de acessibilidade do Enhanced (geometria real da VCL, 37 elementos).
- `uia-novo-app.txt` — árvore do novo app (146 elementos).

Resultado:

1. **Painel de controle (88 px):** todas as posições e tamanhos do DFM conferem, no Enhanced e no novo
   app, à exceção da origem (o Enhanced começa em `112,539`; o novo, em `428,436`, por causa do menu mais
   baixo e da aba). Botões Play/Stop/Pause/Loop (4/28/52/76, 0), PlayToEnd, Volume (-4,61), Auto-scroll
   (104,0), painel de tempo (197,43), `PanelTimeSelView` (352,0,260x85) com os seis campos de 73x21,
   Check errors/Suggestions/Preferences/Project Properties (623/647/671/695, 10), Styles (616,52),
   ComboBox (658,54) e PageQuickSmartSilentZonePanels (749,0,273x87): **idênticos aos valores do DFM**.
2. **Menus:** os 133 rótulos do `MENU.txt` do Enhanced aparecem todos, na mesma ordem, sem omissão nem
   divergência de texto (comparação automática `Compare-Object`). As 9 strings extras do novo app são a
   aba "Subtitle", as três abas dos painéis novos e os cinco cabeçalhos da lista.
3. **`MainForm_PanelBottom_Height`:** o padrão estava 65 (altura do `main.dfm` 1.0.1); o DFM do Enhanced
   usa 104. Corrigido em `VssSettings` (inicializador e leitura). Sem `VisualSubSync.ini`, a linha do
   editor agora sai com 104 px, como no Enhanced.
4. **Altura da área de conteúdo (diferença pequena, mantida):** com a mesma janela de 1083x755, a lista
   fica com 234 px em vez de 224. São 11 px vindos das métricas do framework: menu do WPF 17 px contra
   20 px do `TntMainMenu` (3), cabeçalho da aba 20 px contra 24 px do `TPageControl` (4) e o recuo de
   4 px que o `TPageControl` deixa em volta do `TTabSheet` (4). Compensar isso exige fixar a altura do
   menu e do cabeçalho da aba e recuar o conteúdo da aba; não foi feito para não artificializar o layout.
5. **Estado inicial da janela:** o `VisualSubSync.ini` do Enhanced tem `MainForm_State=2` (maximizada),
   `PanelTop_Height=360`, `PanelBottom_Height=146` e `SwapSubtitlesList=1`; por isso o executável de
   referência abre com o editor em cima e a lista embaixo. O novo app lê todas essas chaves: copiando o
   ini para a pasta do executável, a janela reproduz o mesmo arranjo.
6. **Cor de fundo:** as duas janelas usam o cinza claro do sistema (240,240,240) e o branco da lista.
   A primeira captura do novo app saiu escura apenas porque foi feita antes de o WPF terminar de pintar.

## Marcos

| Marco | Entrega | Critério de pronto |
| --- | --- | --- |
| M0 | Esqueleto da solução, formatos de projeto, picos e mudanças de cena, forma de onda básica | **Concluído**: compila, 15 testes passam |
| M1 | Núcleo de legendas: SRT completo, SSA/ASS, CUE, TXT, CSV, codificações | **Concluído**: leitores e gravadores portados de `SRTParserUnit`, `SSAParserUnit` e `SaveSubtitlesAs*`; 49 testes passam; `samples\UnicodeSample.srt` regrava como o original; abrir, salvar e salvar como ligados à janela |
| M2 | Prova de conceito de mídia e escolha do motor | **Concluído**: libmpv escolhido; reprodução, quadros-chave (FFmpeg) e picos medidos no Linux e no Windows com vídeos reais; avaliação do player (vídeo na janela, cursor) fechada nas fatias 2/3 do M3 |
| M3 | Reprodução, extração de áudio e geração de picos, janela de vídeo | **Concluído no código**: fatias 2 a 6 prontas (motor ligado à janela, reprodução, cursor de 40 ms, painel de vídeo por HwndHost, frames de cursor/início/fim; extração de WAV/picos pelo FFmpeg com o diálogo do original; novo projeto e propriedades do projeto ligados aos menus e ao botão; legenda atualizada sobre o vídeo e Show/Hide subtitles; janela de vídeo destacada). Falta verificar em execução com `libmpv-2.dll` presente |
| M4 | Forma de onda completa e sincronização (modos normal e timing, atalhos, mouse) | **Concluído no código**: fatias 1 a 3 prontas (mouse do `TWAVDisplayer`, edição de tempos pela forma de onda, `UpdatePlayRange`, mudanças de cena; modo Normal/Timing, tabela `[Hotkeys]` e ações de sincronização; janelas Vertical scaling, Silent zones e Log; 130 testes passam). As 113 ações do `main.dfm` do 1.0.1 estão ligadas no código (conferido em 20/10/2026; Export to SSA não tem código no original). Falta conferir na tela. Critério: paridade com as ações de `main.pas` listadas no inventário |
| M5 | Edição: undo/redo, mesclar, dividir, atraso, buscar, área de transferência, text pipe, karaokê | **Concluído no código**: fatias 1 a 9 prontas (desfazer/refazer; apagar, mesclar e dividir, com os menus de contexto da lista e da forma de onda; janela de atraso; busca, troca e Go to; área de transferência; text pipe; karaokê; menu do editor e estilos de texto; inserir/apagar mudança de cena, Reload, Open Project Folder e Insert file; 127 testes passam). Falta conferir o Tutorial 2 na tela. Critério: Tutorial 2 da ajuda reproduzível |
| M6 | Plugins JavaScript e relatório de erros | **Concluído no código**: partes 1 a 3 prontas no código (motor Jint com os plugins de `Release\jsplugin` sem alteração, preferências dos plugins no ini, janela Error report, Check errors, correções com desfazer, Next error e exportação para HTML; plugin geral com a coluna RS, o texto na barra de status, o duplo clique nas bordas e o menu JS Tools; 175 testes passam). Falta conferir na tela. Critério: plugins de `Release\jsplugin` rodam sem alteração |
| M7 | Ortografia, estilos, tradução, presets, preferências e `VisualSubSync.ini` | **Concluído no código** (parte 1: janela Preferences com as opções do 1.0.1, gravação completa do ini, roda do mouse, backup automático; parte 2: presets; parte 3: janela Styles; parte 4: verificação ortográfica; parte 5: VO e tradução; 207 testes passam). Critério: preferências do original carregam |
| M8 | Servidor web de sugestões | **Concluído no código**: servidor HTTP, páginas de `Release\web`, WAV virtual, POST de sugestões e janela Suggestions; 153 testes passam. Falta abrir as páginas num navegador com o programa rodando. Critério: páginas de `Release\web` funcionam |
| M9 | Publicação autocontida e instalador | **Pronto no código**: `installer\build-installer.ps1` e `installer\VisualSubSync.nsi`, no molde do instalador original; o instalador foi gerado com o NSIS 3. Falta instalar e rodar em Windows 10 e 11 limpos. Critério: instala e roda em Windows 10 e 11 limpos |

## Verificação de compatibilidade

- Coletar arquivos reais gerados pelo VisualSubSync 1.0.1 (projetos, `.peak`, `.scenechange`,
  legendas em várias codificações, `VisualSubSync.ini`) em `tests\Fixtures`.
- Para cada formato: abrir, salvar sem alterações e comparar byte a byte; depois abrir o arquivo
  salvo no original.
- Para picos: gerar a partir do mesmo WAV nas duas versões e comparar.
- Para a interface: comparação visual lado a lado com o original em execução.

### Comportamentos do legado mantidos nas legendas (M1)

- SRT: texto antes do primeiro tempo é ignorado; linhas são aparadas (inclusive espaço no fim);
  o índice da legenda seguinte é removido; fim igual ao início da próxima vira fim - 1 ms e o
  projeto fica marcado como alterado.
- SSA/ASS: tempos em centésimos (os milissegundos são truncados ao salvar); linhas `Comment:` e
  chaves desconhecidas nas seções de estilos e eventos são descartadas; seções extras (`[Fonts]`,
  `[Graphics]`) e linhas antes de `[Script Info]` vão para o rodapé e são regravadas.
- Codificação: com BOM = Unicode, sem BOM = ANSI do sistema; ao salvar, UTF-8 com BOM ou ANSI.
  Se o texto não couber em ANSI, passa a UTF-8 com o mesmo aviso do original. CUE é sempre ANSI.
- Única diferença deliberada: arquivo sem BOM com UTF-8 válido e caracteres não ASCII é lido como
  UTF-8 (o original mostraria caracteres trocados) e passa a ser salvo com BOM.
- Cópia de segurança: `<nome>.bak` na pasta `Backup` ao lado do executável, antes de sobrescrever;
  se essa pasta não for gravável, `%APPDATA%\VisualSubSync\Backup` (revisão de 20/10/2026).

## Pontos em aberto

| Ponto | Padrão adotado até decisão |
| --- | --- |
| Idioma da interface | Textos em inglês como no original, preparados para tradução em `.resx` (pt-BR depois) |
| Editor de texto | AvalonEdit, se a verificação ortográfica ao digitar couber nele |
| Local do `VisualSubSync.ini` | Ao lado do executável quando gravável; senão `%APPDATA%\VisualSubSync.ini`, com verificação efetiva de escrita |
| Pasta `VisualSubSync\modern` existente | Não alterada; o importador de formulário e o inventário de menus podem ser trazidos para cá |

## Revisão aplicada — 20/10/2026

Escopo: corrigir os pontos encontrados na análise geral dos três diretórios, sem redesenhar a
interface. Fontes Delphi e executável Enhanced não foram alterados. O manifesto do Enhanced
confirma um aplicativo desktop x86 empacotado (`Windows.FullTrustApplication`, `runFullTrust`),
e não uma reescrita nativa UWP.

### Persistência e proteção dos arquivos (M1, M5, M7, M9)

- `VssSettings.ResolvePath` verifica a abertura para escrita do INI existente e a criação de um
  temporário na pasta. O atributo ReadOnly sozinho não identifica as ACLs de Program Files.
  `LoadForExecutable`, usado no início do aplicativo, preserva as configurações do INI local
  protegido na primeira carga quando ainda não existe INI no destino do usuário. Um INI já
  existente em `%APPDATA%` tem precedência nesse caso. O INI local permanece intacto.
- Backup manual e automático preferem a pasta portátil `Backup`; quando indisponível, usam
  `%APPDATA%\VisualSubSync\Backup`. Falhas finais de backup passam a abrir o log do programa;
  falha ao gravar preferências ao fechar também é informada ao usuário.
- Legendas, projetos e configurações gravados por `TextFileWriter`/`IniDocument` passam por um
  temporário no mesmo diretório, descarregado no disco antes da substituição do destino.
  As codificações, BOMs e conteúdo gerados pelos formatos não foram alterados.
- `ProjectProperties` respeita Cancel e a falha da gravação pedida pelo usuário antes de
  substituir a legenda. Se a conversão de extensão não conseguir gravar o novo arquivo, mantém
  o caminho original e não recarrega um documento vazio. É uma correção deliberada de um defeito
  herdado do Delphi: o original também ignorava o retorno de `CheckSubtitlesAreSaved` nesse ponto.

### Comandos de quadro (M3)

- `ShowStartFrame` e `ShowStopFrame` enviam StartMs/StopMs diretamente a `SeekAsync` do mpv.
  Foi removido o arredondamento por FPS constante e o acréscimo de um quadro que compensava
  o DirectShow/VSFilter. O motor novo resolve os timestamps do vídeo, inclusive VFR.
- A suíte da aplicação verifica os tempos enviados, com FPS 0, 25 e 29,97. Ela usa um motor
  substituto e não comprova qual imagem o mpv efetivamente apresenta nem a sincronia auditiva.
  A validação visual CFR/VFR e a escuta permanecem pendentes; as medições antigas não foram refeitas.

### Servidor de sugestões (M8)

- Preservados o acesso pela rede, as páginas e o protocolo existente. O servidor continua em
  todas as interfaces quando iniciado pelo usuário; não foi acrescentada autenticação.
- Limites: 16 conexões simultâneas, 8 KiB por linha, 32 KiB de cabeçalhos e 1 MiB de corpo POST.
  Comprimento negativo ou inválido é rejeitado; corpo grande é recusado antes da alocação.
  Há prazo de leitura do pedido e timeouts de leitura/escrita (padrão de 10 segundos).
- `Stop` fecha também os clientes ativos, evitando deixar leituras incompletas penduradas.
  Os testes cobrem pedidos normais, recusa de excessos, inatividade e encerramento com pedido parcial.

### Verificação realizada

- `dotnet build VisualSubSync.slnx`: passou, zero avisos e zero erros.
- `dotnet test --project tests/VisualSubSync.Core.Tests --no-build --no-restore`:
  **215 aprovados, zero falhas, zero ignorados** (207 anteriores + 8 novos).
- `dotnet test --project tests/VisualSubSync.App.Tests --no-build --no-restore`:
  **6 aprovados, zero falhas, zero ignorados**. Projeto novo, incluído na solução, com testes
  de cancelamento, falha ao salvar/converter e timestamps dos comandos de quadro.
- Total atual: **221 casos aprovados**. Contagens dos marcos acima são registros históricos.
- O README foi alinhado ao estado atual. O pacote e o instalador anteriores não passam a
  incorporar essas alterações automaticamente: precisam ser publicados/gerados novamente.

### Pendências mantidas

1. Conferir os tutoriais e o fluxo de legendagem em uso real: foco, atalhos, mouse, karaokê,
   edição com vídeo, loop e sincronia entre cursor e som.
2. Validar as ações de quadro e vídeo destacado com CFR/VFR, HEVC, DPI e vários monitores.
3. Instalar como usuário comum em Windows 10 e 11 limpos e confirmar INI/backup no perfil.
   Os testes automatizados desta revisão não substituem o teste de instalação sob ACLs reais.
4. Ampliar arquivos de referência produzidos pelo original; os testes atuais não constituem
   prova exaustiva de compatibilidade de todos os formatos, codificações e plugins de terceiros.
5. Antes da distribuição, revisar avisos/licenças das dependências e regenerar pacote/instalador.

## Auditoria de paridade e fonte Enhanced — 21/10/2026

Versão de trabalho: **2.0.0-alpha.3**. Relatório detalhado, referências de origem, testes por achado
e limites da revisão em [AUDITORIA-PARIDADE-2026-10-21.md](AUDITORIA-PARIDADE-2026-10-21.md).

### Bases de comparação

- Original 2013: permanece a especificação principal de comportamento e não foi alterado.
- Enhanced executável: **1.2.29.0**, usado como referência visual; permanece intacto.
- Novo material fornecido pelo usuário: fontes **1.2.33.0**, confirmados por `VisualSubSyncEnh.dof`,
  em `C:\Projetos\VisualSubSyncEnh\fontes`. As afirmações anteriores de ausência de fonte estão
  superadas para essa versão; não se pressupõe que ela seja idêntica ao executável 1.2.29.
- .NET: correções preservam a interface existente. Recursos adicionais do Enhanced são
  inventariados separadamente e não declarados prontos apenas porque agora existe fonte.

### Verificação reproduzível e resultados

- Novo `tools/audit-parity.py`: gera `docs/RASTREABILIDADE-PARIDADE-2026-10-21.json` com entradas
  das rotinas Delphi, linhas, hashes normalizados, candidatos textuais C# e diferenças Enhanced.
  São **1.530 entradas originais**; os candidatos não comprovam equivalência semântica.
- Comparação mecânica entre unidades comuns: 1.371 entradas iguais, 154 alteradas, 270 novas do
  Enhanced e 5 exclusivas do original; 11 unidades adicionais no Enhanced. Strings e diretivas de
  compilação são preservadas na normalização. Overloads são pareados por nome/ocorrência.
- Plugins/web/ajuda originais: **12/8/42 arquivos idênticos byte a byte** ao conteúdo na .NET.
- Build da solução: **zero avisos e zero erros**.
- Core.Tests: **220 aprovados**, zero falhas/ignorados.
- App.Tests: **17 aprovados**, zero falhas/ignorados.
- Media.Tests, novo projeto: **8 aprovados**, zero falhas/ignorados, com as bibliotecas nativas reais.
- **245 casos aprovados no total**, 24 além dos 221 da revisão de 20/10.

### Correções aplicadas

O relatório detalha A01–A23. Entre elas: descarregar mídia e limpar estado ao fechar/trocar projeto;
gerar cache de picos de WAV quando ausente; ler metadados RIFF/LIST como o Enhanced; manter
marcadores/tags de karaokê ao atrasar ou arrastar bordas; impedir auto-scroll durante o arraste;
não abrir painel de vídeo em projeto só de áudio; usar WAV externo com vídeo oculto e restaurar
áudio do contêiner ao mostrá-lo; retomar loop no EOF; evitar salto do cursor após pausa; preservar
WAV anterior ao cancelar extração; liberar recursos nativos na falha de construção; bloquear
opções de extração enquanto ela roda; descartar análises antigas de trilhas; persistir dicionário
pessoal no perfil quando a pasta instalada não é gravável; refletir bind malsucedido no menu;
preservar documento em falha de Reload; aplicar a família de fonte nas Suggestions e proteger
a gravação de `.peak` por temporário.

O script de instalação também ganhou `-PublishDirectory` e valida ProductVersion contra o projeto,
evitando empacotar a publicação alpha.2 com o nome alpha.3 quando usado `-NoPublish`.
Dois checks PowerShell confirmaram a recusa da versão antiga e a aceitação da alpha.3 até a
verificação do compilador, antes de gerar arquivos do instalador.

Diferenças deliberadas: `TWAVFile.Open` do Enhanced pulava um trecho fixo para aceitar LIST;
a porta percorre chunks RIFF de forma geral e respeita padding. O buffer final float32 é zerado
inteiro ao gerar picos, sem reproduzir o ZeroMemory de tamanho parcial do Delphi. A escolha entre
WAV e áudio do vídeo usa tracks de um mpv, em vez de dois grafos DirectShow; o contrato e os testes
mantêm a escolha funcional, mas não provam sincronia auditiva durante toda sessão real.

### Distribuição desta revisão

- Publicação autocontida alpha.3: `publish\VisualSubSync-win-x64-alpha.3\VisualSubSync.exe`.
  Foi escolhida uma pasta separada para preservar a alpha.2 e suas configurações/testes.
- Instalador alpha.2 informado pelo usuário como regenerado após a revisão anterior:
  `installer\Redist\VisualSubSync-2.0.0-alpha.2-Setup.exe`. Não incorpora automaticamente esta revisão.
- Instalador alpha.3: ainda requer executar `installer\build-installer.ps1` com NSIS 3 disponível.
  Para aproveitar o pacote já publicado, usar `-NoPublish -PublishDirectory publish\VisualSubSync-win-x64-alpha.3`
  e `-MakeNsis` com o caminho do compilador, se necessário.
  O compilador NSIS não foi encontrado na instalação padrão/PATH nem na busca de temporários
  realizada nesta sessão; não foi instalado outro programa para substituí-lo.

### Validação informada pelo usuário e pendências

- Usuário confirmou abertura e continuidade bidirecional Enhanced 1.2.29 ↔ .NET no projeto de
  Painted Skin em D:. Resultado real de um projeto, sem generalização para todos os formatos.
- Permanecem os testes humanos de foco, atalhos, mouse, som/cursor, CFR/VFR/HEVC, DPI e instalação
  limpa. Testes nativos desta revisão usam áudio sintético de 48 kHz, sem escuta nem imagem real.
- Os números anteriores de 36 diferenças em main e 11 em formatos/projeto/desfazer não vieram com
  lista de itens recuperável. Não foram marcados como encerrados por equivalência numérica.
- O relatório contém matriz de cobertura e extensões Enhanced ainda não portadas. O inventário
  integral não substitui a conferência semântica manual das rotinas restantes nem constitui uma
  declaração de paridade completa com 1.2.33.0.

### Diretriz de uso das referências — 21/10/2026

Confirmado pelo usuário: a migração tem como base os fontes originais do VisualSubSync
**1.0.1 (29/06/2013)**, preservando interface, fluxo de legendagem e compatibilidade dos projetos.
O executável Enhanced **1.2.29** e os fontes Enhanced **1.2.33.0** são referências auxiliares,
com proveniências distintas; nenhuma equivalência entre eles é presumida.

O repositório https://github.com/Red5goahead/VisualSubSync-Enhanced contém uma continuação
em Delphi: `Readme_Sources.txt` declara Delphi 7.0 e o README descreve a distribuição na Store
como Desktop Bridge. Isso não comprova uma migração para outra linguagem/runtime. A hipótese
de tentativa de migração e encerramento em 2021, levantada pelo usuário, permanece sem
confirmação histórica nesta revisão e não determina o escopo técnico.

As diferenças Enhanced inventariadas são oportunidades de melhoria, não pendências obrigatórias
de paridade. Avaliar cada candidata pelo benefício no uso real, compatibilidade com o original,
adequação à arquitetura .NET e testes de regressão. Registrar origem e decisão antes de incorporar;
não transportar automaticamente dependências DirectShow/LAV nem ampliar a interface por existir
um recurso no Enhanced. Prioridade: concluir a conferência semântica e os ajustes da migração
original; aproveitar correções e melhorias pontuais do Enhanced quando justificadas.

## Correções da comparação com os fontes — 23/10/2026

Comparação rotina a rotina de `main.pas`, `WAVDisplayerUnit.pas`, `MiniScrollBarUnit.pas`,
`SceneChangeUnit.pas`, `VSSClipboardUnit.pas`, `MRUListUnit.pas` e `ProjectUnit.pas` (1.0.1, conferidos
com os fontes Enhanced 1.2.33.0). Os relatórios ficam em `auditoria-paridade/` na pasta do projeto. Os
itens já resolvidos em A01–A23 (fechar a mídia, picos a partir do WAV, atraso e karaokê, áudio só de
WAV) não estão repetidos. Corrigido nesta revisão, sempre seguindo o 1.0.1:

| Rotina original | O que mudou na .NET |
| --- | --- |
| `WAVDisplayer1CursorChange` / `PlayCursorChange` | O tempo grande do cursor acompanha a reprodução e, parado, mostra o cursor de edição. |
| `chkAutoScrollSubClick` / `TimerAutoScrollSubTimer` | "Auto-scroll: Subtitles" funciona: a cada 80 ms, tocando, a legenda sob o cursor é focada e selecionada; fora de legenda o editor e o VO ficam vazios (a lista fica sem foco). |
| `vtvSubsListDblClick` | Tocando, o duplo clique desliga o auto-scroll da onda; parado com o vídeo à mostra, mostra o quadro inicial. |
| `WAVDisplayer1SelectedRangeChange` / `DisableVideoUpdatePreview` | Durante o arraste de uma borda a legenda do vídeo não é regravada a cada movimento. |
| `SaveSubtitles` | Toda gravação que não é cópia de segurança (Save As, troca de formato, tradução) limpa o "alterado"; a mensagem de extensão inválida da legenda do projeto tem o texto longo. |
| `SaveSubtitlesAsSSA/ASS` | Com vídeo aberto e cabeçalho vazio, grava PlayResX/PlayResY; "; Written by VisualSubSync" usa a versão do About. |
| `ClearVideoSceneChange` | Trocar ou tirar o vídeo apaga as mudanças de cena do anterior. |
| `ActionProjectPropertiesExecute` | Esconde o vídeo quando ele não ficou aberto (e não só quando nenhuma mídia abriu); depois de trocar o vídeo, volta a mostrar as legendas; SRT autocorrigido marca o projeto como alterado. |
| `LoadProject` / `TWAVExtractForm.FormActivate` | Ampulheta durante a abertura do projeto e a análise dos fluxos; "Loading audio file..." e "Loading video file...". |
| `TWAVDisplayer.LoadWAV` | Um `.peak` do tamanho do cabeçalho ou menor é refeito a partir do WAV. |
| `SetPlayCursorPos` / `SetPositionMs` | A rolagem automática não passa do fim do arquivo. |
| `OnScrollBarChange` / botão do meio | Rolar a vista pela mini barra ou com o botão do meio desmarca "Auto-scroll: WAV display". |
| `TMiniScrollBar` | Segurar o botão no trilho continua paginando a cada 150 ms até o polegar chegar ao mouse; qualquer botão pagina. |
| `PaintWavOnCanvas` | Sem picos carregados a área fica toda preta, sem a linha zero. |
| `WAVDisplayer1CustomDrawRange` | Legendas com karaokê não têm o texto desenhado na onda; o texto usa também o itálico da fonte do editor. |
| `CheckSubtitleForDynamicSelection` | O cursor sobre a borda é um desenho do `crHSplit` da VCL (duas barras com setas). |
| `ExtractKeyFramesAVI` | Em AVI, 100 quadros-chave seguidos (vídeo só de quadros-chave) não geram mudanças de cena; os tempos são truncados, como em AVI/MKV/MP4. |
| `AddSubtitle` / `DeleteSubtitles` | Inserir e apagar ajustam a posição de F3; apagar as últimas legendas não leva o foco para a primeira. |
| `ReplaceAllTextSubs` | Replace All com legendas na lista marca o projeto como alterado, mesmo sem trocas. |
| `UpdateStylesComboboxFromSelection` | Sem legenda selecionada, o combo de estilos fica como estava. |
| `TMRUList.LoadIni` | O `VisualSubSync.ini` é lido como o `TIniFile` (BOM, senão ANSI): projetos com acento no caminho não somem mais de Open Recent. |
| `GetACP` | A página ANSI é a do sistema ("linguagem para programas não Unicode"), não a do formato regional. |
| `LoadColumnsSettings` / `SaveSettings` | `[General] ColumnsPosition`: a ordem das colunas arrastadas é gravada e restaurada. |
| `AutoFitSubsListColumns` | As colunas (menos Text e VO) se ajustam ao conteúdo quando a fonte da lista muda e quando entra a primeira legenda. A grade mede só as linhas visíveis, também para a coluna #. |
| `LoadFormPosition(MainForm)` | Janela principal fora das telas volta para 100,100; `_State=1` abre minimizada; sem ini o Windows escolhe a posição (`poDefault`). O estado minimizado vale também para as outras janelas. |
| `EnableControl` | Os seis campos Sel/View ficam desabilitados sem projeto. |
| `TProjectForm.UpdateFormatCombobox` | Preencher o combo de formato pelo programa não troca mais a extensão (no WPF isso disparava a troca e um falso "arquivo mudou"); a tabulação do grupo de áudio segue o DFM. |
| `TPeakCreationProgressForm` | A criação de picos a partir do WAV mostra a janela "Creating peak file..." com a barra de progresso, sempre por cima, e a principal fica desabilitada; o trabalho roda fora da thread da interface. |
| `bttOpenBackupDirClick` | "Open backup dir" abre a mesma pasta em que os backups são gravados (a portátil ou `%APPDATA%\VisualSubSync\Backup`); um erro ao criar a pasta vira mensagem. |
| `TTranslateForm` | A janela Translate é redimensionável (608x289, mínimo 300x280), com os campos esticando como as âncoras do DFM. |
| TabOrder dos DFM | Delay, Translate, Silent zones, Extract WAV e Spell check seguem a ordem de Tab do DFM; cada grupo de rádios é uma parada só (as setas trocam o item), e o texto da verificação ortográfica não recebe Tab. |
| `vtvSuggestionsLstFocusChanged` / `DblClick` | Com várias sugestões selecionadas, o memo e o duplo clique usam a sugestão focada (a última clicada). |
| `SetPropValue` (jsbridge) | Valores fracionários atribuídos a `Start`/`Stop` por plugins são truncados, como o `RangedValue` do Delphi 7, e não arredondados. |
| `GetPropInfo` (jsbridge) | Os nomes de membros dos objetos expostos aos plugins não diferenciam maiúsculas (`sub.start` vale `sub.Start`). |
| `OnJsError` | `FatalError` continua ligado depois de um erro de script, como no original; `HasError` só descarta o resultado da chamada que falhou. |
| `TJSEngine` | Os plugins não têm mais limite de 30 s de execução (o original não tinha). O limite de recursão de 1000 níveis continua, para um plugin com recursão infinita dar erro em vez de derrubar o programa. |
| `TDShowRenderer.SetVolume` | O volume segue a curva do original: 1000·log2(v/100) centésimos de dB (-10 dB em 50%), convertida para a escala cúbica do mpv. |
| `TWaitCompletionThread` | O trecho em reprodução também termina (ou recomeça, em loop) quando o início dele passa da posição atual; a margem de um quadro absorve a busca exata. |
| `MemoSubPopupMenu` / `MemoSubtitleVOPopupMenu` | O menu de contexto do editor tem os ícones do Enhanced; o painel VO tem o menu "Copy VO to subtitlle text" (grafia do original), desabilitado por ser só do Enhanced. |

Diferenças mantidas de propósito:

- Um `.peak` com assinatura ou versão diferente não é lido; o projeto abre sem forma de onda e o motivo vai
  para o Log (o legado leria qualquer cabeçalho como dados).
- Responder "Yes" na pergunta ao fechar passa pelo mesmo Save do menu: com caracteres fora da página ANSI
  o arquivo passa a UTF-8, com o aviso. O original gravava em ANSI e trocava esses caracteres por "?".
- Numa correção de erros por plugin, desfazer volta ao valor de antes da correção, mesmo que dois plugins
  tenham mudado o mesmo campo (o original voltava ao valor intermediário).
- O servidor web, o campo `$|version|$` das páginas e o relatório de erros mostram a versão do projeto
  (`2.0.0-alpha.N`), e não os quatro números da FileVersion, que seria `2.0.0.0` em todas as alfas.
- O motor JS aceita qualquer número de argumentos nos métodos de `VSSCore` e `SceneChange` (o jsbridge dava
  "Not enough parameters"/"Too many parameters"), e `GetSelectedCount` devolve a contagem. Nenhum plugin
  distribuído depende disso.

