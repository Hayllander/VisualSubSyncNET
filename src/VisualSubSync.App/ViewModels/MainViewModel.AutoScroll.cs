using System.Windows.Threading;

namespace VisualSubSync.App.ViewModels;

/// <summary>chkAutoScrollSub e TimerAutoScrollSub: a lista acompanha a legenda sob o cursor de reprodução.</summary>
public sealed partial class MainViewModel
{
    private DispatcherTimer? _autoScrollSubTimer;

    /// <summary>OldAutoScrollIdx.</summary>
    private int _oldAutoScrollIdx = -1;

    /// <summary>chkAutoScrollSubClick: o timer de 80 ms só roda com a caixa marcada.</summary>
    private void ApplyAutoScrollSubtitles(bool enabled)
    {
        if (_autoScrollSubTimer is null)
        {
            _autoScrollSubTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            _autoScrollSubTimer.Tick += (_, _) => AutoScrollSubTimerTick();
        }

        _autoScrollSubTimer.IsEnabled = enabled;
        _oldAutoScrollIdx = -1;
    }

    /// <summary>
    /// TimerAutoScrollSubTimer: durante a reprodução, foca e seleciona só a legenda sob o cursor de
    /// reprodução quando ela muda. Fora de qualquer legenda o editor e o VO ficam vazios, sem desfazer;
    /// aqui isso é feito tirando o foco da lista, já que o editor mostra o texto da legenda focada.
    /// </summary>
    private void AutoScrollSubTimerTick()
    {
        if (!IsPlaying)
        {
            return;
        }

        int idx = GetRangeIdxAt(PlayCursorMs);
        if (idx == _oldAutoScrollIdx)
        {
            return;
        }

        SelectedSubtitle = idx != -1 ? Subtitles[idx] : null;
        _oldAutoScrollIdx = idx;
    }
}
