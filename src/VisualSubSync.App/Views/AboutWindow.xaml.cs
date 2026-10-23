using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace VisualSubSync.App.Views;

/// <summary>TAboutForm (AboutFormUnit.pas).</summary>
public partial class AboutWindow : Window
{
    private const string PublicVersion = "2.0.0.00";
    private const string ProductSiteUrl = "http://legendasrt.com.br/VisualSubSyncNET/";

    public AboutWindow(string version)
    {
        InitializeComponent();

        // Exibe a versão pública da marca; a versão interna alpha continua disponível na dica.
        LabelVersion.Text = "Version : " + PublicVersion;
        LabelVersion.ToolTip = version;
        lblJS.ToolTip = typeof(Jint.Engine).Assembly.GetName().Version?.ToString();
        lblSpell.ToolTip = typeof(WeCantSpell.Hunspell.WordList).Assembly.GetName().Version?.ToString();
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    /// <summary>TntLabel1Click.</summary>
    private void OnSiteClick(object sender, MouseButtonEventArgs e) => ShellOpen(ProductSiteUrl);

    private void OnTaggedLinkClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock { Tag: string target } && !string.IsNullOrWhiteSpace(target))
        {
            ShellOpen(target);
        }
    }

    /// <summary>FormKeyDown (KeyPreview): Esc fecha.</summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private static void ShellOpen(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.TraceWarning($"Could not open {target}: {ex.Message}");
        }
    }
}
