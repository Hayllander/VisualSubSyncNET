using System.IO;
using System.Windows;
using System.Windows.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.App.Views;

/// <summary>TTranslateForm: o que traduzir, o texto das legendas novas e o arquivo de destino.</summary>
public partial class TranslateWindow : Window
{
    private readonly IDialogService _dialogs;
    private bool _hasBrowsedForFile;

    public TranslateWindow(IDialogService dialogs, string targetFile, bool hasVo)
    {
        _dialogs = dialogs;
        InitializeComponent();

        // FormCreate: tstAll e tttEmpty; SetTargetFile e SetHasVO.
        rbEmpty.IsChecked = true;
        edTargetFile.Text = targetFile;
        rbMissingOnly.IsEnabled = hasVo;
        (hasVo ? rbMissingOnly : rbAll).IsChecked = true;
        UpdateEnabled();
    }

    public TranslateResult? Result { get; private set; }

    private bool MissingOnly => rbMissingOnly.IsChecked == true;

    private TranslateTextType TextType =>
        rbCopyOriginal.IsChecked == true ? TranslateTextType.CopyOriginal
        : rbCopyTagsOnly.IsChecked == true ? TranslateTextType.CopyTagsOnly
        : rbCustomText.IsChecked == true ? TranslateTextType.CustomText
        : TranslateTextType.Empty;

    private void UpdateEnabled()
    {
        // rgSelectionTypeClick e rgTextTypeClick.
        edTargetFile.IsEnabled = !MissingOnly;
        bttBrowseTargetFile.IsEnabled = edTargetFile.IsEnabled;
        edCustomText.IsEnabled = rbCustomText.IsChecked == true;
    }

    private void OnSelectionTypeClick(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateEnabled();
        }
    }

    private void OnTextTypeClick(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateEnabled();
        }
    }

    /// <summary>bttBrowseTargetFileClick.</summary>
    private void OnBrowseTargetFile(object sender, RoutedEventArgs e)
    {
        if (_dialogs.SaveFile("All files (*.*)|*.*", edTargetFile.Text, 1) is { } choice)
        {
            _hasBrowsedForFile = true;
            edTargetFile.Text = choice.Path;
        }
    }

    /// <summary>bttOKClick: sem ter escolhido pelo diálogo, pergunta antes de sobrescrever.</summary>
    private void OnOK(object sender, RoutedEventArgs e)
    {
        if (!MissingOnly && !_hasBrowsedForFile && File.Exists(edTargetFile.Text))
        {
            var res = MessageBox.Show(this, $"The subtitle file \"{edTargetFile.Text}\" already exists, do you want to overwrite it?",
                "Warning", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.No)
            {
                return;
            }

            if (res != MessageBoxResult.Yes)
            {
                DialogResult = false;
                return;
            }
        }

        Result = new TranslateResult(MissingOnly, Translators.Create(TextType, edCustomText.Text), edTargetFile.Text);
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
