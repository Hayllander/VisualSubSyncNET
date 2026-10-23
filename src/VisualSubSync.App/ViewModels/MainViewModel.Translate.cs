using System.IO;
using CommunityToolkit.Mvvm.Input;
using VisualSubSync.Core.Subtitles;
using VisualSubSync.Core.Subtitles.Formats;

namespace VisualSubSync.App.ViewModels;

/// <summary>ActionTranslationTemplate (File > Translate) de main.pas.</summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// ActionTranslationTemplateExecute. "All": grava uma cópia com o texto do tradutor, que passa a ser
    /// a legenda do projeto, e a legenda antiga vira o VO. "Missing only": cria, com desfazer, uma
    /// legenda para cada VO que não tem legenda no meio dele.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasProject))]
    private void Translate()
    {
        if (Project is not { } project || !CheckSubtitlesAreSaved())
        {
            return;
        }

        if (_dialogs.ShowTranslate(Translators.TranslatedFileName(project.SubtitlesFile), project.SubtitlesVO.Length > 0) is not { } choice)
        {
            return;
        }

        if (!choice.MissingOnly)
        {
            var options = BaseWriteOptions with
            {
                PreviousExtension = Path.GetExtension(project.SubtitlesFile),
                Translate = choice.Translate,
            };
            if (!SaveTo(choice.TargetFile, options))
            {
                return;
            }

            Errors.Clear();
            project.SubtitlesVO = project.SubtitlesFile;
            project.SubtitlesFile = choice.TargetFile;
            _document = LoadDocument(project.SubtitlesFile);
            LoadSubtitles(_document);
            ShowStyleColumn = SubtitleFile.HasStyles(project.SubtitlesFile);
            LoadVO(project.SubtitlesVO);
            if (IsVideoOpen)
            {
                SetVideoSubtitleFile(project.SubtitlesFile);
            }

            SelectNodeAtIndex(0);

            // TurnOnVO e ShowVO := True.
            project.ShowVO = true;
            IsVoVisible = true;
            SaveProject(project, silent: true);
            ClearUndo();
            return;
        }

        var subs = Translators.MissingFromVo(_vo, time => GetRangeIdxAt(time) != -1, choice.Translate);
        if (subs.Count > 0)
        {
            DoAndPush(new MultiAddTask(this, subs));
            IsDirty = true;
        }

        UpdateDurationWithSubOnly();
        RefreshRanges();
    }
}
