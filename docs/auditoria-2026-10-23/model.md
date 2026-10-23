# Auditoria de paridade — fatia "model"

Escopo: SRTParserUnit, SSAParserUnit, SubStructUnit, SubtitleModel, ProjectUnit (+dfm), MiscToolsUnit,
VSSClipboardUnit, MRUListUnit, UndoableSubTaskUnit, UndoableTaskUnit, TntRichEditCustomUndoUnit,
FileReaderUnit, GlobalUnit e as rotinas SaveSubtitles* de main.pas, comparadas com
`src/VisualSubSync.Core` e o uso em `src/VisualSubSync.App`.

Caminhos .NET relativos a `<NET>/src/`.

---

### Lista recente perde projetos com caminho não ASCII ao reabrir o programa
- Gravidade: alta
- Original: MRUListUnit.pas:131-157 (SaveIni / LoadIni). O INI é lido e gravado pela API ANSI (TIniFile). `UTF8Decode` é aplicado aos bytes que foram gravados.
- .NET: VisualSubSync.Core/Projects/RecentFiles.cs:55-94, VisualSubSync.Core/Settings/VssSettings.cs:180 e 391, VisualSubSync.Core/Text/IniDocument.cs:42, VisualSubSync.Core/Text/TextFileReader.cs:44
- Diferença: `Save` grava `Recent0..7` como "UTF-8 dentro de ANSI" (por exemplo, "é" vira os bytes C3 A9). Depois `ini.Save(FileName, Ansi)` grava o arquivo sem BOM. Na carga seguinte, `IniDocument.Load` passa por `TextFileReader.ReadAllText`. Essa função trata um arquivo sem BOM que é UTF-8 válido e não ASCII como UTF-8, e é exatamente o caso de um INI cujo único texto não ASCII são essas entradas. O texto lido já vem como "é", então `Utf8Decode` (`Ansi.GetBytes` → E9 → UTF-8 inválido) devolve "" e a entrada é descartada. O mesmo acontece com um VisualSubSync.ini gravado pelo original. Resultado: projetos com acento no caminho somem de File > Open Recent, e `LoadMostRecentProjectOnStartup` deixa de abri-los. Os testes (RecentFilesTests) só testam a ida e volta em memória.
- Correção sugerida: ler o VisualSubSync.ini sempre em ANSI, sem a heurística de UTF-8, como faz a API do Windows; por exemplo, um `IniDocument.Load(path, Encoding)` usado em `VssSettings`. Outra opção: em `RecentFiles.Utf8Decode`, quando os bytes ANSI não formarem UTF-8 válido, devolver o texto como veio. Incluir um teste que grava em disco e relê.

### Cabeçalho SSA/ASS padrão nunca recebe PlayResX/PlayResY
- Gravidade: média
- Original: main.pas:6694-6698 (SaveSubtitlesAsSSA) e main.pas:6791-6795 (SaveSubtitlesAsASS): com vídeo aberto e cabeçalho vazio, grava `PlayResX: <largura>` e `PlayResY: <altura>`.
- .NET: VisualSubSync.Core/Subtitles/Formats/SsaFormat.cs:135-139 (suporta `options.VideoWidth/VideoHeight`), mas VisualSubSync.App/ViewModels/MainViewModel.cs:436, 471-475 e 528, e MainViewModel.Preferences.cs:102 criam `SubtitleWriteOptions` sem `VideoWidth`/`VideoHeight`.
- Diferença: ao salvar um SSA/ASS novo, ou ao converter SRT→SSA/ASS (Save As, Project Properties ou cópia automática) com vídeo aberto, o arquivo sai sem PlayResX/PlayResY. O original grava os dois. Isso muda a escala da renderização no libass/VSFilter.
- Correção sugerida: passar o tamanho do vídeo aberto (`_engine.VideoWidth/VideoHeight`, quando `IsOpen`) nas opções de todas as gravações SRT/SSA/ASS. O melhor caminho é centralizar isso num `CreateWriteOptions()`.

### Save As não limpa o estado de "modificado"
- Gravidade: média
- Original: main.pas:3374-3375 (ActionSaveAsExecute → SaveSubtitles) e main.pas:2756-2760 (SaveSubtitles): sem `BackupOnly`, faz `ApplyAutoBackupSettings` e `CurrentProject.IsDirty := False` também no Save As. O '*' some do título e Save fica desabilitado.
- .NET: VisualSubSync.App/ViewModels/MainViewModel.cs:446-476 (SaveAs → SaveTo, linhas 1383-1405). `IsDirty` só é zerado em `Save()` (linha 438).
- Diferença: depois de Save As, o .NET continua com o projeto marcado como modificado (título com '*', Save habilitado e pergunta ao fechar). No original, o estado fica limpo. Isso não aparece em "Diferenças registradas".
- Correção sugerida: zerar `IsDirty` quando o `SaveTo` de SaveAs der certo, como no original. Se a intenção for manter o .NET mais seguro, registrar a diferença no plano.

### Project Properties: troca automática de extensão ao preencher o combo de formato (falso "arquivo mudou")
- Gravidade: média
- Original: ProjectUnit.pas:600-606 (ConfigureInModifyProjectMode → UpdateFormatCombobox) e ProjectUnit.pas:704-726. Na VCL, atribuir `ItemIndex` por código não dispara `OnChange`, então `cbSubtitleFormatChange` só roda quando o usuário escolhe no combo.
- .NET: VisualSubSync.App/Views/ProjectFormWindow.xaml.cs:72-90 e 181-210; ProjectFormWindow.xaml:104 (`SelectedIndex="0"` + `SelectionChanged`).
- Diferença: no WPF, mudar `SelectedIndex` por código dispara `SelectionChanged`. Ao abrir Project Properties de um projeto com legenda "X.ASS" ou "X.Ssa", `UpdateFormatCombobox` muda o índice de 0 para 2 ou 1. O handler roda `Path.ChangeExtension(..., ".ass")` e o campo vira "X.ass". Ao clicar em OK, `project.SubtitlesFile != form.SubtitlesFile` (comparação exata, MainViewModel.cs:509) entra no ramo de troca de legenda: pede para salvar se houver alterações, recarrega o documento, perde o desfazer e grava o projeto com o novo nome, mesmo sem nada ter sido alterado. O mesmo acontece em Browse de legenda, vídeo, WAV ou picos quando a extensão não está em minúsculas.
- Correção sugerida: ignorar `SelectionChanged` durante as atribuições por código (uma flag `_updatingFormat` em `UpdateFormatCombobox`), reproduzindo o comportamento da VCL.

### Colar texto ASS sem cabeçalho perde o campo Layer
- Gravidade: baixa
- Original: VSSClipboardUnit.pas:154-160 (TryPasteAsSSA): lê `Marked` e `Layer` de forma independente, cada um quando existe no `Format:`, sem olhar se o texto é ASS.
- .NET: VisualSubSync.Core/Subtitles/SubtitleClipboard.cs:198-225 com SsaFormat.cs:104-111.
- Diferença: `ParseText` reaproveita `SsaFormat.Parse`, que só lê `Layer` quando `isAss` (ScriptType v4.00+ ou `[v4+ Styles]`). Ao colar só a seção `[Events]` com `Format: Layer, Start, ...` (o caso típico de área de transferência), `isAss` é falso, o Layer do texto (por exemplo, 2) é ignorado e fica "0". O original mantém o valor colado.
- Correção sugerida: em `ParseText`, ler Marked e Layer independentemente do tipo detectado (expor no parser os valores crus por chave ou uma opção "clipboard").

### "Paste at cursor" com SRT fora de ordem usa outra legenda como referência
- Gravidade: baixa
- Original: VSSClipboardUnit.pas:104-124 (TryPasteAsSRT usa TSRTParser na ordem do texto, sem ordenar) e main.pas:9588-9596 (ActionPasteAtCursorExecute desloca por `SubList[0]`).
- .NET: VisualSubSync.Core/Subtitles/Formats/SrtFormat.cs:67 (`document.Sort()` dentro de `Parse`), usado por SubtitleClipboard.ParseText:201 e MoveToCursor:228-242.
- Diferença: o .NET ordena o SRT colado e alinha ao cursor a legenda de menor início. O original alinha a primeira do texto. O plano registra essa ordenação só para SSA.
- Correção sugerida: tirar o `Sort()` de `SrtFormat.Parse` e ordenar em `SubtitleFile.Parse`/`Load`, como o `FullSort` de LoadSRT, para que o parser preserve a ordem como o TSRTParser. Outra opção: registrar a diferença também para SRT.

### Versão em "; Written by VisualSubSync ..." difere da versão do programa
- Gravidade: baixa
- Original: main.pas:6680 e 6777 (`g_ApplicationVersion.VersionString`, a mesma string usada no About, no servidor web e no Error report; GlobalUnit.pas:68, MiscToolsUnit.pas:438-447).
- .NET: VisualSubSync.Core/Subtitles/Formats/SubtitleWriteOptions.cs:31-32 (padrão: versão do assembly Core, "2.0.0.0"). O App usa `MainViewModel.ApplicationVersion` ("2.0.0-alpha.2", MainViewModel.Export.cs:16-23) em todos os outros lugares e nunca preenche `ApplicationVersion` nas opções.
- Diferença: os arquivos SSA/ASS saem com uma versão diferente da mostrada no About e nas páginas web. No original é a mesma string.
- Correção sugerida: preencher `ApplicationVersion = MainViewModel.ApplicationVersion` nas opções de gravação, ou definir uma única fonte de versão no formato a.b.c.d.

### Página ANSI tirada da cultura do usuário, e não de GetACP
- Gravidade: baixa
- Original: MiscToolsUnit.pas:1414-1430 (WC2MB/MB2WC com `GetACP`); as leituras sem BOM usam a página ANSI do sistema.
- .NET: VisualSubSync.Core/Text/TextFileReader.cs:57 (`CultureInfo.CurrentCulture.TextInfo.ANSICodePage`)
- Diferença: `CurrentCulture` segue o formato regional do usuário, enquanto GetACP segue a "linguagem para programas não Unicode" do sistema. Quando os dois diferem (por exemplo, formato russo num Windows ocidental), legendas ANSI, CUE, INI e MRU são lidos e gravados numa página diferente da do original, com caracteres trocados.
- Correção sugerida: usar a página do sistema (`GetACP` via P/Invoke ou `Encoding.GetEncoding(0)` com `CodePagesEncodingProvider`, que no .NET Core devolve a ACP do sistema no Windows).

### Mensagem de extensão inválida não distingue o arquivo do projeto
- Gravidade: baixa
- Original: main.pas:2744-2754 (SaveSubtitles): para `Filename = CurrentProject.SubtitlesFile`, mostra 'Cannot save the file "..." . Please, specify a valid file extension in the "Project Properties" dialog under the "File" menu.'; para outros arquivos, a mensagem curta.
- .NET: VisualSubSync.App/ViewModels/MainViewModel.cs:1385-1389 (SaveTo): sempre a mensagem curta.
- Diferença: o texto da mensagem é diferente quando Save, a pergunta ao fechar ou a cópia de segurança encontram a legenda do projeto com extensão inválida.
- Correção sugerida: em `SaveTo`, comparar `target` com `Project.SubtitlesFile` e usar o texto longo nesse caso.

### Pergunta ao fechar passa a converter para UTF-8 e avisar
- Gravidade: baixa
- Original: main.pas:4252-4270 (CheckSubtitlesAreSaved): chama `SaveSubtitles(..., CurrentProject.IsUTF8, ...)` direto, sem `DetectCharsetDataLoss`, sem aviso e sem `SetSubtitleFilename` do vídeo.
- .NET: VisualSubSync.App/ViewModels/MainViewModel.cs:1307-1324 → `Save()` (linhas 422-443), que roda `PromoteToUtf8IfNeeded` com o aviso de charset e `SetVideoSubtitleFile`.
- Diferença: respondendo "Yes" ao fechar ou trocar de projeto, o .NET pode mostrar o aviso "Subtitles contain some characters..." e gravar em UTF-8. O original grava em ANSI, trocando os caracteres por '?'. A mudança evita perda de dados, mas não está registrada.
- Correção sugerida: registrar como diferença deliberada no PLANO, ou reproduzir a chamada direta (`SaveTo` + `SaveProject`) sem a promoção.

### Ordem de tabulação do grupo de áudio na janela de projeto
- Gravidade: baixa
- Original: docs/enhanced/TPROJECTFORM.txt:261-318 (gbWAVFile: EditWAVFilename 0, rbExternal 1, rbPeakOnly 2, EditPeakFilename 3, rbNoWaveform 4); ProjectUnit.dfm tem a mesma ordem.
- .NET: VisualSubSync.App/Views/ProjectFormWindow.xaml:79-97 (ordem do XAML: NoWaveform, PeakOnly, PeakFileEdit, External, WavFileEdit, sem TabIndex)
- Diferença: o Tab percorre os controles do grupo de áudio em outra ordem. Os outros grupos seguem a ordem do Enhanced.
- Correção sugerida: definir `TabIndex`/`KeyboardNavigation.TabIndex` conforme o DFM do Enhanced, ou reordenar os elementos no XAML.

---

Sem diferença encontrada (conferidos linha a linha): TSRTParser.Load e IsTimeStampsLine (lixo antes do primeiro tempo, Trim por linha, remoção do índice seguinte, correção fim = início − 1 com AutoCorrected → IsDirty); TimeStringToMs, TimeStringToMS_SSA, TimeMsToString, TimeMsToSSAString e TimeMsToCUE (arredondamento bancário); TSSAParser (seções, Format e Dialogue com o último campo levando o resto, rodapé, Values com ':'), LoadASS (Layer/Marked por IsASS, `\N`), LoadStylesFromParser; SaveSubtitlesAsSRT/SSA/ASS/CUE/TXT/CSV (cabeçalhos, Format com espaço duplo, regex do ScriptType, Trim do cabeçalho e do rodapé, BOM, CUE sempre ANSI, conversões de tags conforme a extensão anterior, ordem Translator → Convert); DetectCharsetDataLoss e o aviso de ActionSave; backup .bak e o timer automático (formato da legenda, sem mexer no estado de modificado); formato binário CF_VisualSubSync (ordem dos 9 textos, MarginR antes de MarginL) e texto SRT da cópia; TSubtitleRangeFactory (padrões Layer "0", "Marked=0", margens "0000", Style "Default"); TSubtitleRangeWrapper.GetFieldValue; TVSSProject.LoadFromINIFile e SaveProject (chaves, padrões, caminhos relativos, TextPipe só com conteúdo, aviso de somente leitura); TProjectForm (CheckData e textos, confirmação de sobrescrita, Browse e preenchimentos automáticos, placeholder "Leave empty for extraction", Esc); TMRUList (limite 8, sem diferenciar maiúsculas, Clear list e Clear dead entries, submenu desabilitado vazio); PushUndoableTask, Undo e Redo, TUndoableTextTask (DiffText, Merge das três formas, DisableMerge após desfazer), TUndoable{SetTime,Delay,Add,Delete,Split,Merge,MultiAdd,Composite,InsertSceneChange,DeleteSceneChange}; TagSplit, StripTags, StripTagsKeepNL, ConvertSSAToSRT/SRTToSSA, AssColorString2ABGR/ABGRColor2AssColorString, WideResolveRelativePath/WideIsAbsolutePath, Font2String/String2Font, StringConvert*. FileReaderUnit não foi portado porque só servia aos leitores MKV/MP4 de quadros-chave, substituídos por FFmpeg (plano). SubtitleModel.pas não é usado pelo programa (só por dunit), então não portá-lo não é defeito.

Conferidas cerca de 130 rotinas. Não conferidas: WideStringFind/WideStringCount (busca), URLDecode, PosStream/PosStreamEx, BinarySearch, ReadLineStream, Shell*RegisterExtension, SetRE*/SetCheckedState, GetUserApplicationDataFolder/GetTemporaryFolder (pertencem a outras fatias: busca, web, cenas, preferências, editor); TUndoablePipeTask e TUndoableMultiChangeTask/ProcessMultiChangeSub em detalhe (text pipe e Replace All); o interior de DeleteSubtitles/RestoreSubtitles/SplitSubtitle/MergeSubtitles (fatia de edição).
