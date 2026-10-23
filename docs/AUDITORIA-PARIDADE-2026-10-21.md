# Auditoria de paridade — 21/10/2026

## Bases e limites

| Base | Local | Uso |
| --- | --- | --- |
| VisualSubSync 1.0.1 / 2013 | `C:\Projetos\VisualSubSync` | Especificação principal de comportamento |
| Enhanced 1.2.29.0 | `C:\Projetos\VisualSubSyncEnh\VisualSubSyncEnh.exe` e recursos extraídos | Referência visual da instalação do usuário |
| Enhanced 1.2.33.0 | `C:\Projetos\VisualSubSyncEnh\fontes` | Fonte adicional, agora disponível; versão confirmada em `VisualSubSyncEnh.dof` |
| VisualSubSync.NET alpha.3 | `C:\Projetos\VisualSubSync.NET` | Implementação revisada |

O fonte Enhanced foi acrescentado pelo usuário durante a auditoria. As observações anteriores
que diziam não haver fonte referiam-se ao material disponível antes disso. Fonte 1.2.33.0 e
executável 1.2.29 não são a mesma versão; uma diferença entre eles não é automaticamente um
defeito da migração. Não se passou a adotar toda extensão do Enhanced como requisito de 2013.

Os números "36 diferenças em main.pas" e "11 em formatos/projeto/desfazer" foram informados
pelo usuário, mas não havia uma lista identificada por rotina nos documentos encontrados.
Esta revisão registra achados independentes e não declara ter encerrado essas 47 diferenças.

## Rastreabilidade reproduzível

`python tools/audit-parity.py` lê os fontes sem modificá-los e gera
`docs/RASTREABILIDADE-PARIDADE-2026-10-21.json`:

- 1.530 entradas de declarações/rotinas dos `.pas` na raiz original, com linha, hash normalizado
  e arquivos candidatos da .NET encontrados por identificadores/comentários.
- Entre unidades de mesmo nome: 1.371 entradas iguais após normalização, 154 alteradas,
  270 exclusivas do Enhanced e 5 exclusivas do original. Há 11 unidades novas no Enhanced.
- Em `main.pas`: 255 iguais, 75 alteradas, 127 novas do Enhanced e 3 exclusivas do original.
- Em `WAVDisplayerUnit.pas`: 126 iguais, 11 alteradas e 2 novas do Enhanced.
- Os leitores SRT e SSA/ASS e o servidor Delphi permanecem iguais após normalização entre
  as duas bases. A comparação não elimina a necessidade de conferir a porta C#.
- Os 12 arquivos de plugins, 8 de web e 42 de ajuda do original estão presentes e idênticos
  byte a byte na distribuição de conteúdo da .NET.

Esses números são um índice mecânico, não um certificado de equivalência: overloads são pareados
por nome/ocorrência; candidatos textuais podem ser falsos positivos; ausência de nome não prova
ausência da função. A revisão semântica realizada concentra-se nos caminhos abaixo e nos seus
testes. Não se declara verificação manual linha a linha de todas as 1.530 entradas.

## Diferenças confirmadas e corrigidas

| ID | Referência original/Enhanced | Divergência na .NET e correção | Evidência |
| --- | --- | --- | --- |
| A01 | `main.pas: CloseProject`, `TWAVDisplayer.Close`, `TDShowRenderer.Close` | Fechar projeto não descarregava mídia nem limpava reprodução/vídeo. Contrato Close implementado; timer, flags e cursor são zerados. | App: Close_project; Media: Closing_real_mpv |
| A02 | `LoadProject` / `ActionProjectPropertiesExecute` | Ao trocar para projeto sem mídia, o motor anterior podia continuar aberto. Abertura limpa o estado anterior antes de selecionar a nova fonte. | Compilação, caminho compartilhado com Close e reabertura nativa |
| A03 | `TWAVDisplayer.LoadWAV/CreatePeakTab` | WAV sem `.peak` ficava sem onda. Criado gerador para PCM 8/16 e float 32, blocos de ~10 ms, canais intercalados e normalização do legado. | Core: WavPeakCompatibility; App: Opening_external_wav |
| A04 | Enhanced `WAVFileUnit.pas: TWAVFile.Open` | Leitor recusava `LIST` antes de `data`. Leitura RIFF passa por chunks, incluindo tamanhos ímpares/padding, sem presumir 30 bytes fixos. | Core: Reads_ffmpeg_list_metadata |
| A05 | `main.pas: ApplyDelay` | Atraso movia início/fim, mas deixava marcadores de karaokê na posição antiga. Ambos os tempos deslocam também SubTimes; desfazer/refazer acompanha. | App: Delay_and_undo_move_karaoke_markers |
| A06 | `main.pas: SetSubtitleTime` | Arrastar início/fim de karaokê não recalculava as tags `\k`. O texto é refeito a partir dos marcadores absolutos. | App: Dragging_karaoke_boundaries |
| A07 | `ApplyDelay` / `WAVDisplayer1KaraokeChanged` | Atraso só de uma borda da faixa selecionada não refazia as tags. Recalcula o texto sem criar outra tarefa de desfazer, como o caminho legado. | Revisão semântica; testes A05/A06 cobrem os componentes, não todos os modos de uma borda |
| A08 | `TWAVDisplayer.SetPlayCursorPos` | Auto-scroll podia mover a vista enquanto o mouse arrastava seleção ou scrollbar. Respeita o estado de interação da onda. | App: Playback_does_not_scroll_viewport |
| A09 | `ActionShowHideVideoExecute` | Projeto somente de áudio podia abrir um painel de vídeo vazio. A condição passa a exigir vídeo aberto. | App: Audio_only_project |
| A10 | `LoadProject` / `ActionShowHideVideoExecute`, seleção AudioOnlyRenderer/VideoRenderer | Com vídeo oculto, o som continuava vindo do contêiner mesmo havendo WAV próprio. Agora seleciona WAV externo no mpv e restaura áudio do contêiner ao mostrar vídeo. | App: Audio_selection_follows_video_visibility; Media: External_wav_selection |
| A11 | `TWAVDisplayer.PlayRange` / repetição do renderer | Loop atingindo EOF podia ficar pausado por keep-open. Repetição retoma após buscar o começo. | Media: Loop_at_eof |
| A12 | `TWAVDisplayer.Pause` / cursor do renderer | Relógio interpolado podia somar o tempo pausado ao retomar. A base do relógio é reiniciada. | Media: Resuming_after_pause |
| A13 | `WAVExtractFormUnit` / novo backend FFmpeg | Cancelar/falhar extração podia deixar WAV parcial ou sobrescrever o anterior. WAV é produzido em temporário e só substitui destino após conclusão. | Media: Cancelled_extraction e Successful_extraction |
| A14 | `Renderer.pas` / backend FFmpeg | Falha no construtor após abrir mídia podia deixar handles nativos abertos. Recursos são liberados também na falha de construção. | Media: Invalid_stream_selection_releases_input_file |
| A15 | `TExtractWAVForm` | Tipo de extração podia mudar na interface durante trabalho já iniciado. Opções e botão Close ficam indisponíveis durante a extração; Stop continua disponível. | Compilação; interação visual ainda pendente |
| A16 | Enhanced `ProjectUnit.pas: VideoSourceOperation`, mudança de fonte | Análises de trilhas concorrentes podiam aplicar o resultado do arquivo anterior. Número do pedido e caminho validam o resultado antes da aplicação. | Revisão estática; cenário rápido de troca na tela ainda pendente |
| A17 | `LibHunspellUnit` / Enhanced `g_AppDataDictPath` | `perso.dic` podia não persistir instalado em Program Files. Mantém pasta portátil quando gravável e usa perfil do usuário quando necessário; erro final é informado. | Caminhos de escrita já exercitados no núcleo; instalação sob ACL real ainda pendente |
| A18 | `ServerUnit.pas` / `StartStopServer` | Menu podia indicar servidor iniciado depois de bind malsucedido. Estado agora depende do listener criado. | App: Failed_server_bind |
| A19 | `ActionReloadExecute` | Exceção de leitura em Reload escapava do comando e podia derrubar o aplicativo. Mantém documento atual e informa falha antes de substituí-lo. | App: Reload_failure |
| A20 | `ApplyFontSettings` / `SuggestionForm.UpdateFonts` | Fonte da janela Suggestions não acompanhava a família da lista. Aplica Font.Name ao criar e após Preferences, sem alterar tamanho/estilo que o legado não altera nesse ponto. | App: Suggestions_use_the_subtitle_list_font_family |
| A21 | `TWAVDisplayer.LoadWAV: SavePeakFile` | Cache `.peak` era sobrescrito diretamente. Gravação concluída em memória e substituição via temporário, como os demais arquivos protegidos na revisão anterior. | Core: round-trip de picos e gravação de cache |
| A22 | Publicação/instalador .NET | `-NoPublish` podia empacotar binários alpha.2 com nome alpha.3. Script recebe `-PublishDirectory` e confere ProductVersion antes de gerar o instalador. | Dois checks PowerShell: recusa da versão antiga e aceitação da pasta alpha.3 até a verificação do compilador |
| A23 | `ActionProjectPropertiesExecute`, reabertura do AudioOnlyRenderer | Trocar WAV nas propriedades não reabria a fonte de áudio; agora reabre a mídia sem vídeo ou atualiza a faixa externa com vídeo aberto. | App: Changing_wav_in_project_properties |

Os nomes completos e asserções estão nas suítes; a coluna acima distingue teste direto, revisão
estática e pendência visual. Teste de componente não implica validação completa de todos os fluxos.
Na saída de áudio, usa-se `audio-add`/`audio-remove` da API do mpv:
[Manual do mpv](https://mpv.io/manual/stable/#track-manipulation). O teste nativo verifica a faixa selecionada,
sem reproduzir som no dispositivo do usuário.

## Cobertura por grupo solicitado

| Grupo | Resultado desta revisão | O que ainda não está encerrado |
| --- | --- | --- |
| Últimas alterações: WAV, projeto e mídia | Revisados fluxo de extração, ordinal de faixa, cancelamento, reabertura, EOF, áudio do WAV e cache | Multifaixa e diálogo FC com mídia real; FC continua desabilitado como documentado |
| main.pas | Indexado integralmente; comparação semântica de ciclo de projeto, reprodução, atrasos, karaokê, Reload e servidor; achados A01–A21 | Demais combinações de ações e os 36 itens anteriores sem lista recuperada não foram declarados encerrados |
| Forma de onda | Conferidos encaixe/minimum blank, seleção, conversões de coordenadas, zoom e auto-scroll nos caminhos consultados; corrigido bloqueio de rolagem durante interação | Paridade de todos os traços/pixels, DPI, multi-monitor, seleção VO e mouse Normal/Timing em uso real |
| Formatos, projeto e desfazer | SRT/SSA/ASS iguais entre fontes Delphi; suíte de formatos preservada; regressões de karaokê e projeto cobertas | Corpus completo gerado nos dois aplicativos; os 11 itens anteriores não foram identificados individualmente |
| Janelas secundárias | Mapeadas famílias de diálogos e verificado o caminho de projeto/extração/Suggestions | Comparação visual e de teclado de cada janela, tabulação, fontes e escalas |
| Plugins | Scripts originais comparados byte a byte e mantidos; API nova Enhanced identificada abaixo | Compatibilidade completa com o pacote de scripts Enhanced e suas extensões |
| Servidor e layout | Fonte HTTP Delphi original/Enhanced idêntico; testes HTTP mantidos; estado de bind corrigido | Navegador e layout durante uma sessão real de revisão |

## Enhanced: diferenças adicionais identificadas, não habilitadas como se já estivessem portadas

O arquivo de rastreabilidade enumera todas as rotinas novas encontradas nas unidades comuns e as
11 unidades exclusivas. Este é o mapa inicial para os incrementos Enhanced, cuja implementação
deixa de depender de adivinhação por observação. O alvo-base continua sendo a versão de 2013.

| Grupo Enhanced | Fonte para continuar | Situação na .NET |
| --- | --- | --- |
| Quick & Smart Delay | `main.pas: ActionDelayMove*`, `MoveToFromCurrentToEnd` | Painel presente; não confundir com o Delay original já portado |
| Silent Zone rápido | `SpeedButtonUpQuickSilentZoneClick`, `FillZone`, `ComboBoxSilentZoneSelect` | Janela original de zonas existe; painel rápido não equivale ao diálogo original |
| Smart Desynch e ressincronização progressiva | `ResynchToolFormUnit.pas`, `UndoableSubTaskUnit.pas` e handlers em main | Requer porta própria com desfazer e validação numérica |
| Alterar FPS e manter duração | `main.pas: ChangeFps` e handlers dos pares de taxas | Não habilitado como função concluída |
| Ajustar onda | `TWAVDisplayer.ShiftPeakData` | Exige decisão sobre diferenças de borda e proteção do cache; não aplicado automaticamente |
| Comparar legendas | `Diff.pas`, `DiffSubsFormUnit.pas` | Módulo exclusivo ainda sem equivalente completo |
| API JavaScript Enhanced | `TVSSCoreWrapper.RegisterJS` | 11 funções novas: DeleteSubtitle, GetCursorPosition, GetPluginParamValue, SetPluginParamValue, SetSubColor, SetSubIcon, ResetSubColor, EnableJavascriptItemMenu, DisableJavascriptItemMenu, InsertBreakBeforeJavascriptMenuItem, InsertBreakAfterJavascriptMenuItem |
| Configuração LAV/VSFilter | `Lav*SettingsInterface.pas`, `Renderer.pas` | Detalhe do backend antigo; não migrar COM por cima de mpv sem necessidade funcional |
| Operações no arquivo de vídeo | `ProjectUnit.VideoSourceOperation`, `SourceFileOperationCancel.pas`, `HashUnit.pas` | Incluem ferramentas externas e precisam de implementação/validação explícita |
| Dicionários on-line, thesaurus e correções inteligentes | `main.pas`, `LibHunspellUnit.pas`, `PreferencesFormUnit.pas` | Não garantidos pela porta Hunspell original |
| Exportações adicionais e correções externas | `ConvertSubToNetFlixFormat`, `ConvertSubToYouTubeSbv`, `ConvertSubTowebVTT`, `ActionSubtitleEdit` | Dependem de chamadas a Subtitle Edit; não são simples formatos já implementados no Core |

## Testes e distribuição

Resultados finais: **220 Core + 17 App + 8 Media = 245 testes aprovados**, sem falhas/ignorados;
build sem avisos/erros. Dois checks PowerShell do empacotamento também passaram. Publicação
autocontida em `publish/VisualSubSync-win-x64-alpha.3`, ProductVersion `2.0.0-alpha.3` confirmado.
O instalador alpha.3 ainda precisa do NSIS 3, ausente nesta sessão. Os testes nativos usam
WAVs sintéticos de 48 kHz e libmpv sem áudio/vídeo reais. A primeira execução com WAV sintético de
1 kHz foi rejeitada pelo caminho de reprodução do mpv; o corpus foi corrigido para 48 kHz.
Isso não é evidência de suporte a toda taxa de amostragem ou a todos os codecs.

Nenhum arquivo do projeto de legendagem em D: foi alterado. A continuidade Enhanced ↔ .NET
no projeto Painted Skin é resultado informado pelo usuário, não teste executado por esta auditoria.
