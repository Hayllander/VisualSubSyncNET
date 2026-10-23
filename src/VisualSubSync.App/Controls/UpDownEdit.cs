using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VisualSubSync.App.Controls;

/// <summary>
/// TEdit com TUpDown associado (16 px à direita). Como o TUpDown, as setas e as teclas para cima/para
/// baixo andam de <see cref="Increment"/> entre <see cref="Minimum"/> e <see cref="Maximum"/> e
/// escrevem o número, com separador de milhar se <see cref="Thousands"/>; <see cref="Position"/> lê o texto
/// e, se ele não for um número dentro dos limites, fica com o último valor válido (UDM_GETPOS).
/// </summary>
public sealed class UpDownEdit : Grid
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(UpDownEdit), new PropertyMetadata(string.Empty, (d, e) => ((UpDownEdit)d).OnTextPropertyChanged((string)e.NewValue)));

    private readonly TextBox _edit = new() { VerticalContentAlignment = VerticalAlignment.Center };
    private int _position;

    public UpDownEdit()
    {
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        RowDefinitions.Add(new RowDefinition());
        RowDefinitions.Add(new RowDefinition());
        SetRowSpan(_edit, 2);
        Children.Add(_edit);
        Children.Add(Arrow(0, "M0,4 L3.5,0 L7,4 Z", 1));
        Children.Add(Arrow(1, "M0,0 L3.5,4 L7,0 Z", -1));
        _edit.TextChanged += (_, _) => Text = _edit.Text;
        _edit.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Up or Key.Down)
            {
                Step(e.Key == Key.Up ? 1 : -1);
                e.Handled = true;
            }
        };
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int Minimum { get; set; }

    public int Maximum { get; set; } = 100;

    public int Increment { get; set; } = 1;

    /// <summary>TUpDown.Thousands: separador de milhar no texto escrito pelas setas.</summary>
    public bool Thousands { get; set; } = true;

    /// <summary>TUpDown.Position: o número do texto, ou o último válido.</summary>
    public int Position
    {
        get
        {
            if (TryParse(Text, out int value))
            {
                _position = value;
            }

            return _position;
        }
        set
        {
            _position = Math.Clamp(value, Minimum, Maximum);
            Text = _position.ToString(Thousands ? "N0" : "D", CultureInfo.CurrentCulture);
        }
    }

    private void OnTextPropertyChanged(string text)
    {
        if (_edit.Text != text)
        {
            _edit.Text = text;
        }

        if (TryParse(text, out int value))
        {
            _position = value;
        }
    }

    private bool TryParse(string text, out int value) =>
        int.TryParse(text, NumberStyles.AllowThousands | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value) &&
        value >= Minimum && value <= Maximum;

    private void Step(int direction) => Position = Position + (direction * Increment);

    private RepeatButton Arrow(int row, string geometry, int direction)
    {
        var button = new RepeatButton
        {
            Focusable = false,
            Padding = new Thickness(0),
            Content = new Path { Data = Geometry.Parse(geometry), Fill = SystemColors.ControlTextBrush },
        };
        button.Click += (_, _) => Step(direction);
        SetColumn(button, 1);
        SetRow(button, row);
        return button;
    }
}
