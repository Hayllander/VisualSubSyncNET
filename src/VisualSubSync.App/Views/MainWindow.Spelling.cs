using System.Windows;
using System.Windows.Controls;
using VisualSubSync.App.Controls;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Spelling;

namespace VisualSubSync.App.Views;

/// <summary>O TagHighlight do MemoSubtitleText e as sugestões do corretor no MemoSubPopupMenu.</summary>
public partial class MainWindow
{
    private MemoHighlightAdorner? _memoHighlight;

    /// <summary>A palavra sob o mouse quando o menu do editor abriu (MemoSubPopupMenu.PopupPoint).</summary>
    private WordInfo? _popupWord;

    private void InitializeMemoHighlight(MainViewModel viewModel)
    {
        MemoSubtitleText.Loaded += (_, _) =>
        {
            _memoHighlight ??= MemoHighlightAdorner.Attach(MemoSubtitleText, ComputeMemoSpans);
        };
        viewModel.SpellingChanged += (_, _) => _memoHighlight?.Refresh();
        WAVDisplayer.KaraokeRangeSelected += (_, _) => _memoHighlight?.Refresh();
        WAVDisplayer.KaraokeChanged += (_, _) => _memoHighlight?.Refresh();
        MemoSubtitleText.ContextMenuOpening += MemoSubPopupMenuPopup;
    }

    private IReadOnlyList<HighlightSpan> ComputeMemoSpans()
    {
        var vm = _viewModel;
        return TagHighlighter.Compute(MemoSubtitleText.Text, vm.HighlightTagIndex, vm.IsLiveSpellCheckActive ? vm.SpellChecker.Spell : null);
    }

    /// <summary>
    /// MemoSubPopupMenuPopup: tira os itens acima de Undo e, para uma palavra recusada sob o mouse,
    /// põe as sugestões (ou "(No suggestions)"), "=> Add to dictionary", "=> Ignore" e um separador.
    /// </summary>
    private void MemoSubPopupMenuPopup(object sender, ContextMenuEventArgs e)
    {
        var menu = MemoSubPopupMenu;
        while (menu.Items.Count > 0 && !ReferenceEquals(menu.Items[0], MenuItemMemoSubUndo))
        {
            menu.Items.RemoveAt(0);
        }

        _popupWord = null;
        var vm = _viewModel;
        if (!vm.IsLiveSpellCheckActive || e.CursorLeft < 0 || e.CursorTop < 0)
        {
            return;
        }

        int charIndex = MemoSubtitleText.GetCharacterIndexFromPoint(new Point(e.CursorLeft, e.CursorTop), snapToText: true);
        if (charIndex < 0)
        {
            return;
        }

        string text = MemoSubtitleText.Text;
        var word = SpellWords.GetWordAt(charIndex, text);
        if (word.Length == 0)
        {
            return;
        }

        string aWord = text.Substring(word.Position, word.Length);
        if (vm.SpellChecker.Spell(aWord))
        {
            return;
        }

        _popupWord = word;
        int j = 0;
        foreach (string suggestion in vm.SpellChecker.Suggest(aWord))
        {
            var item = new MenuItem { Header = new TextBlock { Text = suggestion } };
            item.Click += (_, _) => ReplacePopupWord(suggestion);
            menu.Items.Insert(j++, item);
        }

        if (j == 0)
        {
            menu.Items.Insert(j++, new MenuItem { Header = "(No suggestions)", IsEnabled = false });
        }

        var add = new MenuItem { Header = "=> Add to dictionary" };
        add.Click += (_, _) => vm.AddToDictionary(aWord);
        menu.Items.Insert(j++, add);
        var ignore = new MenuItem { Header = "=> Ignore" };
        ignore.Click += (_, _) => vm.IgnoreWord(aWord);
        menu.Items.Insert(j++, ignore);
        menu.Items.Insert(j, new Separator());
    }

    /// <summary>OnSpellcheckSuggestionMenuItemClick: troca a palavra no texto, mantendo o SelStart.</summary>
    private void ReplacePopupWord(string suggestion)
    {
        if (_popupWord is not { } word)
        {
            return;
        }

        string text = MemoSubtitleText.Text;
        if (word.Position + word.Length > text.Length)
        {
            return;
        }

        int selStart = MemoSubtitleText.SelectionStart;
        MemoSubtitleText.Text = text[..word.Position] + suggestion + text[(word.Position + word.Length)..];
        MemoSubtitleText.SelectionStart = Math.Min(selStart, MemoSubtitleText.Text.Length);
        if (_viewModel.IsProjectOpen)
        {
            _viewModel.IsDirty = true;
        }
    }
}
