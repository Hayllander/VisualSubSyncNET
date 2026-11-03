# Conferência final da migração — 25/10/2026

Pergunta: tudo o que existe no VisualSubSync 1.0.1 (`C:\Projetos\VisualSubSync`) chegou ao
VisualSubSyncNET (`C:\Projetos\VisualSubSync.NET`, 2.0.0-alpha.5)? O Enhanced
(`C:\Projetos\VisualSubSyncEnh`) foi usado como referência de layout e das funções que o usuário decidiu portar.

## Resultado

Não ficou nenhuma função do 1.0.1 sem destino. Cada item tem um equivalente no .NET ou está registrado no
PLANO-DE-MIGRACAO.md como diferença intencional.

| Conferência | Total no 1.0.1 | Resultado |
| --- | --- | --- |
| Rotinas (`procedure`/`function`) das 46 unidades `.pas` | 1502 | 1050 encontradas pelo nome no código ou nos documentos do .NET; 452 revisadas abaixo |
| Janelas (`.dfm`) | 18 | 18 com janela equivalente em `Views/` |
| Ações do `main.dfm` (`TTntAction`) | 113 | 111 com o mesmo nome; `ActionExit` virou `ExitCommand`; `ActionExportToSSA` não tem `OnExecute` nem menu no original |
| Itens dos menus do `main.dfm` (todos os popups) | 148 | 25 separadores; 93 ligados a ações, todas presentes (`EditCut1`, `EditDelete1` e `EditSelectAll1` são ações padrão da VCL, trocadas pelos comandos do WPF); 30 com legenda própria, 29 presentes e "Codecs installation" fora, registrado no plano |
| Chaves de `VisualSubSync.ini` e do projeto (`Read*`/`Write*`) | 85 | 85 lidas e gravadas com o mesmo nome |
| Arquivos de `Release\help`, `web` e `jsplugin` | todos | iguais ao original, menos `help/french.html`, que agora cita `VisualSubSyncNET.exe` |

## As rotinas não encontradas pelo nome

### Unidades trocadas por outra tecnologia (149 rotinas)

| Unidade | Rotinas | Destino no .NET |
| --- | --- | --- |
| `MatroskaHelper.pas` | 48 | leitura de MKV, trocada pelo FFmpeg (FFmpegKeyframeReader) |
| `Renderer.pas` | 43 | reprodução DirectShow, trocada pelo mpv (MpvMediaEngine) |
| `MP4File.pas` | 22 | leitura de MP4, trocada pelo FFmpeg |
| `TntRichEditCustomUndoUnit.pas` | 17 | desfazer do TTntRichEdit da VCL, trocado pelo desfazer do próprio programa |
| `DynamicPageProcessorUnit.pas` | 7 | código morto (só aparece no .dpr) |
| `MiniScrollBarUnit.pas` | 6 | controle VCL, desenhado dentro do WaveformControl |
| `PopupTrackbarUnit.pas` | 4 | controle VCL, trocado por Slider do WPF |
| `LogWriterIntf.pas` | 2 | interface interna de log |

### Acessores, construtores e destrutores (115 rotinas)

São `Get*`/`Set*` de propriedades, `Create`/`Destroy` e eventos de formulário. No .NET viram propriedades e
construtores; o comportamento delas foi conferido junto com a rotina que as usa. Exemplo: `TVSSCoreWrapper.SetCpsTarget`
não existe porque `CpsTarget` é só leitura nos dois programas (`JavaScriptPluginUnit.pas`, linha 190;
`JsObjects.cs`, `VssCoreJs.CpsTarget`).

### Revisadas uma a uma (188 rotinas)

Todas existem no .NET com outro nome ou estão registradas no plano:

- Ações com outro nome: `ActionZoomIn/Out/All` → `ZoomIn/Out/AllCommand`; `ActionShiftStart/Stop Plus/Minus 10/100`
  → `ShiftStart`/`ShiftStop` com o valor; `ActionSetPlaybackRate60..100` → `PlaybackRate`; `ActionText*`
  → `Text*Command`; `ActionCopy/Paste*` → `CopySubtitles`/`PasteSubtitles` e os comandos do editor;
  `ActionOpenProject`, `ActionClose`, `ActionExit`, `MenuItemStart/StopWebServer`, `bttWorkingMode`.
- Eventos ligados por binding: `WAVDisplayer1ViewChange` (campos View), `WAVDisplayer1AutoScrollChange`,
  `tbVolumeChange` (`OnVolumeChanged`), `vtvSubsListPaintText` e `vtvSubsListCompareNodes` (`MainWindow.Columns.cs`).
- Botões OK/Cancel/Browse dos diálogos: `IsCancel`/`IsDefault` do WPF e os handlers `OnBrowse*`.
- Funções utilitárias (`Explode`, `Implode`, `RPos`, `StringConvertPipeTo*`, leitura de streams): trocadas pela
  biblioteca do .NET; a conversão de `|` está em `VoReference.cs` e `MainViewModel.Web.cs`.
- Parser SSA (`TFormatedParser`, `TSSAParser`): `SsaFormat.cs`. Modelo e desfazer (`TSubtitleModel`,
  `TUndoable*`): `SubtitleDocument` e `MainViewModel.Undo.cs`.
- Mudança de cena (`ExtractKeyFramesMKV/MP4`, `LoadSceneChange`): `FFmpegKeyframeReader`, registrado no plano.
- Registradas no plano como fora de propósito: o painel de teste de falha (`panelCrashTestClick`), "List used
  DirectShow filters" (`ListDirectShowfilters1Click`, desabilitado porque o motor não é DirectShow) e "Codecs installation".

Lista completa:

| Unidade | Rotina | Linha |
| --- | --- | --- |
| `AboutFormUnit.pas` | `TAboutForm.TntLabel3Click` | 80 |
| `AboutFormUnit.pas` | `TAboutForm.panelCrashTestClick` | 100 |
| `DelayFormUnit.pas` | `TDelayForm.bttCancelClick` | 74 |
| `ErrorReportFormUnit.pas` | `TErrorReportForm.CreateFont` | 178 |
| `ErrorReportFormUnit.pas` | `TErrorReportForm.pmiFixErrorClick` | 358 |
| `FileReaderUnit.pas` | `TFileReader.Tell` | 64 |
| `FindFormUnit.pas` | `TFindForm.UseRegExp` | 126 |
| `GotoFormUnit.pas` | `TGotoForm.bttGoClick` | 59 |
| `GotoFormUnit.pas` | `TGotoForm.bttCancelClick` | 64 |
| `JavaScriptPluginUnit.pas` | `IsValidSubRange` | 409 |
| `JavaScriptPluginUnit.pas` | `_JS_ScriptLog` | 450 |
| `JavaScriptPluginUnit.pas` | `_JS_SetStatusBarText` | 471 |
| `JavaScriptPluginUnit.pas` | `_JS_LoadScript` | 492 |
| `JavaScriptPluginUnit.pas` | `TBaseJavascriptPlugin.PreInitScript` | 666 |
| `JavaScriptPluginUnit.pas` | `TJavaScriptPlugin.FillParamArray` | 1022 |
| `JavaScriptPluginUnit.pas` | `TSimpleJavascriptWrapper.FillParamArray` | 1415 |
| `JavaScriptPluginUnit.pas` | `TSimpleJavascriptWrapper.FillICPNParamArray` | 1441 |
| `JavaScriptPluginUnit.pas` | `TSceneChangeWrapper.RegisterSceneChange` | 1579 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.MakeWrappedSub` | 1713 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_INDEX_COL_IDX` | 1720 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_START_COL_IDX` | 1725 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_STOP_COL_IDX` | 1730 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_STYLE_COL_IDX` | 1735 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_TEXT_COL_IDX` | 1740 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_VO_COL_IDX` | 1745 |
| `JavaScriptPluginUnit.pas` | `TVSSCoreWrapper.Set_LAST_CORE_COL_IDX` | 1750 |
| `LibHunspellUnit.pas` | `SkipDelimiters` | 179 |
| `LibHunspellUnit.pas` | `ConvertToHunspell` | 302 |
| `LibHunspellUnit.pas` | `ConvertFromHunspell` | 312 |
| `LogWindowFormUnit.pas` | `TLogForm.ListDirectShowfilters1Click` | 73 |
| `MiscToolsUnit.pas` | `StringConvertPipeToSpace` | 302 |
| `MiscToolsUnit.pas` | `StringConvertPipeToCRLF` | 317 |
| `MiscToolsUnit.pas` | `StringConvertPipeToBR` | 352 |
| `MiscToolsUnit.pas` | `TFileVersion.ParseFile` | 438 |
| `MiscToolsUnit.pas` | `RPos` | 712 |
| `MiscToolsUnit.pas` | `Implode` | 766 |
| `MiscToolsUnit.pas` | `ExplodeW` | 779 |
| `MiscToolsUnit.pas` | `Explode` | 803 |
| `MiscToolsUnit.pas` | `WideMakeRelativePath` | 973 |
| `MiscToolsUnit.pas` | `WideResolvePath` | 987 |
| `MiscToolsUnit.pas` | `ConvertNull` | 1284 |
| `MiscToolsUnit.pas` | `SwapColor` | 1291 |
| `MiscToolsUnit.pas` | `SaveToStreamWS` | 1329 |
| `MiscToolsUnit.pas` | `LoadFromStreamWS` | 1339 |
| `MiscToolsUnit.pas` | `SaveToStreamInt` | 1347 |
| `MiscToolsUnit.pas` | `LoadFromStreamInt` | 1352 |
| `MiscToolsUnit.pas` | `MyTTntStringList.LoadFromStream_BOM` | 1432 |
| `PageProcessorUnit.pas` | `TPageProcessor.ProcessField` | 90 |
| `PageProcessorUnit.pas` | `TPageProcessor.CopyAndReplaceField` | 113 |
| `PreferencesFormUnit.pas` | `PreferencesFormInstance` | 350 |
| `PreferencesFormUnit.pas` | `TJSPluginInfo.ClearParam` | 383 |
| `PreferencesFormUnit.pas` | `TConfigObject.ClearJSPluginInfoList` | 558 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.ClearErrorList` | 853 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.bttCancelClick` | 1026 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.ListHotkeysDeletion` | 1171 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.ListPluginParamGetText` | 1215 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.ListPluginParamCreateEditor` | 1248 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.WMEndEditing` | 1257 |
| `PreferencesFormUnit.pas` | `TPropertyEditLink.ProcessMessage` | 1437 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.bttSubListFontClick` | 1456 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.bttSubTextFontClick` | 1468 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.ListPluginParamGetHint` | 1493 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.pmiSelectAllClick` | 1507 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.pmiUnselectAllClick` | 1521 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.chkAssociateExtVSSPRJClick` | 1553 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.chkAssociateExtSRTClick` | 1570 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.chkAssociateExtSSAClick` | 1587 |
| `PreferencesFormUnit.pas` | `TPreferencesForm.chkAssociateExtASSClick` | 1604 |
| `ProjectUnit.pas` | `CheckFileCanBeOpened` | 149 |
| `ProjectUnit.pas` | `TProjectForm.bttCancelClick` | 354 |
| `ProjectUnit.pas` | `TProjectForm.bttBrowseVideoFileClick` | 361 |
| `ProjectUnit.pas` | `TProjectForm.bttBrowseWAVFileClick` | 410 |
| `ProjectUnit.pas` | `TProjectForm.bttBrowseSubtitleFileClick` | 434 |
| `ProjectUnit.pas` | `TProjectForm.bttBrowseProjectFileClick` | 463 |
| `ProjectUnit.pas` | `TProjectForm.bttBrowseVOFileClick` | 476 |
| `ProjectUnit.pas` | `TProjectForm.bttBrowsePeakFileClick` | 634 |
| `SSAParserUnit.pas` | `TFormatedParser.ParseFormatLine` | 164 |
| `SSAParserUnit.pas` | `TFormatedParser.ParseFormatedLine` | 182 |
| `SSAParserUnit.pas` | `TStyleParser.IsSection` | 284 |
| `SSAParserUnit.pas` | `TDialogueParser.IsSection` | 307 |
| `SSAParserUnit.pas` | `TScriptInfoParser.IsSection` | 338 |
| `SSAParserUnit.pas` | `TSSAParser.StyleKeyExists` | 518 |
| `SSAParserUnit.pas` | `TSSAParser.DialogueKeyExists` | 523 |
| `SceneChangeUnit.pas` | `LoadSceneChange` | 34 |
| `SceneChangeUnit.pas` | `IsCuePointOnTrack` | 133 |
| `SceneChangeUnit.pas` | `ExtractKeyFramesMKV` | 152 |
| `SceneChangeUnit.pas` | `ExtractKeyFramesMP4` | 193 |
| `SceneChangeUnit.pas` | `ExtractSceneChange` | 245 |
| `ServerUnit.pas` | `MoreDataOnSocket` | 373 |
| `ServerUnit.pas` | `THTTPRequestThreadProcessor.AddStats` | 493 |
| `ServerUnit.pas` | `MyGetFileSize` | 621 |
| `ServerUnit.pas` | `THTTPRequestThreadProcessor.SendFile` | 643 |
| `ServerUnit.pas` | `WriteStreamLn` | 717 |
| `ServerUnit.pas` | `THTTPRequestThreadProcessor.SendStream` | 768 |
| `SpellCheckFormUnit.pas` | `TSpellCheckForm.bttCancelClick` | 92 |
| `StyleFormUnit.pas` | `TStyleForm.bttPrimaryColorClick` | 340 |
| `StyleFormUnit.pas` | `TStyleForm.ClearList` | 550 |
| `StyleFormUnit.pas` | `TStyleForm.ClearAll` | 817 |
| `SubtitleModel.pas` | `SubtitleTimeDComparator` | 150 |
| `SubtitleModel.pas` | `SubtitleTimeComparator` | 155 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalCreateSubtitle` | 348 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalInsertSubtitle` | 354 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalDeleteSubtitle` | 360 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalSetStart` | 375 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalSetStop` | 403 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalSetText` | 431 |
| `SubtitleModel.pas` | `TSubtitleModel.InternalReindex` | 436 |
| `SubtitleModel.pas` | `TSubtitleModel.CreateSubtitle` | 447 |
| `SubtitleModel.pas` | `TSubtitleModel.BeginTransaction` | 476 |
| `SubtitleModel.pas` | `TSubtitleModel.EndTransaction` | 483 |
| `SubtitleModel.pas` | `_myBinarySearchInWith` | 576 |
| `SubtitleModel.pas` | `myBinarySearch` | 621 |
| `SubtitleModel.pas` | `TSubtitleModel.FindInsertPosition` | 628 |
| `SubtitleModel.pas` | `TSubtitleModel.FindInsertPosition` | 641 |
| `SuggestionFormUnit.pas` | `TSuggestionForm.vtvSuggestionsLstDrawNode` | 158 |
| `TranslateFormUnit.pas` | `TTranslateForm.bttCancelClick` | 185 |
| `TranslateFormUnit.pas` | `RadioGroupButton` | 228 |
| `UndoableSubTaskUnit.pas` | `TUndoableTaskIndexed.AddSubtitleIndex` | 278 |
| `UndoableSubTaskUnit.pas` | `TUndoableMultiChangeTask.AddData` | 747 |
| `WAVDisplayerUnit.pas` | `TRange.ClearSubTimes` | 629 |
| `WAVDisplayerUnit.pas` | `TRange.DelSubTimeAt` | 636 |
| `WAVDisplayerUnit.pas` | `TRangeFactory.CreateRangeSS` | 823 |
| `WAVDisplayerUnit.pas` | `TRangeList.AddAndReturnSibling` | 865 |
| `WAVDisplayerUnit.pas` | `TRangeList.AddAtEnd` | 878 |
| `WAVDisplayerUnit.pas` | `CompareRangesFullsort` | 911 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.WM_EraseBKGND` | 1171 |
| `WAVDisplayerUnit.pas` | `NoKeyModifierIn` | 3374 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.DeleteRangeAt` | 3483 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.DeleteRangeAtIdx` | 3495 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.RedirectedMousewheel` | 3601 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.RestoreAutoScroll` | 3818 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.Resize` | 3976 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.ClearRangeList` | 4022 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.ClearRangeListVO` | 4032 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.ClearPeakData` | 4073 |
| `WAVDisplayerUnit.pas` | `TWAVDisplayer.IsPeakDataLoaded` | 4084 |
| `WAVExtractFormUnit.pas` | `TExtractWAVForm.CancelAndClose` | 108 |
| `WAVExtractFormUnit.pas` | `TExtractWAVForm.Timer1Timer` | 180 |
| `WAVExtractFormUnit.pas` | `TExtractWAVForm.rbOnlyPeakClick` | 216 |
| `WAVFileUnit.pas` | `TWAVFile.SeekMs` | 238 |
| `WAVFileUnit.pas` | `TWAVRenderer.IncreaseFreeBlockCount` | 302 |
| `WAVFileUnit.pas` | `TWAVRenderer_CallBack` | 311 |
| `WAVFileUnit.pas` | `TWAVRenderer.Deinit` | 352 |
| `main.pas` | `TMainForm.WAVDisplayer1ViewChange` | 1754 |
| `main.pas` | `TMainForm.WAVDisplayer1AutoScrollChange` | 1861 |
| `main.pas` | `TMainForm.ActionZoomInExecute` | 3136 |
| `main.pas` | `TMainForm.ActionZoomOutExecute` | 3143 |
| `main.pas` | `TMainForm.ActionZoomAllExecute` | 3157 |
| `main.pas` | `TMainForm.ActionExitExecute` | 3211 |
| `main.pas` | `TMainForm.ActionOpenProjectExecute` | 3302 |
| `main.pas` | `TMainForm.ClearWAVSelection` | 4214 |
| `main.pas` | `TMainForm.vtvSubsListCompareNodes` | 4419 |
| `main.pas` | `TMainForm.MenuItemStartWebServerClick` | 4590 |
| `main.pas` | `TMainForm.MenuItemStopWebServerClick` | 4597 |
| `main.pas` | `TMainForm.ActionCloseExecute` | 4828 |
| `main.pas` | `TMainForm.bttWorkingModeClick` | 5059 |
| `main.pas` | `TMainForm.ActionSetPlaybackRate70Execute` | 5269 |
| `main.pas` | `TMainForm.ActionSetPlaybackRate60Execute` | 5277 |
| `main.pas` | `TMainForm.ActionSetPlaybackRate80Execute` | 5285 |
| `main.pas` | `TMainForm.ActionSetPlaybackRate90Execute` | 5293 |
| `main.pas` | `TMainForm.ActionSetPlaybackRate100Execute` | 5301 |
| `main.pas` | `TMainForm.ActionShiftStartPlus100Execute` | 5415 |
| `main.pas` | `TMainForm.ActionShiftStartMinus100Execute` | 5422 |
| `main.pas` | `TMainForm.ActionShiftStopPlus100Execute` | 5429 |
| `main.pas` | `TMainForm.ActionShiftStopMinus100Execute` | 5436 |
| `main.pas` | `TMainForm.ActionShiftStartPlus10Execute` | 5443 |
| `main.pas` | `TMainForm.ActionShiftStartMinus10Execute` | 5450 |
| `main.pas` | `TMainForm.ActionShiftStopPlus10Execute` | 5457 |
| `main.pas` | `TMainForm.ActionShiftStopMinus10Execute` | 5464 |
| `main.pas` | `TMainForm.OnSubtitleRangeJSWrapperChangeStop` | 5976 |
| `main.pas` | `TMainForm.OnSubtitleRangeJSWrapperChangeText` | 6000 |
| `main.pas` | `TMainForm.PanelVideoDblClick` | 7246 |
| `main.pas` | `TMainForm.tbVolumeChange` | 7618 |
| `main.pas` | `TMainForm.CurrentProjectSetDirty` | 7702 |
| `main.pas` | `TMainForm.WAVDisplayer1RangeStopDblClick` | 7826 |
| `main.pas` | `TMainForm.SaveSubtitlesAsTXT` | 7940 |
| `main.pas` | `TMainForm.SaveSubtitlesAsCSV` | 7977 |
| `main.pas` | `TMainForm.ActionTextItalicExecute` | 8639 |
| `main.pas` | `TMainForm.ActionTextBoldExecute` | 8646 |
| `main.pas` | `TMainForm.ActionTextUnderlineExecute` | 8653 |
| `main.pas` | `TMainForm.ActionTextColorExecute` | 8660 |
| `main.pas` | `TMainForm.ActionTextSizeExecute` | 8667 |
| `main.pas` | `TMainForm.vtvSubsListPaintText` | 8706 |
| `main.pas` | `TMainForm.ActionCopyExecute` | 9124 |
| `main.pas` | `TMainForm.ActionCopyUpdate` | 9139 |
| `main.pas` | `TMainForm.OnLoadDictionnary` | 9240 |
| `main.pas` | `TMainForm.ActionSpellCheckUpdate` | 9515 |
| `main.pas` | `TMainForm.ActionPasteUpdate` | 9567 |

## Limites desta conferência

- Encontrar o nome prova que a rotina tem um lugar no .NET, não que o comportamento é igual. O comportamento foi
  conferido nas auditorias de 21/10, 23/10 (`docs/auditoria-2026-10-23/`) e 25/10 (`comparacao-2026-10-25.md`),
  com as diferenças intencionais no PLANO-DE-MIGRACAO.md.
- `ThirdParty`, `dunit` e `WavWriter` (bibliotecas e testes do Delphi) ficaram fora da contagem.
- Funções que só existem no Enhanced continuam desabilitadas, exceto as que o usuário decidiu portar (Check for
  updates e "Subtitles video"), registradas no plano.
