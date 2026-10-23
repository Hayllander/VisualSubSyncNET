using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Spelling;

namespace VisualSubSync.App.Views;

/// <summary>TSpellCheckForm: mostra cada palavra recusada com as sugestões; a lógica fica em <see cref="SpellCheckSession"/>.</summary>
public partial class SpellCheckWindow : Window
{
    private readonly SpellCheckSession _session;
    private readonly Action<int> _focusSubtitle;

    public SpellCheckWindow(SpellCheckSession session, FontSpec font, Action<int> focusSubtitle)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(font);
        _session = session;
        _focusSubtitle = focusSubtitle;
        InitializeComponent();

        // FormCreate: o texto e as sugestões usam a fonte do editor de legendas.
        foreach (Control control in new Control[] { reSubtitleText, lbSuggestions })
        {
            control.FontFamily = new FontFamily(font.Name);
            control.FontSize = font.SizePx;
            control.FontWeight = font.Bold ? FontWeights.Bold : FontWeights.Normal;
            control.FontStyle = font.Italic ? FontStyles.Italic : FontStyles.Normal;
        }

        reSubtitleText.Document.PagePadding = new Thickness(0);

        // FormShow: Reset e NextError.
        Loaded += (_, _) =>
        {
            _session.NextError();
            ShowState();
        };
    }

    private void ShowState()
    {
        pbSubs.Maximum = Math.Max(1, _session.Count);
        pbSubs.Value = _session.Progress;
        lblSub.Text = _session.Caption;
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        if (!_session.EndOfCheck)
        {
            _focusSubtitle(_session.SubtitleIndex);
            string text = _session.Text;
            var error = _session.Error;
            paragraph.Inlines.Add(Plain(text[..error.Position]));
            paragraph.Inlines.Add(new Run(text.Substring(error.Position, error.Length)) { Foreground = Brushes.Red });
            paragraph.Inlines.Add(Plain(text[(error.Position + error.Length)..]));
        }

        reSubtitleText.Document.Blocks.Clear();
        reSubtitleText.Document.Blocks.Add(paragraph);
        lbSuggestions.ItemsSource = _session.Suggestions.ToList();
        if (_session.Suggestions.Count > 0)
        {
            lbSuggestions.SelectedIndex = 0;
        }

        edReplaceBy.Text = _session.ReplaceBy;

        // UpdateButton.
        bttReplace.IsEnabled = _session.CanReplace;
        bttReplaceAll.IsEnabled = _session.CanReplace;
        bttIgnore.IsEnabled = !_session.EndOfCheck;
        bttIgnoreAll.IsEnabled = !_session.EndOfCheck;
        bttAdd.IsEnabled = !_session.EndOfCheck;
    }

    /// <summary>As quebras de linha do texto viram LineBreak no RichTextBox.</summary>
    private static Span Plain(string text)
    {
        var span = new Span();
        string[] lines = text.Split("\r\n");
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                span.Inlines.Add(new LineBreak());
            }

            span.Inlines.Add(new Run(lines[i]));
        }

        return span;
    }

    private void OnReplace(object sender, RoutedEventArgs e)
    {
        _session.Replace(edReplaceBy.Text);
        ShowState();
    }

    private void OnReplaceAll(object sender, RoutedEventArgs e)
    {
        _session.ReplaceAll(edReplaceBy.Text);
        ShowState();
    }

    private void OnIgnore(object sender, RoutedEventArgs e)
    {
        _session.IgnoreOnce();
        ShowState();
    }

    private void OnIgnoreAll(object sender, RoutedEventArgs e)
    {
        _session.IgnoreAll();
        ShowState();
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        _session.Add();
        ShowState();
    }

    /// <summary>lbSuggestionsClick.</summary>
    private void OnSuggestionClick(object sender, SelectionChangedEventArgs e)
    {
        if (lbSuggestions.SelectedItem is string suggestion)
        {
            edReplaceBy.Text = suggestion;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}
