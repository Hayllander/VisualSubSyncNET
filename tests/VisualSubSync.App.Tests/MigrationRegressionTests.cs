using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using VisualSubSync.App.Services;
using VisualSubSync.App.ViewModels;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Settings;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Media;

namespace VisualSubSync.App.Tests;

public sealed class MigrationRegressionTests
{
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "STA test did not finish.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Changing_subtitle_stops_on_cancel_or_failed_save(bool chooseSave) => OnSta(() =>
    {
        string folder = Directory.CreateTempSubdirectory("vss-app-").FullName;
        try
        {
            string original = Path.Combine(folder, "original.srt");
            File.WriteAllText(original, "original bytes");
            var dialogs = DispatchProxy.Create<IDialogService, RecordingProxy>();
            var proxy = (RecordingProxy)(object)dialogs;
            proxy.Handler = (method, _) => method.Name switch
            {
                "ShowProjectForm" => new ProjectFormData { SubtitlesFile = Path.Combine(folder, "new.srt") },
                "AskYesNoCancel" => chooseSave ? true : null,
                _ => null,
            };
            var settings = VssSettings.Load(Path.Combine(folder, "test.ini"));
            settings.EnableBackup = false;
            using var model = new MainViewModel(dialogs, settings);
            model.Project = new VssProject { SubtitlesFile = original, FileName = Path.Combine(folder, "project.vssprj") };
            var row = new SubtitleItemViewModel(new Subtitle { Text = "unsaved text", StartMs = 1000, StopMs = 2000 }, 1);
            model.Subtitles.Add(row);
            model.IsDirty = true;
            using (var locked = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                model.ProjectPropertiesCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            }

            Assert.Equal(original, model.Project.SubtitlesFile);
            Assert.Same(row, Assert.Single(model.Subtitles));
            Assert.True(model.IsDirty);
            Assert.Equal("original bytes", File.ReadAllText(original));
            Assert.False(File.Exists(Path.Combine(folder, "new.srt")));
        }
        finally { Directory.Delete(folder, recursive: true); }
    });

    [Fact]
    public void Changing_extension_keeps_original_when_new_file_cannot_be_written() => OnSta(() =>
    {
        string folder = Directory.CreateTempSubdirectory("vss-app-").FullName;
        try
        {
            string original = Path.Combine(folder, "original.srt");
            string destination = Directory.CreateDirectory(Path.Combine(folder, "original.ass")).FullName;
            var dialogs = DispatchProxy.Create<IDialogService, RecordingProxy>();
            ((RecordingProxy)(object)dialogs).Handler = (method, _) => method.Name == "ShowProjectForm"
                ? new ProjectFormData { SubtitlesFile = destination } : null;
            var settings = VssSettings.Load(Path.Combine(folder, "test.ini"));
            settings.EnableBackup = false;
            using var model = new MainViewModel(dialogs, settings);
            model.Project = new VssProject { SubtitlesFile = original, FileName = Path.Combine(folder, "project.vssprj") };
            model.ProjectPropertiesCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.Equal(original, model.Project.SubtitlesFile);
            Assert.False(File.Exists(destination));
        }
        finally { Directory.Delete(folder, recursive: true); }
    });

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(29.97)]
    public void Frame_commands_send_exact_timestamps_without_constant_fps_alignment(double fps) => OnSta(() =>
    {
        var requests = new List<int>();
        var media = DispatchProxy.Create<IMediaEngine, RecordingProxy>();
        ((RecordingProxy)(object)media).Handler = (method, arguments) => method.Name switch
        {
            "get_FramesPerSecond" => fps,
            "SeekAsync" => RecordSeek(requests, arguments),
            "DisposeAsync" => ValueTask.CompletedTask,
            _ => null,
        };
        var dialogs = DispatchProxy.Create<IDialogService, RecordingProxy>();
        using var model = new MainViewModel(dialogs, VssSettings.Load(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ini")));
        typeof(MainViewModel).GetField("_engine", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(model, media);
        model.SelectedSubtitle = new SubtitleItemViewModel(new Subtitle { StartMs = 1017, StopMs = 2051, Text = "frame" }, 1);
        model.IsVideoOpen = true;
        model.ShowStartFrameCommand.Execute(null);
        model.ShowStopFrameCommand.Execute(null);
        Assert.Equal([1017, 2051], requests);
    });

    private static Task RecordSeek(List<int> requests, object?[]? arguments)
    {
        requests.Add((int)arguments![0]!);
        return Task.CompletedTask;
    }
}

public class RecordingProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?>? Handler { get; set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        Handler?.Invoke(targetMethod!, args);
}
