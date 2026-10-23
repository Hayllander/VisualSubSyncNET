using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VisualSubSync.App.Controls;

/// <summary>
/// TMaskEdit com EditMask "!99:99:99.999;1;0": só dígitos, escritos por cima, pulando os separadores;
/// posições apagadas voltam a "0" (o caractere de vazio da máscara).
/// </summary>
public sealed class MaskTimeEdit : TextBox
{
    private const string Blank = "00:00:00.000";

    public MaskTimeEdit()
    {
        Text = Blank;
        MaxLength = Blank.Length;
        IsUndoEnabled = false;
        AllowDrop = false;
        DataObject.AddPastingHandler(this, OnPaste);
    }

    private static bool IsDigitPosition(int index) => index is >= 0 and < 12 && char.IsDigit(Blank[index]);

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        e.Handled = true;
        foreach (char c in e.Text)
        {
            if (char.IsAsciiDigit(c))
            {
                Type(c);
            }
        }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        switch (e.Key)
        {
            case Key.Back:
                e.Handled = true;
                if (SelectionLength > 0)
                {
                    ClearSelection();
                }
                else
                {
                    int i = SelectionStart - 1;
                    while (i >= 0 && !IsDigitPosition(i))
                    {
                        i--;
                    }

                    if (i >= 0)
                    {
                        SetChar(i, '0');
                        CaretIndex = i;
                    }
                }

                break;
            case Key.Delete:
                e.Handled = true;
                if (SelectionLength > 0)
                {
                    ClearSelection();
                }
                else if (IsDigitPosition(SelectionStart))
                {
                    int caret = SelectionStart;
                    SetChar(caret, '0');
                    CaretIndex = caret;
                }

                break;
            case Key.Space:
                e.Handled = true;
                break;
            default:
                base.OnPreviewKeyDown(e);
                break;
        }
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (e.DataObject.GetData(DataFormats.UnicodeText) is string text)
        {
            foreach (char c in text.Where(char.IsAsciiDigit))
            {
                Type(c);
            }
        }
    }

    /// <summary>Escreve o dígito na próxima posição de dígito a partir do cursor e avança.</summary>
    private void Type(char digit)
    {
        if (SelectionLength > 0)
        {
            ClearSelection();
        }

        int i = SelectionStart;
        while (i < Blank.Length && !IsDigitPosition(i))
        {
            i++;
        }

        if (i >= Blank.Length)
        {
            return;
        }

        SetChar(i, digit);
        i++;
        while (i < Blank.Length && !IsDigitPosition(i))
        {
            i++;
        }

        CaretIndex = i;
    }

    private void ClearSelection()
    {
        int start = SelectionStart, end = start + SelectionLength;
        var chars = Text.ToCharArray();
        for (int i = start; i < end; i++)
        {
            if (IsDigitPosition(i))
            {
                chars[i] = '0';
            }
        }

        Text = new string(chars);
        CaretIndex = start;
    }

    private void SetChar(int index, char c)
    {
        var chars = Text.PadRight(Blank.Length, '0').ToCharArray();
        chars[index] = c;
        Text = new string(chars);
    }
}
