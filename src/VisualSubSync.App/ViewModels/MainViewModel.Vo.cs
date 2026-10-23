using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Waveform;

namespace VisualSubSync.App.ViewModels;

/// <summary>As legendas de referência (VO) de main.pas: LoadVO, ActionToggleVO, TurnOnVO/TurnOffVO e UpdateMemoVO.</summary>
public sealed partial class MainViewModel
{
    /// <summary>WAVDisplayer.RangeListVO.</summary>
    private List<Subtitle> _vo = [];

    /// <summary>A lista do VO para a forma de onda.</summary>
    [ObservableProperty]
    private IReadOnlyList<WaveformRange>? _voRanges;

    /// <summary>TurnOnVO/TurnOffVO: o MemoSubtitleVO, o splitter, a coluna VO e a faixa do VO na onda.</summary>
    [ObservableProperty]
    private bool _isVoVisible;

    /// <summary>Muda quando o texto da coluna VO precisa ser refeito.</summary>
    [ObservableProperty]
    private int _voRevision;

    /// <summary>MemoSubtitleVO.</summary>
    [ObservableProperty]
    private VoMemoText _memoVo = new(string.Empty, 0, 0);

    public bool HasVo => _vo.Count > 0;

    /// <summary>LoadVO: carrega (ou limpa, se o arquivo não existe) e ajusta a parte visual.</summary>
    private void LoadVO(string fileName)
    {
        _vo = VoReference.Load(fileName);
        VoRanges = [.. _vo.Select(s => new WaveformRange(s.StartMs, s.StopMs, s.Text))];
        AdjustShowVO();
        VoRevision++;
        UpdateMemoVO();
    }

    /// <summary>AdjustShowVO: com VO carregado, segue o ShowVO do projeto; sem VO, esconde.</summary>
    private void AdjustShowVO() => IsVoVisible = _vo.Count > 0 && Project is { ShowVO: true };

    private void ClearVO()
    {
        _vo = [];
        VoRanges = null;
        IsVoVisible = false;
        VoRevision++;
        MemoVo = new VoMemoText(string.Empty, 0, 0);
    }

    /// <summary>
    /// ActionToggleVOExecute: com VO carregado, mostra ou esconde e grava no projeto; sem VO, tenta
    /// recarregar o arquivo do projeto e, se não der, pergunta qual arquivo usar.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ToggleVO()
    {
        if (Project is not { } project)
        {
            IsVoVisible = false;
            return;
        }

        if (_vo.Count > 0)
        {
            project.ShowVO = !project.ShowVO;
            IsVoVisible = project.ShowVO;
            SaveProject(project, silent: true);
            return;
        }

        if (project.SubtitlesVO.Length > 0)
        {
            LoadVO(project.SubtitlesVO);
        }

        if (_vo.Count == 0)
        {
            string? fileName = _dialogs.OpenFile(
                "SRT files (*.srt)|*.SRT|SSA/ASS files (*.ssa,*.ass)|*.SSA;*.ASS|All files (*.*)|*.*", project.SubtitlesVO);
            if (fileName is not null)
            {
                if (project.SubtitlesVO != fileName)
                {
                    project.SubtitlesVO = fileName;
                    SaveProject(project, silent: true);
                }

                LoadVO(project.SubtitlesVO);
            }
        }

        if (_vo.Count > 0 && !project.ShowVO)
        {
            project.ShowVO = true;
            SaveProject(project, silent: true);
        }

        AdjustShowVO();
    }

    /// <summary>vtvSubsListGetText, coluna VO_COL_INDEX: GetVOText(NodeData, False).</summary>
    public string GetVoColumnText(SubtitleItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _vo.Count == 0 ? string.Empty : VoReference.GetVoText(_document.Items, _document.Items.IndexOf(item.Model), _vo, enhanced: false);
    }

    /// <summary>UpdateMemoVO: o texto do VO da legenda focada; sem legenda focada, vazio.</summary>
    private void UpdateMemoVO()
    {
        MemoVo = SelectedSubtitle is { } item && _vo.Count > 0
            ? VoReference.MemoText(VoReference.GetVoText(_document.Items, _document.Items.IndexOf(item.Model), _vo, enhanced: true))
            : new VoMemoText(string.Empty, 0, 0);
    }

    /// <summary>Depois de mudar tempos ou textos: a coluna VO e o MemoSubtitleVO acompanham as legendas.</summary>
    private void RefreshVoTexts()
    {
        if (_vo.Count > 0)
        {
            VoRevision++;
            UpdateMemoVO();
        }
    }
}
