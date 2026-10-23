using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Spelling;
using VisualSubSync.Core.Settings;

namespace VisualSubSync.App.ViewModels;

/// <summary>O menu Edit > Spell check, o carregamento dos dicionários e a janela Spell check de main.pas.</summary>
public sealed partial class MainViewModel
{
    private bool _spellCheckerStarted;
    private string _personalDictionaryPath = string.Empty;

    /// <summary>g_DictPath: a pasta dict ao lado do executável.</summary>
    public static string DictPath => Path.Combine(AppContext.BaseDirectory, "dict");

    /// <summary>FSpellChecker.</summary>
    public HunspellChecker SpellChecker { get; } = new();

    /// <summary>MenuItemLiveSpellcheck.Checked (marcado ao abrir, não é gravado).</summary>
    [ObservableProperty]
    private bool _isLiveSpellCheck = true;

    /// <summary>Enquanto um dicionário carrega, os itens dos dicionários ficam desabilitados.</summary>
    [ObservableProperty]
    private bool _isDictionaryLoading;

    /// <summary>O item de dicionário marcado (o último pedido), ou -1.</summary>
    [ObservableProperty]
    private int _checkedDictionaryIndex = -1;

    /// <summary>TagHighlight: o editor deve refazer as cores e os sublinhados.</summary>
    public event EventHandler? SpellingChanged;

    /// <summary>FormCreate: o dicionário pessoal e a lista de dicionários da pasta dict.</summary>
    public void StartSpellChecker()
    {
        if (_spellCheckerStarted)
        {
            return;
        }

        _spellCheckerStarted = true;
        string local = Path.Combine(DictPath, "perso.dic");
        _personalDictionaryPath = StoragePaths.ResolveDataFile(local, Path.Combine("dict", "perso.dic"));
        SpellChecker.LoadPersonalDict(File.Exists(_personalDictionaryPath) ? _personalDictionaryPath : local);
        SpellChecker.FindDict(DictPath);
    }

    /// <summary>FormDestroy: o dicionário pessoal volta para perso.dic.</summary>
    public void SavePersonalDictionary()
    {
        if (_spellCheckerStarted && _personalDictionaryPath.Length > 0)
        {
            if (!SpellChecker.SavePersonalDict(_personalDictionaryPath))
            {
                _dialogs.LogMessage($"Could not save personal dictionary: {_personalDictionaryPath}", silent: false);
            }
        }
    }

    /// <summary>LoadAfterParamsSettings: sem projeto (ou projeto sem dicionário), o dicionário padrão.</summary>
    private void LoadAfterParamsSettings()
    {
        if (Project is null || Project.Dictionary.Length == 0)
        {
            int idx = SpellChecker.GetDictIdx(Settings.Dictionary);
            if (idx != -1)
            {
                LoadDict(idx);
            }
        }
    }

    /// <summary>OnSpellcheckLanguageMenuItemClick.</summary>
    [RelayCommand]
    private void LoadDictionary(int index) => LoadDict(index);

    /// <summary>
    /// LoadDict: tira o dicionário atual, carrega o novo em segundo plano e, ao terminar, faz dele o
    /// padrão e o grava no projeto aberto.
    /// </summary>
    private async void LoadDict(int idx)
    {
        int oldIdx = SpellChecker.GetDictIdx(SpellChecker.CurrentDictName);
        if (idx == oldIdx || IsDictionaryLoading)
        {
            return;
        }

        SpellChecker.Cleanup();
        SpellingChanged?.Invoke(this, EventArgs.Empty);
        IsDictionaryLoading = true;
        CheckedDictionaryIndex = idx;
        SpellCheckCommand.NotifyCanExecuteChanged();
        try
        {
            await Task.Run(() => SpellChecker.Initialize(idx)).ConfigureAwait(true);
        }
        finally
        {
            IsDictionaryLoading = false;
        }

        // OnLoadDictionnaryTerminate.
        SpellingChanged?.Invoke(this, EventArgs.Empty);
        SpellCheckCommand.NotifyCanExecuteChanged();
        Settings.Dictionary = SpellChecker.CurrentDictName;
        if (Project is { } project && project.FileName.Length > 0 && project.Dictionary != Settings.Dictionary)
        {
            project.Dictionary = Settings.Dictionary;
            SaveProject(project, silent: true);
        }
    }

    /// <summary>ActionLiveSpellCheckExecute.</summary>
    [RelayCommand]
    private void ToggleLiveSpellCheck()
    {
        IsLiveSpellCheck = !IsLiveSpellCheck;
        SpellingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>MenuItemGetMoreDictionariesClick.</summary>
    [RelayCommand]
    private static void GetMoreDictionaries()
    {
        try
        {
            Process.Start(new ProcessStartInfo("http://wiki.services.openoffice.org/wiki/Dictionaries") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning($"Could not open the dictionaries page: {ex.Message}");
        }
    }

    /// <summary>A verificação ao vivo está ligada e há dicionário carregado.</summary>
    public bool IsLiveSpellCheckActive => IsLiveSpellCheck && SpellChecker.IsInitialized;

    /// <summary>OnSpellcheckAddToDictionaryMenuItemClick.</summary>
    public void AddToDictionary(string word)
    {
        SpellChecker.Add(word);
        SpellingChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>OnSpellcheckIgnoreMenuItemClick.</summary>
    public void IgnoreWord(string word)
    {
        SpellChecker.Ignore(word);
        SpellingChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSpellCheck => SpellChecker.IsInitialized && !IsDictionaryLoading;

    /// <summary>
    /// ActionSpellCheckExecute: a janela troca o texto das legendas na hora; as trocas viram uma
    /// tarefa de desfazer só.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSpellCheck))]
    private void SpellCheck()
    {
        var selected = _selectedItems.Select(Subtitles.IndexOf).Where(i => i >= 0).ToList();
        var session = new SpellCheckSession(_document.Items, selected, SpellChecker);
        _dialogs.ShowSpellCheck(session, Settings.SubTextFont, FocusSubtitle);
        if (session.Changes.Count == 0)
        {
            return;
        }

        var changes = session.Changes.Select(c => new TextChange(c.SubtitleIndex, c.OldText, c.NewText)).ToList();
        _undoStack.Push(new MultiChangeTask(this, changes));
        IsDirty = true;
        foreach (int index in changes.Select(c => c.Index).Distinct())
        {
            Subtitles[index].RefreshAll();
        }

        RefreshRanges();
        SpellingChanged?.Invoke(this, EventArgs.Empty);
    }
}
