using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Waveform;

namespace VisualSubSync.App.ViewModels;

/// <summary>
/// Karaokê (main.pas, EnableExperimentalKaraoke): Create/Clear Karaoke, Insert karaoke marker,
/// Place karaoke cursors at end, o arraste dos marcadores na onda (WAVDisplayer1KaraokeChanged), a
/// seleção de um trecho (WAVDisplayer1SelectedKaraokeRange) e os ajustes de tempo com um trecho
/// selecionado (OffsetCurrentSubtitleStartTime/StopTime).
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>pmiCreateKaraokeClick: \k proporcional às letras em cada legenda selecionada.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void CreateKaraoke() => ChangeKaraoke(Karaoke.Create);

    /// <summary>pmiClearKaraokeClick: tira os \k das legendas selecionadas.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void ClearKaraoke() => ChangeKaraoke(Karaoke.Clear);

    /// <summary>ActionInsertKaraokeMarkerExecute: um marcador no cursor, na legenda sob ele (sem desfazer).</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void InsertKaraokeMarker()
    {
        int index = GetRangeIdxAt(CursorMs);
        if (index == -1)
        {
            return;
        }

        var sub = _document.Items[index];
        var subTimes = sub.SubTimes.ToList();
        Karaoke.AddSubTime(subTimes, CursorMs);
        sub.SubTimes = [.. subTimes];
        RefreshRanges();
        IsDirty = true;
    }

    /// <summary>ActionPlaceKaraokeCursorsAtEndExecute: junta os marcadores da legenda focada no fim dela.</summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void PlaceKaraokeCursorsAtEnd()
    {
        if (SelectedSubtitle is not { } item || Karaoke.PlaceCursorsAtEnd(item.Model) is not { } result || result.Text == item.Text)
        {
            return;
        }

        int index = Subtitles.IndexOf(item);
        DoAndPush(new MultiChangeTask(this, [new TextChange(index, item.Text, result.Text, [.. item.Model.SubTimes], result.SubTimes)]));
    }

    /// <summary>Um marcador está sendo arrastado: o TRange do original muda na hora.</summary>
    public void OnKaraokeChanging(KaraokeChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (SubAt(change) is { } sub)
        {
            sub.SubTimes[change.SubTimeIndex] = change.TimeMs;
            RefreshRanges();
        }
    }

    /// <summary>
    /// WAVDisplayer1KaraokeChanged: reescreve os \k a partir dos marcadores; quando o texto muda, a
    /// troca entra no desfazer com o marcador na posição de antes.
    /// </summary>
    public void OnKaraokeChanged(KaraokeChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (SubAt(change) is not { } sub)
        {
            return;
        }

        sub.SubTimes[change.SubTimeIndex] = change.TimeMs;
        string newText = Karaoke.TextFromSubTimes(sub);
        if (newText != sub.Text)
        {
            int[] oldSubTimes = [.. sub.SubTimes];
            oldSubTimes[change.SubTimeIndex] = change.OldTimeMs;
            DoAndPush(new MultiChangeTask(this, [new TextChange(change.RangeIndex, sub.Text, newText, oldSubTimes, [.. sub.SubTimes])]));
            IsDirty = true;
        }

        RefreshRanges();
    }

    /// <summary>WAVDisplayer1SelectedKaraokeRange: a legenda do trecho fica focada e é a única selecionada.</summary>
    public void OnKaraokeRangeSelected(int index)
    {
        if (index >= 0 && index < Subtitles.Count)
        {
            SelectedSubtitle = Subtitles[index];
        }
    }

    /// <summary>
    /// WAVDisplayer.KaraokeSelectedIndex para o TagHighlight do editor: só quando o trecho de karaokê
    /// selecionado é da legenda em edição; senão -1.
    /// </summary>
    public int HighlightTagIndex =>
        Waveform is { KaraokeSelectedRangeIndex: >= 0 } wave && SelectedSubtitle is { } item
            && Subtitles.IndexOf(item) == wave.KaraokeSelectedRangeIndex
            ? wave.KaraokeSelectedIndex
            : -1;

    /// <summary>
    /// OffsetCurrentSubtitleStartTime/StopTime com um trecho de karaokê selecionado: move o marcador
    /// do início (ou do fim) do trecho, a 10 ms dos vizinhos. Devolve falso quando não se aplica.
    /// </summary>
    private bool OffsetKaraokeMarker(int offset, bool start)
    {
        if (Waveform is not { KaraokeSelectedRangeIndex: >= 0 } wave || wave.KaraokeSelectedRangeIndex >= _document.Items.Count)
        {
            return false;
        }

        int rangeIndex = wave.KaraokeSelectedRangeIndex;
        var sub = _document.Items[rangeIndex];
        int idx = start ? wave.KaraokeSelectedIndex - 1 : wave.KaraokeSelectedIndex;
        if (idx < 0 || idx >= sub.SubTimes.Length)
        {
            return false;
        }

        int bStart = (idx == 0 ? sub.StartMs : sub.SubTimes[idx - 1]) + 10;
        int bStop = idx < sub.SubTimes.Length - 1 ? sub.SubTimes[idx + 1] - 10 : sub.StopMs - 10;
        int oldTime = sub.SubTimes[idx];
        int newTime = Constrain(oldTime + offset, bStart, bStop);
        sub.SubTimes[idx] = newTime;
        if (start)
        {
            SelectionStartMs = newTime;
        }
        else
        {
            SelectionStopMs = newTime;
        }

        OnKaraokeChanged(new KaraokeChange(rangeIndex, idx, newTime, oldTime));
        return true;

        // Constrain (MiscToolsUnit): primeiro o mínimo, depois o máximo.
        static int Constrain(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>SelectNextSub/SelectPreviousSub com um trecho de karaokê selecionado: o trecho vizinho.</summary>
    private bool SelectSiblingKaraoke(int step)
    {
        if (Waveform is not { KaraokeSelectedRangeIndex: >= 0 } wave)
        {
            return false;
        }

        if (step > 0)
        {
            wave.SelectNextKaraoke();
        }
        else
        {
            wave.SelectPreviousKaraoke();
        }

        return true;
    }

    private void ChangeKaraoke(Action<Subtitle> change)
    {
        var changes = new List<TextChange>();
        foreach (int index in SelectedIndexes())
        {
            var sub = _document.Items[index];
            var copy = sub.Clone();
            change(copy);
            changes.Add(new TextChange(index, sub.Text, copy.Text, [.. sub.SubTimes], copy.SubTimes));
        }

        if (changes.Count > 0)
        {
            DoAndPush(new MultiChangeTask(this, changes));
            IsDirty = true;
        }
    }

    private Subtitle? SubAt(KaraokeChange change) =>
        change.RangeIndex >= 0 && change.RangeIndex < _document.Items.Count &&
        change.SubTimeIndex >= 0 && change.SubTimeIndex < _document.Items[change.RangeIndex].SubTimes.Length
            ? _document.Items[change.RangeIndex]
            : null;
}
