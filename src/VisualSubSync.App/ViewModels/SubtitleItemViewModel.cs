using CommunityToolkit.Mvvm.ComponentModel;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Timing;

namespace VisualSubSync.App.ViewModels;

/// <summary>Linha da lista de legendas (colunas #, Start, Stop, Style, Text do legado).</summary>
public sealed partial class SubtitleItemViewModel(Subtitle model, int index, Action<SubtitleItemViewModel, string>? changed = null) : ObservableObject
{
    public Subtitle Model { get; } = model;

    /// <summary>Número na lista (Node.Index + 1); muda quando a lista é reordenada.</summary>
    public int Index
    {
        get => index;
        set
        {
            if (SetProperty(ref index, value))
            {
                BumpRevision();
            }
        }
    }

    /// <summary>Muda sempre que algo da legenda ou a posição dela muda: as colunas extras são recalculadas.</summary>
    public int Revision { get; private set; }

    private void BumpRevision()
    {
        Revision++;
        OnPropertyChanged(nameof(Revision));
    }

    public string Start => TimeFormat.ToVss(Model.StartMs);

    public string Stop => TimeFormat.ToVss(Model.StopMs);

    public string Style => Model.Style;

    public string Text
    {
        get => Model.Text;
        set
        {
            if (Model.Text != value)
            {
                string oldText = Model.Text;
                Model.Text = value;
                changed?.Invoke(this, oldText);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SingleLineText));
                BumpRevision();
            }
        }
    }

    /// <summary>Os tempos do modelo mudaram (arraste na forma de onda): atualiza as colunas Start e Stop.</summary>
    public void RefreshTimes()
    {
        OnPropertyChanged(nameof(Start));
        OnPropertyChanged(nameof(Stop));
        BumpRevision();
    }

    /// <summary>Um plugin mudou o modelo diretamente: atualiza todas as colunas.</summary>
    public void RefreshAll()
    {
        Revision++;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Texto na lista, com quebras exibidas como "|", como no legado.</summary>
    public string SingleLineText => Model.Text.Replace("\r\n", "|", StringComparison.Ordinal);
}
