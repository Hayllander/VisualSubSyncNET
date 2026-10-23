using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace VisualSubSync.App.Views;

/// <summary>
/// TPeakCreationProgressForm: "Creating peak file...", sempre por cima, só a barra de progresso
/// (250x25 com borda de 4); o FormCloseQuery não deixa fechar enquanto os picos são criados.
/// </summary>
internal sealed class PeakCreationProgressWindow : Window
{
    private readonly ProgressBar _progressBar1 = new() { Width = 250, Height = 25, Minimum = 0, Maximum = 100 };
    private bool _closing;

    public PeakCreationProgressWindow()
    {
        Title = "Creating peak file...";
        ResizeMode = ResizeMode.CanMinimize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Topmost = true;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Content = new Border { Padding = new Thickness(4), Child = _progressBar1 };
    }

    /// <summary>SetProgress.</summary>
    public void SetProgress(int value) => _progressBar1.Value = value;

    public void CloseForGood()
    {
        _closing = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        e.Cancel = !_closing;
    }
}
