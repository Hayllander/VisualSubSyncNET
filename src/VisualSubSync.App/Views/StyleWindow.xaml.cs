using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VisualSubSync.App.Services;
using VisualSubSync.Core.Subtitles;

namespace VisualSubSync.App.Views;

/// <summary>
/// TStyleForm: os estilos do documento são editados no lugar. A lógica fica em <see cref="StyleEditor"/>;
/// aqui só se copiam os campos para os controles e de volta.
/// </summary>
public partial class StyleWindow : Window
{
    private readonly StyleEditor _editor;
    private readonly Action<string, string> _renameStyle;
    private readonly Action _setDirty;
    private readonly Border[] _colorPanels;
    private readonly RadioButton[] _hRadios;
    private readonly RadioButton[] _vRadios;
    private bool _updating;

    // FPopupTrackbar.
    private Button? _trackbarButton;
    private int _trackbarMin;
    private int _trackbarMax = 100;
    private double _trackbarValue;

    public StyleWindow(StyleEditor editor, Action<string, string> renameStyle, Action setDirty)
    {
        ArgumentNullException.ThrowIfNull(editor);
        _editor = editor;
        _renameStyle = renameStyle;
        _setDirty = setDirty;
        InitializeComponent();
        _colorPanels = [pnlPrimaryColor, pnlSecondaryColor, pnlOutlineColor, pnlBackColor];
        _hRadios = [rbLeft, rbCenter, rbRight];
        _vRadios = [rbTop, rbMiddle, rbBottom];

        // ConfigureMode.
        edScaleX.IsEnabled = editor.AssMode;
        edScaleY.IsEnabled = editor.AssMode;
        edSpacing.IsEnabled = editor.AssMode;
        edAngle.IsEnabled = editor.AssMode;

        editor.Show();
        FillList();
        PushFields();
    }

    private void FillList()
    {
        _updating = true;
        lstStyles.Items.Clear();
        foreach (var style in _editor.Styles)
        {
            lstStyles.Items.Add(new ListBoxItem { Content = style.Name, Padding = new Thickness(2, 0, 2, 0) });
        }

        lstStyles.SelectedIndex = _editor.SelectedIndex;
        _updating = false;
    }

    /// <summary>Os campos do editor para os controles (DataToForm, EnableControls, CheckChange).</summary>
    private void PushFields()
    {
        _updating = true;
        var f = _editor.Fields;
        edStyleName.Text = f.Name;
        stFontPreview.Text = f.FontCaption;
        stFontPreview.FontFamily = new FontFamily(f.Font.Name);
        stFontPreview.FontSize = Math.Max(1, f.Font.SizePx);
        stFontPreview.FontWeight = f.Font.Bold ? FontWeights.Bold : FontWeights.Normal;
        stFontPreview.FontStyle = f.Font.Italic ? FontStyles.Italic : FontStyles.Normal;
        var decorations = new TextDecorationCollection();
        if (f.Font.Underline)
        {
            decorations.Add(TextDecorations.Underline);
        }

        if (f.Font.Strikeout)
        {
            decorations.Add(TextDecorations.Strikethrough);
        }

        stFontPreview.TextDecorations = decorations;
        int? rgb = f.Font.ToRgb();
        stFontPreview.Foreground = rgb is { } c
            ? new SolidColorBrush(Color.FromRgb((byte)(c >> 16), (byte)(c >> 8), (byte)c))
            : SystemColors.WindowTextBrush;

        for (int i = 0; i < 3; i++)
        {
            _hRadios[i].IsChecked = f.HAlign == i;
            _vRadios[i].IsChecked = f.VAlign == i;
        }

        edLMargin.Text = f.MarginL;
        edRMargin.Text = f.MarginR;
        edVMargin.Text = f.MarginV;
        edOutline.Text = f.Outline;
        edShadow.Text = f.Shadow;
        edScaleX.Text = f.ScaleX;
        edScaleY.Text = f.ScaleY;
        edSpacing.Text = f.Spacing;
        edAngle.Text = f.Angle;
        PushColors();
        if (f.Hue == 0 && f.Saturation == 0 && f.Luminance == 0)
        {
            bttHValue.Content = "H=0";
            bttSValue.Content = "S=0";
            bttLValue.Content = "L=0";
        }

        bool enable = _editor.ControlsEnabled;
        foreach (var control in new FrameworkElement[] { edStyleName, bttFont, rgHAlignment, rgVAlignment, edLMargin, edRMargin, edVMargin, edOutline, edShadow })
        {
            control.IsEnabled = enable;
        }

        foreach (var panel in _colorPanels)
        {
            panel.IsEnabled = enable;
        }

        UpdateApply();
        _updating = false;
    }

    /// <summary>
    /// Os painéis mostram o RGB da cor; o byte alto (alfa) é guardado mas não muda o que aparece.
    /// </summary>
    private void PushColors()
    {
        for (int i = 0; i < 4; i++)
        {
            uint v = _editor.Fields.Colors[i];
            _colorPanels[i].Background = new SolidColorBrush(Color.FromRgb((byte)v, (byte)(v >> 8), (byte)(v >> 16)));
        }
    }

    private void UpdateApply()
    {
        bttApply.IsEnabled = _editor.CanApply;
        bttReset.IsEnabled = _editor.CanApply;
    }

    /// <summary>Os controles para os campos do editor, e CheckChange (checkChangedSender).</summary>
    private void OnFieldChanged(object sender, RoutedEventArgs e)
    {
        if (_updating)
        {
            return;
        }

        var f = _editor.Fields;
        f.Name = edStyleName.Text;
        int h = Array.FindIndex(_hRadios, r => r.IsChecked == true);
        int v = Array.FindIndex(_vRadios, r => r.IsChecked == true);
        f.HAlign = h;
        f.VAlign = v;
        f.MarginL = edLMargin.Text;
        f.MarginR = edRMargin.Text;
        f.MarginV = edVMargin.Text;
        f.Outline = edOutline.Text;
        f.Shadow = edShadow.Text;
        f.ScaleX = edScaleX.Text;
        f.ScaleY = edScaleY.Text;
        f.Spacing = edSpacing.Text;
        f.Angle = edAngle.Text;
        _editor.CheckChange();
        UpdateApply();
    }

    /// <summary>lstStylesClick.</summary>
    private void OnStyleClick(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || lstStyles.SelectedIndex == -1)
        {
            return;
        }

        _editor.SelectedIndex = lstStyles.SelectedIndex;
        _editor.ShowSelection();
        PushFields();
    }

    private void OnFont(object sender, RoutedEventArgs e)
    {
        if (FontPicker.ChooseForStyle(this, _editor.Fields.Font, _editor.AssMode) is { } font)
        {
            _editor.SetFont(font);
            PushFields();
        }
    }

    /// <summary>pnlColorClick.</summary>
    private void OnColorClick(object sender, MouseButtonEventArgs e)
    {
        var panel = (Border)sender;
        int index = int.Parse((string)panel.Tag, CultureInfo.InvariantCulture);
        if (ColorPicker.Choose(this, _editor.Fields.Colors[index]) is { } color)
        {
            _editor.SetColor(index, color);
            PushColors();
            UpdateApply();
        }
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        _editor.New();
        FillList();
        PushFields();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        _editor.Copy();
        FillList();
        PushFields();
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        _editor.Delete();
        FillList();
        PushFields();
    }

    /// <summary>bttApplyClick (UpdateSelectionData).</summary>
    private void OnApply(object sender, RoutedEventArgs e)
    {
        if (_editor.SelectedStyle is null)
        {
            return;
        }

        string? error = _editor.Apply(_renameStyle);
        if (error is not null)
        {
            MessageBox.Show(this, error, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            edStyleName.Focus();
            return;
        }

        ((ListBoxItem)lstStyles.Items[_editor.SelectedIndex]).Content = _editor.SelectedStyle!.Name;
        _setDirty();
        PushFields();
    }

    /// <summary>bttResetClick: ShowSelection.</summary>
    private void OnReset(object sender, RoutedEventArgs e)
    {
        _editor.ShowSelection();
        PushFields();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseTrackbar();
        base.OnClosed(e);
    }

    /// <summary>bttHSLButtonClick: abre a barra à direita do botão (ou fecha, se já era dele).</summary>
    private void OnHslButton(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (PopupTrackbar.IsOpen)
        {
            var previous = _trackbarButton;
            CloseTrackbar();
            if (previous == button)
            {
                return;
            }
        }

        _trackbarButton = button;
        TrackbarPanel.Height = button.ActualHeight;
        var f = _editor.Fields;
        switch (button.Tag)
        {
            case "0":
                (_trackbarMin, _trackbarMax, _trackbarValue) = (0, 360, f.Hue);
                break;
            case "1":
                (_trackbarMin, _trackbarMax, _trackbarValue) = (-100, 100, f.Saturation);
                break;
            default:
                (_trackbarMin, _trackbarMax, _trackbarValue) = (-100, 100, f.Luminance);
                break;
        }

        UpdateTrackbar();
        PopupTrackbar.PlacementTarget = button;
        PopupTrackbar.IsOpen = true;
    }

    /// <summary>TPopupTrackbar.MouseMove: captura o mouse e cada movimento muda o valor.</summary>
    private void OnTrackbarMouseMove(object sender, MouseEventArgs e)
    {
        if (Mouse.Captured is null)
        {
            Mouse.Capture(TrackbarPanel);
        }

        SetTrackbarValue(e.GetPosition(TrackbarPanel).X);
        if (_trackbarButton is { } button)
        {
            button.Content = _editor.SetHsl(int.Parse((string)button.Tag, CultureInfo.InvariantCulture), _trackbarValue);
            PushColors();
            UpdateApply();
        }
    }

    /// <summary>TPopupTrackbar.MouseDown: o valor do clique fica na barra e ela fecha.</summary>
    private void OnTrackbarMouseDown(object sender, MouseButtonEventArgs e)
    {
        SetTrackbarValue(e.GetPosition(TrackbarPanel).X);
        CloseTrackbar();
        e.Handled = true;
    }

    private void SetTrackbarValue(double x)
    {
        double width = TrackbarPanel.Width;
        double value = _trackbarMin + (x / (width / (_trackbarMax - _trackbarMin)));
        _trackbarValue = Math.Clamp(value, _trackbarMin, _trackbarMax);
        UpdateTrackbar();
    }

    /// <summary>UpdateCaption e Paint: o texto é o valor arredondado; a parte à esquerda fica destacada.</summary>
    private void UpdateTrackbar()
    {
        string caption = ((long)Math.Round(_trackbarValue, MidpointRounding.ToEven)).ToString(CultureInfo.InvariantCulture);
        TrackbarText.Text = caption;
        TrackbarHighlightText.Text = caption;
        double width = TrackbarPanel.Width;
        TrackbarHighlight.Width = Math.Round(width / (_trackbarMax - _trackbarMin) * (_trackbarValue - _trackbarMin), MidpointRounding.ToEven);
    }

    private void CloseTrackbar()
    {
        PopupTrackbar.IsOpen = false;
        if (Mouse.Captured == TrackbarPanel)
        {
            TrackbarPanel.ReleaseMouseCapture();
        }

        _trackbarButton = null;
    }
}
