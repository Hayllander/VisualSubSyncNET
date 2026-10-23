namespace VisualSubSync.App.Services;

/// <summary>Diálogos do sistema, isolados para que os view models não dependam de janelas.</summary>
public interface IDialogService
{
    /// <summary>TntOpenDialog1: sem título próprio, como no original.</summary>
    /// <param name="fileName">Nome inicial mostrado no diálogo (legado: <c>FileName := ...</c>).</param>
    /// <param name="filterIndex">Índice do filtro escolhido (1 = primeiro, como no TOpenDialog).</param>
    string? OpenFile(string filter, string fileName = "", int filterIndex = 1);

    /// <summary>Como <see cref="OpenFile"/>, devolvendo também o índice do filtro escolhido.</summary>
    (string Path, int FilterIndex)? OpenFileWithFilter(string filter, int filterIndex = 1);

    /// <summary>ActionZoomVerticalExecute: TVerticalScalingForm, criada uma vez, alterna visível/escondida.</summary>
    void ToggleVerticalScaling(ViewModels.MainViewModel viewModel);

    /// <summary>ActionShowSilentZonesExecute: mostra o TSilentZoneForm e atualiza a lista.</summary>
    void ShowSilentZones(ViewModels.MainViewModel viewModel);

    /// <summary>ActionShowSuggestionsExecute: SuggestionForm.Visible := not SuggestionForm.Visible.</summary>
    void ToggleSuggestions(ViewModels.MainViewModel viewModel);

    /// <summary>ErrorReportForm.Visible := True (ActionCheckErrors com erros encontrados).</summary>
    void ShowErrorReport(ViewModels.MainViewModel viewModel);

    /// <summary>ActionShowErrorReportExecute: ErrorReportForm.Visible := not ErrorReportForm.Visible.</summary>
    void ToggleErrorReport(ViewModels.MainViewModel viewModel);

    /// <summary>ErrorReportForm.GoToNextError.</summary>
    void GoToNextError();

    /// <summary>ErrorReportForm.FixSelectedErrors.</summary>
    void FixSelectedErrors();

    /// <summary>ActionShowHideLogsExecute.</summary>
    void ToggleLog();

    /// <summary>LogForm.LogMsg (mostra a janela) ou SilentLogMsg.</summary>
    void LogMessage(string message, bool silent);

    /// <summary>LogForm.Clear.</summary>
    void ClearLog();

    /// <summary>SaveFormPosition das janelas auxiliares, antes de gravar o VisualSubSync.ini.</summary>
    void StoreWindowPlacements();

    /// <summary>
    /// ShowPreferences: mostra a janela na aba pedida (ou na última aberta) e devolve verdadeiro com OK,
    /// já com as preferências gravadas em <paramref name="settings"/>.
    /// </summary>
    bool ShowPreferences(Core.Settings.VssSettings settings, string? tabName);

    /// <summary>StyleFormInstance.ShowModal: Apply troca o nome nas legendas e marca o projeto.</summary>
    /// <summary>TSpellCheckForm.ShowModal; <paramref name="focusSubtitle"/> é o FocusNode de cada erro.</summary>
    void ShowSpellCheck(Core.Spelling.SpellCheckSession session, Core.Settings.FontSpec font, Action<int> focusSubtitle);

    void ShowStyles(Core.Subtitles.StyleEditor editor, Action<string, string> renameStyle, Action setDirty);

    /// <summary>TntOpenDialog1 com ofAllowMultiSelect: os arquivos escolhidos (vazio quando cancelado).</summary>
    IReadOnlyList<string> OpenFiles(string filter);

    /// <summary>SelectDirectory(caption, '', pasta): null quando cancelado.</summary>
    string? SelectFolder(string caption);

    /// <summary>TCursorManager.Create(crHourGlass): ampulheta até o Dispose.</summary>
    IDisposable WaitCursor();

    /// <summary>
    /// WAVDisplayer1OnPeakFileCreation: a janela "Creating peak file..." fica aberta, com a principal
    /// desabilitada, enquanto <paramref name="work"/> cria os picos fora da thread da interface.
    /// </summary>
    void RunPeakCreation(Action<IProgress<int>> work);

    /// <summary>ShellExecute(..., 'explore', pasta): abre a pasta no Explorer.</summary>
    void ExploreFolder(string folder);

    /// <summary>Devolve o caminho e o índice do filtro escolhido (1 = primeiro, como no TSaveDialog).</summary>
    /// <param name="defaultExt">DefaultExt: acrescentada quando o nome digitado não tem extensão.</param>
    (string Path, int FilterIndex)? SaveFile(string filter, string fileName, int filterIndex, string defaultExt = "");

    /// <summary>TProjectForm (ProjectUnit.pas) em qualquer dos modos; null quando cancelado.</summary>
    ProjectFormData? ShowProjectForm(ProjectFormData data);

    /// <summary>TExtractWAVForm (WAVExtractFormUnit.pas); null quando a extração não foi concluída.</summary>
    ExtractWavResult? ShowExtractWav(ExtractWavRequest request);

    /// <summary>TDelayForm (DelayFormUnit.pas); null quando cancelado.</summary>
    DelayResult? ShowDelay(DelayRequest request);

    /// <summary>FindForm.Showing.</summary>
    bool IsFindShowing { get; }

    /// <summary>FindForm.Show: janela única, sem modo, sempre por cima; guarda histórico e opções.</summary>
    void ShowFind(ViewModels.MainViewModel viewModel);

    /// <summary>TGotoForm (GotoFormUnit.pas); null quando cancelado.</summary>
    GotoResult? ShowGoto();

    /// <summary>TAboutForm.</summary>
    void ShowAbout(string version);

    /// <summary>TTranslateForm.</summary>
    TranslateResult? ShowTranslate(string targetFile, bool hasVo);

    /// <summary>MB_YESNO com ícone de aviso: false quando o usuário escolhe "No".</summary>
    bool Confirm(string caption, string message);

    void ShowError(string message);

    void ShowWarning(string message);

    /// <summary>MB_YESNOCANCEL com ícone de aviso: true = Yes, false = No, null = Cancel.</summary>
    bool? AskYesNoCancel(string message);
}
