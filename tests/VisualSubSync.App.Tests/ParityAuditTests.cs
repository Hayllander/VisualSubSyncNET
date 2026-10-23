using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Net;
using System.Net.Sockets;
using VisualSubSync.App.Services;
using VisualSubSync.App.ViewModels;
using VisualSubSync.App.Views;
using System.Windows.Controls;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Timing;
using VisualSubSync.Core.Waveform;
using VisualSubSync.Media;
using VisualSubSync.Waveform;

namespace VisualSubSync.App.Tests;

public sealed class ParityAuditTests
{
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "STA test did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class Context : IDisposable
    {
        public string Folder { get; } = Directory.CreateTempSubdirectory("vss-parity-").FullName;
        public MainViewModel Model { get; }
        public RecordingProxy Dialogs { get; }

        public Context(params Subtitle[] subtitles)
        {
            var service = DispatchProxy.Create<IDialogService, RecordingProxy>();
            Dialogs = (RecordingProxy)(object)service;
            Model = new MainViewModel(service, VssSettings.Load(Path.Combine(Folder, "VisualSubSync.ini")));
            var document = new SubtitleDocument();
            document.Items.AddRange(subtitles);
            typeof(MainViewModel).GetField("_document", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Model, document);
            Invoke("LoadSubtitles", document);
            Model.Project = new VssProject { FileName = Path.Combine(Folder, "test.vssprj") };
            Model.SelectedSubtitle = Model.Subtitles.FirstOrDefault();
        }

        public void Invoke(string method, params object[] arguments) =>
            typeof(MainViewModel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Model, arguments);

        public void SetEngine(IMediaEngine media) =>
            typeof(MainViewModel).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Model, media);

        public void Dispose() { Model.Dispose(); Directory.Delete(Folder, recursive: true); }
    }

    [Fact]
    public void Close_project_unloads_media_and_disables_playback() => OnSta(() =>
    {
        using var context = new Context();
        int closes = 0;
        var engine = DispatchProxy.Create<IMediaEngine, RecordingProxy>();
        ((RecordingProxy)(object)engine).Handler = (method, _) =>
        {
            if (method.Name == "Close") closes++;
            return method.Name == "DisposeAsync" ? ValueTask.CompletedTask : null;
        };
        context.SetEngine(engine);
        context.Model.IsMediaOpen = true;
        context.Model.IsVideoOpen = true;
        context.Model.IsPlaying = true;
        context.Model.ShowingVideo = true;
        context.Model.PlayCursorMs = 5000;
        context.Model.CloseCommand.Execute(null);
        Assert.Equal(1, closes);
        Assert.Null(context.Model.Project);
        Assert.False(context.Model.IsPlaying);
        Assert.False(context.Model.IsMediaOpen);
        Assert.False(context.Model.IsVideoOpen);
        Assert.False(context.Model.ShowingVideo);
        Assert.Equal(0, context.Model.PlayCursorMs);
        Assert.False(context.Model.PlayCommand.CanExecute(null));
    });

    [Fact]
    public void Audio_only_project_does_not_show_an_empty_video_panel() => OnSta(() =>
    {
        using var context = new Context();
        context.Model.IsMediaOpen = true;
        context.Model.IsVideoOpen = false;
        context.Model.ShowHideVideoCommand.Execute(null);
        Assert.False(context.Model.ShowingVideo);
    });

    [Fact]
    public void Delay_and_undo_move_karaoke_markers_with_the_subtitle() => OnSta(() =>
    {
        using var context = new Context(new Subtitle { StartMs = 1000, StopMs = 2000, Text = "{\\k50}A{\\k50}B" });
        context.Dialogs.Handler = (method, _) => method.Name == "ShowDelay" ? new DelayResult(200, DelayApplyTo.All, DelayShiftType.BothTimes) : null;
        context.Model.DelayCommand.Execute(null);
        var item = Assert.Single(context.Model.Subtitles).Model;
        Assert.Equal(1200, item.StartMs);
        Assert.Equal([1700], item.SubTimes);
        Assert.Equal("{\\k50}A{\\k50}B", item.Text);
        context.Model.UndoCommand.Execute(null);
        Assert.Equal(1000, item.StartMs);
        Assert.Equal([1500], item.SubTimes);
        context.Model.RedoCommand.Execute(null);
        Assert.Equal([1700], item.SubTimes);
    });

    [Fact]
    public void Dragging_karaoke_boundaries_rewrites_tags_and_undo_restores_them() => OnSta(() =>
    {
        using var context = new Context(new Subtitle { StartMs = 1000, StopMs = 2000, Text = "{\\k50}A{\\k50}B" });
        context.Model.OnWaveformRangeChanged(new RangeTimesChange(0, 1100, 2100, 1000, 2000));
        Assert.Equal("{\\k40}A{\\k60}B", context.Model.Subtitles[0].Text);
        Assert.Equal([1500], context.Model.Subtitles[0].Model.SubTimes);
        context.Model.UndoCommand.Execute(null);
        Assert.Equal("{\\k50}A{\\k50}B", context.Model.Subtitles[0].Text);
    });

    [Fact]
    public void Playback_does_not_scroll_viewport_during_mouse_interaction() => OnSta(() =>
    {
        using var context = new Context();
        var engine = DispatchProxy.Create<IMediaEngine, RecordingProxy>();
        ((RecordingProxy)(object)engine).Handler = (method, _) => method.Name switch
        {
            "get_IsOpen" => true, "get_HasVideo" => false, "get_VideoSize" => (0, 0),
            "get_InterpolatedPositionMs" => 5000, "DisposeAsync" => ValueTask.CompletedTask, _ => null,
        };
        context.SetEngine(engine);
        var waveform = DispatchProxy.Create<IWaveformView, RecordingProxy>();
        bool interacting = true;
        ((RecordingProxy)(object)waveform).Handler = (method, _) => method.Name == "get_IsMouseInteracting" ? interacting : null;
        context.Model.Waveform = waveform;
        context.Model.LengthMs = 10000;
        context.Model.PageSizeMs = 1000;
        context.Model.AutoScrollWavDisplay = true;
        context.Invoke("OnPlayTimerTick");
        Assert.Equal(0, context.Model.PositionMs);
        interacting = false;
        context.Invoke("OnPlayTimerTick");
        Assert.Equal(4875, context.Model.PositionMs);
    });

    [Fact]
    public void Failed_server_bind_keeps_menu_state_stopped() => OnSta(() =>
    {
        using var occupied = new TcpListener(IPAddress.Any, 0);
        occupied.Start();
        using var context = new Context();
        context.Model.Settings.WebServerPort = ((IPEndPoint)occupied.LocalEndpoint).Port;
        context.Model.StartWebServerCommand.Execute(null);
        Assert.False(context.Model.IsWebServerRunning);
        Assert.True(context.Model.IsWebServerStopped);
    });

    [Fact]
    public void Reload_failure_keeps_current_document_in_memory() => OnSta(() =>
    {
        using var context = new Context(new Subtitle { StartMs = 0, StopMs = 1000, Text = "keep" });
        string path = Path.Combine(context.Folder, "locked.srt");
        File.WriteAllText(path, "unreadable while locked");
        context.Model.Project!.SubtitlesFile = path;
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        context.Model.ReloadCommand.Execute(null);
        Assert.Equal("keep", Assert.Single(context.Model.Subtitles).Text);
    });

    [Fact]
    public void Opening_external_wav_generates_the_missing_peak_cache() => OnSta(() =>
    {
        using var context = new Context();
        string path = Path.Combine(context.Folder, "audio.wav");
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write("RIFF"u8); writer.Write(76); writer.Write("WAVEfmt "u8);
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(1000);
            writer.Write(2000); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8); writer.Write(40); writer.Write(new byte[40]);
        }

        string project = Path.Combine(context.Folder, "wav.vssprj");
        VssProjectFile.Save(new VssProject { WavMode = ProjectWavMode.External, WavFile = path }, project);
        context.Model.LoadProject(project).GetAwaiter().GetResult();
        Assert.NotNull(context.Model.Peaks);
        Assert.Equal(20, context.Model.Peaks.LengthMs);
        Assert.True(File.Exists(Path.ChangeExtension(path, ".peak")));
    });

    [Fact]
    public void Audio_selection_follows_video_visibility_like_the_two_legacy_renderers() => OnSta(() =>
    {
        using var context = new Context();
        string wav = Path.Combine(context.Folder, "audio.wav");
        File.WriteAllText(wav, "file presence only; native selection is tested separately");
        context.Model.Project!.WavFile = wav;
        context.Model.IsVideoOpen = true;
        var requests = new List<string?>();
        var engine = DispatchProxy.Create<IMediaEngine, RecordingProxy>();
        ((RecordingProxy)(object)engine).Handler = (method, arguments) =>
        {
            if (method.Name == "SetExternalAudio") requests.Add((string?)arguments![0]);
            return method.Name switch { "get_IsOpen" => true, "DisposeAsync" => ValueTask.CompletedTask, _ => null };
        };
        context.SetEngine(engine);
        context.Invoke("ApplyPlaybackAudio", context.Model.Project, false);
        context.Invoke("ApplyPlaybackAudio", context.Model.Project, true);
        Assert.Equal([wav, null], requests);
    });

    [Fact]
    public void Suggestions_use_the_subtitle_list_font_family() => OnSta(() =>
    {
        using var context = new Context();
        context.Model.Settings.SubListFont = new FontSpec("Consolas", 14, 0, 0, "clWindowText");
        var window = new SuggestionWindow(context.Model);
        Assert.Equal("Consolas", ((ListBox)window.FindName("vtvSuggestionsLst")).FontFamily.Source);
        Assert.Equal("Consolas", ((TextBox)window.FindName("MemoSuggPreview")).FontFamily.Source);
        window.Close(); // The legacy tool window hides rather than being destroyed.
    });

    [Fact]
    public void Changing_wav_in_project_properties_reopens_audio_source() => OnSta(() =>
    {
        using var context = new Context();
        string previous = Path.Combine(context.Folder, "first.wav");
        string replacement = Path.Combine(context.Folder, "second.wav");
        File.WriteAllText(previous, "presence only"); File.WriteAllText(replacement, "presence only");
        context.Model.Project!.WavMode = ProjectWavMode.NoWaveform;
        context.Model.Project.WavFile = previous;
        context.Dialogs.Handler = (method, _) => method.Name == "ShowProjectForm" ? new ProjectFormData
        {
            ProjectFile = context.Model.Project.FileName, WavFile = replacement, WavMode = ProjectWavMode.NoWaveform,
        } : null;
        var opened = new List<string>();
        var engine = DispatchProxy.Create<IMediaEngine, RecordingProxy>();
        ((RecordingProxy)(object)engine).Handler = (method, arguments) =>
        {
            if (method.Name == "OpenAsync") opened.Add((string)arguments![0]!);
            return method.Name switch
            {
                "OpenAsync" or "SeekAsync" => Task.CompletedTask,
                "get_IsOpen" => true, "get_HasVideo" => false, "get_VideoSize" => (0, 0),
                "get_DurationMs" => 1000, "DisposeAsync" => ValueTask.CompletedTask, _ => null,
            };
        };
        context.SetEngine(engine);
        context.Model.ProjectPropertiesCommand.ExecuteAsync(null).GetAwaiter().GetResult();
        Assert.Equal([replacement], opened);
        Assert.Equal(replacement, context.Model.Project.WavFile);
    });
}
