# Inventário do VisualSubSync original

Fonte analisada: `C:\Projetos\VisualSubSync` (Delphi 7, versão 1.0.1, última de 2013).
São cerca de 36.600 linhas em 66 unidades `.pas` próprias, mais os `.dfm`. Só `main.pas`
tem 9.886 linhas e `WAVDisplayerUnit.pas` tem 4.107. A pasta `modern\` dentro do original já
existia antes deste trabalho e não foi alterada.

## 1. Funcionalidades

| Área | O que faz | Fonte principal |
| --- | --- | --- |
| Projeto | Novo, abrir, salvar, propriedades; associa vídeo, WAV ou picos, legenda, legenda VO e dicionário. Aceita `.vssprj`, `.srt`, `.ass`, `.ssa` na linha de comando | `ProjectUnit.pas`, `main.pas` (LoadProject, SaveProject, ProcessParams) |
| Forma de onda | Desenho min/máx, zoom (seleção, tudo, vertical), rolagem, seleção, cursor, régua, intervalos de legenda em duas cores, zonas de silêncio, mudanças de cena, marcadores de karaokê, minibarra de rolagem | `WAVDisplayerUnit.pas`, `MiniScrollBarUnit.pas`, `VerticalScalingFormUnit.pas`, `SilentZoneFormUnit.pas` |
| Reprodução | Play, pause, stop, loop, tocar seleção (início, fim, até o fim, 1 s antes), velocidade 60 a 100%, vídeo embutido ou destacado, mostrar quadro de início, de fim e do cursor | `Renderer.pas` (DirectShow), `DetachedVideoFormUnit.pas` |
| Extração de áudio | Extrai WAV ou só picos do vídeo, com progresso | `WAVExtractFormUnit.pas`, `WavWriter\` (filtro DirectShow em C++), `WAVFileUnit.pas`, `PeakCreationProgressFormUnit.pas` |
| Legendas | Lista virtual (#, Start, Stop, Style, Text e colunas de plugins), editor com negrito, itálico, sublinhado, cor e tamanho, remover tags, dividir no cursor, mesclar (anterior, próxima, em uma linha, diálogo), atraso, buscar e substituir, ir para, copiar e colar (formato próprio), legenda VO | `main.pas`, `SubtitleModel.pas`, `DelayFormUnit.pas`, `FindFormUnit.pas`, `GotoFormUnit.pas`, `VSSClipboardUnit.pas`, `TntRichEditCustomUndoUnit.pas` |
| Sincronização | Modo normal e modo timing, definir início/fim pelo cursor, definir fim e ir para a próxima, deslocar ±10 ms e ±100 ms, adicionar legenda da seleção | `main.pas` (Action*Sub*, ActionShift*) |
| Text pipe | Painel com o roteiro em texto, adicionar ou substituir legenda a partir do texto selecionado, salvo em `.vssscript` | `main.pas` (ActionLoadTextPipe, ActionAddSubFromPipe) |
| Desfazer/refazer | Pilha de tarefas reversíveis para todas as edições | `UndoableTaskUnit.pas`, `UndoableSubTaskUnit.pas` |
| Verificação de erros | Relatório de erros, corrigir selecionados, próximo erro, preferências por plugin | `ErrorReportFormUnit.pas`, `JavaScriptPluginUnit.pas` |
| Plugins JavaScript | Plugins de erro (sobreposição, linha longa, CPS, duração, mudança de cena, tipografia francesa), colunas extras e ações registráveis com atalho | `JavaScriptPluginUnit.pas`, `ThirdParty\jsbridge`, `Release\jsplugin\` |
| Mudanças de cena | Lê quadros-chave do vídeo (AVI, MKV, MP4), cache em `.scenechange`, inserir e apagar, mostrar ou ocultar | `SceneChangeUnit.pas`, `MatroskaHelper.pas`, `MP4File.pas`, `ThirdParty\VfW.pas` |
| Ortografia | Hunspell, verificação ao digitar e diálogo, dicionário pessoal `perso.dic` | `LibHunspellUnit.pas`, `SpellCheckFormUnit.pas` |
| Estilos SSA/ASS | Editor de estilos | `StyleFormUnit.pas`, `SSAParserUnit.pas` |
| Karaokê | Inserir marcadores, posicionar cursores no fim | `main.pas` (ActionInsertKaraokeMarker) |
| Tradução | Gera modelo de tradução | `TranslateFormUnit.pas` |
| Servidor web | Servidor HTTP próprio para revisão e sugestões por navegador, páginas `.shtml` dinâmicas | `ServerUnit.pas`, `PageProcessorUnit.pas`, `DynamicPageProcessorUnit.pas`, `SuggestionFormUnit.pas`, `Release\web\` |
| Preferências | Geral, backup automático, fontes, atalhos (modo normal e timing), mouse, forma de onda, servidor web, janelas, MRU | `PreferencesFormUnit.pas`, `MRUListUnit.pas` |
| Presets | Conjuntos de preferências carregáveis por projeto | `main.pas` (LoadPresetFile) |
| Exportar | Exportar WAV da seleção, salvar como SRT, SSA, ASS, CUE, TXT, CSV | `main.pas` (SaveSubtitlesAs*, ActionExportToWAV) |
| Logs | Janela de log | `LogWindowFormUnit.pas` |

## 2. Formatos de arquivo

| Arquivo | Formato verificado no código |
| --- | --- |
| `.vssprj` | INI, seção `[VisualSubsync]`, gravado por TTntIniFile em UTF-16 LE com BOM. Chaves: VideoSource, WAVFile, PeakFile, WAVMode (0 sem onda, 1 WAV externo, 2 só picos; padrão 1), SubtitlesFile, SubtitlesVO, TextPipeSource e TextPipePosition (só quando há texto), VideoPanelWidth/Height, ShowVideo, ShowVO, DetachedVideo, VideoWindowNormalLeft/Top/Width/Height, WAVDisplayerPositionStartMs/StopMs, FocusedTimeMs, Dictionnary (grafia original), Presets. Caminhos relativos ao projeto; em outra unidade ficam absolutos. Booleanos `1`/`0` |
| `.peak` | Binário little-endian: `"PeakFile"` (8 bytes), versão uint32 `0x0100`, LengthMs int32, nSamplesPerSec uint32, nChannels uint16, wBitsPerSample uint16, SamplesPerPeak uint32 (taxa ÷ 100, 10 ms), PeakTabSize uint32, depois pares int16 Max, Min. Fica ao lado do WAV com o mesmo nome. O legado normaliza e regrava ao abrir |
| `.scenechange` | Texto ao lado do vídeo: `SceneChangeFormatVersion=1`, depois um tempo `hh:mm:ss.mmm` por linha |
| `.vssscript` | Conteúdo do text pipe em RTF, ao lado do projeto |
| `VisualSubSync.ini` | Preferências. Fica ao lado do executável ou em `%APPDATA%` se a pasta for somente leitura. Seções General, Backup, Fonts, Hotkeys (`Ação[Normal]`, `Ação[Timing]`), Mouse, WAVDisplay, WebServer (porta padrão 80), Windows, Misc, TextPipe, mais a lista de arquivos recentes |
| `.bak` | Backups automáticos na pasta de backup |
| Legendas lidas | SRT, SSA, ASS. Codificação: BOM UTF-8, UTF-8 sem BOM ou ANSI pela página de código do sistema (GetACP). A codificação é preservada ao salvar |
| Legendas gravadas | SRT, SSA, ASS, CUE, TXT, CSV |
| Dicionários | Hunspell `.aff`/`.dic` em `dict\`, mais `perso.dic` |
| Plugins | `.js` em `jsplugin\` (API em `jsplugin\readme.txt`: VSSCore, VSSPlugin, SceneChange, Subtitle, ScriptLog, LoadScript, SetStatusBarText) |
| Área de transferência | Formato registrado `CF_VisualSubSync`, mais texto Unicode |

## 3. Dependências do original

| Dependência | Uso | Situação no Windows atual |
| --- | --- | --- |
| Delphi 7, VCL, TNT Unicode, Virtual Treeview | Interface e Unicode | Obsoletas |
| DirectShow (`DirectShow9.pas`) e codecs instalados | Reprodução e extração de áudio | Funciona, mas depende de pacotes de codecs de terceiros |
| `WavWriter.ax` (C++, `WavWriter\`) | Filtro DirectShow para gravar WAV | Exige registro COM e build em Visual C++ 6 |
| DirectVobSub / VSFilter | Legendas sobre o vídeo | Projeto descontinuado |
| SpiderMonkey 1.5 (`js3215R.dll`) | Plugins JavaScript | Sem manutenção desde 2007 |
| `libhunspell.dll` | Ortografia | Hunspell segue mantido |
| VfW, parsers Matroska e MP4 próprios | Quadros-chave | VfW é legado |
| WinSock2 | Servidor web | Substituível pela biblioteca padrão |
| RichEdit TOM (`tom_TLB.pas`) | Editor com formatação | Substituível |
| FastMM, JCL, DeCAL, RegExpr | Utilitários | Não necessários em .NET |
| DUnit (`dunit\`) | Testes de SubtitleModel, MiscTools e plugins | Servem de referência para os testes novos |
