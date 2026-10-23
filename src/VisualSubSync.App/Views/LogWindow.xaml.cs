using System.ComponentModel;
using System.Windows;

namespace VisualSubSync.App.Views;

/// <summary>
/// TLogForm (LogWindowFormUnit.pas): mensagens dos plugins e da verificação de erros. LogMsg mostra a
/// janela, a menos que "Stay hidden" esteja marcado; SilentLogMsg só acrescenta a linha.
/// </summary>
public partial class LogWindow : Window
{
    public LogWindow() => InitializeComponent();

    /// <summary>LogMsg.</summary>
    public void LogMsg(string message)
    {
        SilentLogMsg(message);
        if (miStayHidden.IsChecked != true && !IsVisible)
        {
            Show();
        }
    }

    /// <summary>SilentLogMsg: MemoLog.Lines.Add.</summary>
    public void SilentLogMsg(string message)
    {
        MemoLog.AppendText(MemoLog.Text.Length > 0 ? "\r\n" + message : message);
        MemoLog.ScrollToEnd();
    }

    /// <summary>Clear.</summary>
    public void Clear() => MemoLog.Clear();

    /// <summary>miClearClick.</summary>
    private void OnClear(object sender, RoutedEventArgs e) => Clear();

    /// <summary>miStayHiddenClick: marca (AutoCheck) e esconde.</summary>
    private void OnStayHidden(object sender, RoutedEventArgs e) => Hide();

    /// <summary>miCloseClick.</summary>
    private void OnClose(object sender, RoutedEventArgs e) => Hide();

    /// <summary>O TLogForm vive o programa todo: fechar só esconde.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        e.Cancel = true;
        Hide();
    }
}
