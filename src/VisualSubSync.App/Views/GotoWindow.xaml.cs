using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Text;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.App.Views;

/// <summary>TGotoForm (GotoFormUnit.pas): ir para uma linha da lista ou para um tempo.</summary>
public partial class GotoWindow : Window
{
    private bool _loaded;

    public GotoWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _loaded = true;
            EditLine.SelectAll();
        };
    }

    public GotoResult? Result { get; private set; }

    /// <summary>EditLineChange.</summary>
    private void OnLineChanged(object sender, TextChangedEventArgs e)
    {
        if (_loaded)
        {
            rbGotoLine.IsChecked = true;
        }
    }

    /// <summary>MaskEditTimeChange.</summary>
    private void OnTimeChanged(object sender, TextChangedEventArgs e)
    {
        if (_loaded)
        {
            rbGotoTime.IsChecked = true;
        }
    }

    private void OnGo(object sender, RoutedEventArgs e) => Accept();

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    /// <summary>FormKeyDown (KeyPreview): Enter confirma, Esc fecha.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            e.Handled = true;
            Accept();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    /// <summary>StrToIntDef(EditLine.Text, 0) ou TimeStringToMs(MaskEditTime.EditText).</summary>
    private void Accept()
    {
        Result = rbGotoTime.IsChecked == true
            ? new GotoResult(null, TimeFormat.ParseVss(MaskEditTime.Text))
            : new GotoResult(DelphiText.StrToIntDef(EditLine.Text, 0), null);
        DialogResult = true;
    }
}
