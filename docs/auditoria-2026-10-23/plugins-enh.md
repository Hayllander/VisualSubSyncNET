# Auditoria de paridade: plugins, servidor web, corretor, mídia e layout Enhanced (fatia plugins-enh)

Bases: 1.0.1 em /mnt/project-files/VisualSubSync; Enhanced 1.2.29 (docs/enhanced/TMAINFORM.txt, MENU.txt), com consulta às fontes 1.2.33 (enh-src); árvore .NET atualizada. Os itens A01-A23 de docs/AUDITORIA-PARIDADE-2026-10-21.md e o que já está registrado em docs/PLANO-DE-MIGRACAO.md ficaram de fora.

## Parte 1: plugins, web, Hunspell e renderizador

### Atribuição fracionária a Start/Stop arredonda em vez de truncar
- Gravidade: média
- Original: ThirdParty/jsbridge/jsintf.pas:2429-2437 (SetProperty → SetPropValue) e jsintf_bridge.pas:55 (IntfBridge_SetProperty)
- .NET: src/VisualSubSync.Core/Plugins/JsObjects.cs:236-243 (JsConvert.ToInt32, `Math.Floor(d + 0.5)`)
- Diferença: no Delphi o valor JS chega como Variant double e o SetPropValue de uma propriedade Integer trunca. No .NET o valor é arredondado. Scripts que calculam `(a+b)/2` têm resultado 1 ms diferente quando a soma é ímpar. O caso concreto é overlapping.js no modo 1 com sobreposição ímpar. O truncamento vem do RTL do D7 (o RangedValue faz Trunc); confirmar no binário 1.0.1 seria o ideal.
- Correção sugerida: usar `(int)Math.Truncate(d)` nas propriedades graváveis (Start/Stop) para seguir o Delphi.

### Nomes de membros JS diferenciam maiúsculas e minúsculas
- Gravidade: baixa
- Original: ThirdParty/jsbridge/jsintf.pas:2159/2303/2429 (GetPropInfo, que não diferencia maiúsculas de minúsculas)
- .NET: src/VisualSubSync.Core/Plugins/JsObjects.cs (SubtitleJs/VssCoreJs expostos ao Jint com nomes exatos)
- Diferença: no Delphi, `sub.index` ou `sub.start` resolvem para Index/Start. No Jint, dão undefined. Nenhum plugin 1.0.1 depende disso. O Enhanced itasa_typos_kickaha.js usa `CurrentSub.index`.
- Correção sugerida: registrar os membros sem diferenciar maiúsculas de minúsculas (por exemplo, um TypeResolver com StringComparer.OrdinalIgnoreCase).

### FatalError é zerado a cada chamada
- Gravidade: baixa
- Original: JavaScriptPluginUnit.pas:617-638 (OnJsError; o flag só volta a False num warning)
- .NET: src/VisualSubSync.Core/Plugins/JsPlugin.cs:80 e :143 (zerado em Call e em LoadScript)
- Diferença: no original, uma exceção num plugin deixa FatalError ligado nas chamadas seguintes. O laço de checagem (main.pas, verificação de erros) então para naquele plugin logo após a primeira legenda. No .NET, o plugin continua sendo chamado para todas as legendas e o log recebe um erro por legenda.
- Correção sugerida: não zerar FatalError em Call; zerar só onde o original zera (warning) ou ao recarregar o plugin.

### Limites de execução que o original não tem
- Gravidade: baixa
- Original: JavaScriptPluginUnit.pas (TJSEngine sem limite de tempo nem de recursão)
- .NET: src/VisualSubSync.Core/Plugins/JsPlugin.cs:25 (`LimitRecursion(1000).TimeoutInterval(30 s)`)
- Diferença: um plugin longo, como uma ação geral sobre milhares de legendas, é abortado após 30 s no .NET e conclui no original.
- Correção sugerida: remover o timeout ou torná-lo bem maior. Se ele ficar, registrar no plano.

### Contagem de argumentos e GetSelectedCount
- Gravidade: baixa
- Original: ThirdParty/jsbridge/jsbridge_pvt.pas:223-225 ('Not enough parameters' / 'Too many parameters'); JavaScriptPluginUnit.pas:1790-1810 (GetSelectedCount sem SetMethodInfo)
- .NET: src/VisualSubSync.Core/Plugins/JsObjects.cs:202
- Diferença: o original gera um erro JS quando o número de argumentos difere do declarado; o Jint aceita qualquer número. GetSelectedCount não é exposto por SetMethodInfo em 1.0.1 e, por isso, não devolve o número. O .NET devolve a contagem. Nenhum plugin distribuído depende disso.
- Correção sugerida: anotar no plano como divergência aceita, ou validar a aridade nos métodos de VSSCore/SceneChange.

### Formato da versão no servidor web e nas páginas
- Gravidade: baixa
- Original: ServerUnit.pas:700 (`'Server: VisualSubSync/'+g_ApplicationVersion.VersionString`); MiscToolsUnit TFileVersion ("%d.%d.%d.%d")
- .NET: src/VisualSubSync.Core/Web/HttpServer.cs:637; src/VisualSubSync.App/ViewModels/MainViewModel.Web.cs:105 (ApplicationVersion = AssemblyInformationalVersion, "2.0.0-alpha.N")
- Diferença: o cabeçalho Server, o campo `$|version|$` dos templates e o relatório de erro mostram uma string semântica em vez de x.y.z.w.
- Correção sugerida: usar a FileVersion (4 números) nesses três pontos.

### Curva de volume
- Gravidade: média
- Original: Renderer.pas:1003-1015 (TDShowRenderer.SetVolume: `put_Volume(Round(1000*Log2(Vol/100)))`, em centésimos de dB, e -10000 para 0)
- .NET: src/VisualSubSync.App/ViewModels/MainViewModel.cs:1232/1376 → src/VisualSubSync.Media/Mpv/MpvMediaEngine.cs:134-140 (propriedade `volume` do mpv, que segue uma curva cúbica)
- Diferença: a mesma posição do tbVolume dá volumes diferentes. Em 50%, o original aplica -10 dB e o mpv cerca de -18 dB. Volumes baixos ficam bem mais silenciosos no .NET.
- Correção sugerida: converter o valor do controle em ganho como o original (dB = 10·log2(v/100)) e passar ao mpv o percentual equivalente, `100·10^(dB/60)`, porque o mpv aplica (vol/100)³.

### Fim de trecho quando a posição fica antes do início
- Gravidade: baixa
- Original: Renderer.pas:1235-1282 (TWaitCompletionThread.Execute: termina ou repete também quando `CurrentPos < FRenderer.StartTime`)
- .NET: src/VisualSubSync.Media/Mpv/MpvMediaEngine.cs:372-395 (CheckRangeEnd só testa o fim)
- Diferença: no original, quando o início do trecho em reprodução é movido para depois da posição atual (UpdatePlayRange), o trecho termina ou volta ao início. No .NET, a reprodução segue até o Stop.
- Correção sugerida: em CheckRangeEnd, tratar `_positionMs < start` como fim de trecho.

Sem outras diferenças confirmadas em PageProcessorUnit (variáveis `$|...|$`, blocos TMPL, URLDecode, CRLF→BR), ServerUnit (fora as já registradas e o endurecimento de cabeçalhos) e LibHunspellUnit. Analyze, Stem e Remove do Hunspell não foram portados, mas o original também não os usa. DynamicPageProcessorUnit só aparece no .dpr (código morto), e não portá-lo está correto.

## Parte 2: layout da janela principal (Enhanced) × MainWindow.xaml

O menu principal (MainWindow.xaml:128-435) confere com MENU.txt nos itens, na ordem, nos ícones il_NN, nos atalhos e nos itens Enhanced desabilitados. A barra de controles confere: posições e tamanhos dos botões (bttPlay…bttShowProjectProperties), tbVolume, Panel1 (auto-scroll), plCursorPos, PanelTimeSelView, bttStyles/cbStyles e PageQuickSmartSilentZonePanels (abas desabilitadas). O PanelBottom confere: MemoLinesCounter 35 px à esquerda, MemoSubtitleVO 400 px à direita e SplitterSubtitleVO 4 px. Também conferem SubListPopupMenu, MemoTextPipePopupMenu e WAVDisplayPopupMenu, este último com Create from VO e Wrap around Scenechange desabilitados. As abas de dicionário online do Enhanced estão com TabVisible = False e não aparecem no layout.

### MemoSubPopupMenu sem os ícones do Enhanced
- Gravidade: baixa
- Original: docs/enhanced/TMAINFORM.txt:2776-2840 (MemoSubPopupMenu, `Images = ImageList1`; Undo 31, Cut 28, Copy 29, Paste 30, Delete 32, Italic 62, Bold 58, Underline 59, Color 60, Size 61)
- .NET: src/VisualSubSync.App/Views/MainWindow.xaml:863-880
- Diferença: no Enhanced o menu de contexto do editor tem imagens. No .NET nenhum item tem MenuItem.Icon, embora os PNGs il_28…il_32 e il_58…il_62 já estejam em Assets/Menu. Select All e os demais itens não têm imagem nem no Enhanced. O 1.0.1 não tinha Images nesse menu, mas o comentário do xaml diz seguir o layout do Enhanced.
- Correção sugerida: acrescentar `<MenuItem.Icon><Image Source="/Assets/Menu/il_NN.png"/></MenuItem.Icon>` com os índices acima.

### MemoSubtitleVOPopupMenu ausente
- Gravidade: baixa
- Original: docs/enhanced/TMAINFORM.txt:1063 (`PopupMenu = MemoSubtitleVOPopupMenu`) e :2984 (pmiMemoSubtitleCopyVOToSubtitleText, 'Copy VO to subtitlle text')
- .NET: src/VisualSubSync.App/Views/MainWindow.xaml:838-841 (RichTextBox MemoSubtitleVO sem ContextMenu): não portado
- Diferença: no Enhanced o clique direito no painel VO abre um menu com um único item. No .NET aparece o menu padrão do RichTextBox, ou nenhum, porque o controle é só leitura.
- Correção sugerida: adicionar um ContextMenu com o item 'Copy VO to subtitlle text', desabilitado como função só do Enhanced, mantendo a grafia do original.

## Parte 3: plugins JS do Enhanced e chaves do ini

Conjunto distribuído: o .NET (src/VisualSubSync.App/jsplugin) traz exatamente os 12 arquivos do 1.0.1, idênticos byte a byte. O Enhanced acrescenta common/itasa.js, duration.js, speed.js, greek_typo.js, itasa_*.js, un-closed_tags.js e general/action_itasa.js e action_nixxo.js. Ele não traz french_typo, scene_change, too_long/too_short_display_time nem action_split_at_cursor. Seu general_plugin.js acrescenta as colunas CPS e Duration, e seu tools.js foi alterado. Isso é conteúdo do Enhanced e não é defeito.

Execução de todos os plugins Enhanced no Core .NET (harness em uma cópia de trabalho): todos carregam, o general plugin dá 3 colunas extras (RS, CPS, Duration) e 7 ações JS são registradas.

APIs do VSSCore só do Enhanced, que o motor .NET não oferece: DeleteSubtitle, GetPluginParamValue, SetPluginParamValue, GetCursorPosition, SetSubColor, ResetSubColor, SetSubIcon, Enable/DisableJavascriptItemMenu e InsertBreakBefore/AfterJavascriptMenuItem. Só duas são usadas pelos plugins Enhanced:
- itasa_typos_kickaha.js, FixError (VSSCore.DeleteSubtitle): "TypeError: Property 'DeleteSubtitle' of object is not a function … line 33".
- action_itasa.js, split_line (VSSCore.GetPluginParamValue): a falha é engolida por try/catch e o plugin volta aos valores padrão.

Ambas são funções só do Enhanced, desabilitadas por regra, e não entram como defeito. Também conta aqui o acesso `CurrentSub.index` (minúsculo), coberto no item de maiúsculas e minúsculas da parte 1.

Chaves do VisualSubSync.ini padrão do Enhanced:
- Que existem no 1.0.1 e o .NET não lê: nenhuma. Todas são lidas e gravadas, inclusive [General] ColumnsPosition, que entrou nesta atualização.
- Só do Enhanced, não lidas e preservadas na gravação (o IniDocument mantém as chaves desconhecidas):
  - [General]: Dictionary (com um n; o 1.0.1 e o .NET usam Dictionnary, VssSettings.cs:198/389), VisibleExtraColumnsWidth.
  - [Misc]: DictionaryOnLine*, OnLineSearchDict1-5, WordReferenceLanguage, LinkDictionaries, UseInternalFilters, DoNotUseInternalFiltersOnCreateProject, UseDefault/AlternativeCodec*, UseDefault/AlternativeInternalCodec, LAV*, LavV*, LavPreferredLanguages, EnableLavAudioMixing, Vrm*, PreferLegacy/Vrm7/Vrm9VideoRenderer, UseReclockAudioRenderer, ForceNoDesktopComposition, ForceRegisteredCodecs, WavColors*, SilentZone*, ModString, CustomText, UTF8AsDefault, KeepDurationIntact, AccentsAssistant, DefaultPath, ForceDefaultPath, LastUsedPath, CheckUpdates, Flipped*, DoubledSubResolution, ForceStopOnPausing, SpaceKeyMoveAfterSmartButtonIsUsed, AutoPlaySmartButtonIsUsed.
  - [Fonts]: SubVideo, Outline*, Shadow*, Transparency*.
  - [WAVDisplay]: DisableVOShowTextInWAVDisplay, ShowSpecialTagSubs, SwapSubVO, EnableShowVideoForNewProject, EnableDetachedVideoForNewProject.
  - [Windows]: DiffSubsForm_*.
  - As seções inteiras [DesynchTools] e [ItasaUser].
- [Hotkeys]: toda ação do Enhanced existe na HotkeyTable do .NET (127 entradas). As entradas JSAction_* são dinâmicas. JSAction_VO e JSAction_FPS_* vêm de plugins Enhanced que não são distribuídos.

---
Verificação: 9 diferenças (0 alta, 2 média, 7 baixa). Rotinas verificadas: 64.
- JavaScriptPluginUnit.pas (todas as classes: TBaseJavascriptPlugin, TJavaScriptPlugin, TGeneralJSPlugin, wrappers Subtitle/SceneChange/VSSCore, OnJsError, LoadScript, CallFunction, GetParam/SetParam, FillParamList, NotifySubtitleModification/Selection, HasError/FixError/GetColumn*).
- jsbridge: SetProperty, GetProperty e a verificação de aridade.
- ServerUnit.pas (TServerThread: leitura de requisição, cabeçalhos, envio de arquivo, códigos de erro).
- PageProcessorUnit.pas (TPageProcessor, TMPL, GetFieldValue).
- LibHunspellUnit.pas (Load/Unload, Spell, Suggest, Add, dicionário pessoal).
- Renderer.pas (TDShowRenderer Play/Pause/Stop/SetVolume/GetPosition/ShowImageAt, TWaitCompletionThread).
- main.pas: InitGeneralJSPlugin, verificação de erros, ActionFixErrorMain, OnJavascriptAction, UpdateAfterJSChange, RegisterJavascriptAction, getters do motor, StartStopServer, UpdateVolume.
- Layout: menu principal, 6 popups, barra de controles, PanelBottom.
- Os 30 plugins Enhanced e as chaves do ini.
Não verificadas: DynamicPageProcessorUnit.pas (código morto, só no .dpr); a curva exata do put_Volume, que depende do driver DirectShow (o item usa a fórmula do código); as posições em pixels de controles criados em tempo de execução, como o SubListHeaderPopupMenu, que é montado em MainWindow.Columns.cs e só foi conferido pelos itens.
