This directory contains JavaScript plugins for VisualSubSync.

For more information about JavaScript check this links:
http://developer.mozilla.org/en/docs/Core_JavaScript_1.5_Guide
http://developer.mozilla.org/en/docs/Core_JavaScript_1.5_Reference
http://developer.mozilla.org/en/docs/A_re-introduction_to_JavaScript


VSS specific functions and properties:

ScriptLog(Message) : Display a message in the log window
SetStatusBarText(Message) : Display a message in the status bar
LoadScript(Filename) : Load an external javascript file


VSSCore.INDEX_COL_IDX : Index of the subtitle index column
VSSCore.START_COL_IDX : Index of the start time column
VSSCore.STOP_COL_IDX  : Index of the stop time colum
VSSCore.STYLE_COL_IDX : Index of the style column (SSA/ASS only)
VSSCore.TEXT_COL_IDX  : Index of the text column
VSSCore.LAST_CORE_COL_IDX : Index of the last column of VSS core

VSSCore.CpsTarget : Characters per second target (value set in the preferences dialog)
VSSCore.MinimumDuration : Minimum subtitle duration (value set in the preferences dialog)
VSSCore.MaximumDuration : Maximum subtitle duration (value set in the preferences dialog)
VSSCore.MinimumBlank : Minimum blank between subtitles (value set in the preferences dialog)

VSSCore.VideoWidth : The video width in pixel (0 if no video)
VSSCore.VideoHeight : The video height in pixel (0 if no video)

VSSCore.RegisterJavascriptAction(Name, Description, DefaultShortcut) : Register a javascript action
VSSCore.GetSubCount() : Return the total number of subtitles
VSSCore.GetSubAt(Index) : Return the subtitle at the specified index
VSSCore.GetFirst() : Return the first subtitle
VSSCore.GetNext(Subtitle) : Return the subtitle next to the specified one
VSSCore.GetPrevious(Subtitle) : Return the subtitle previous to the specified one
VSSCore.GetFirstSelected() : Return the first selected subtitle
VSSCore.GetNextSelected(Subtitle) : Return the next selected subtitle after the specified one
VSSCore.MeasureStringWidth(FontName, FontSize, IsBold, Text) : Return the width of the text in pixels.

Subtitle.Index : Index of the subtitle
Subtitle.Start : Start time in ms of the subtitle
Subtitle.Stop : Stop time in ms of the subtitle
Subtitle.Text : Subtitle's text
Subtitle.StrippedText : Subtitle's text without any tags


SceneChange.StartOffset : The offset in ms to keep before a scene change (value set in the preferences dialog)
SceneChange.StopOffset : The offset in ms to keep after a scene change (value set in the preferences dialog)
SceneChange.FilterOffset : The offset in ms from subtitle start and stop where scene change are filtered (value set in the preferences dialog)
SceneChange.Visible : True if scene change are currenlty visible in VSS (value set in the preferences dialog)
SceneChange.GetCount() : Get the total number of scene change
SceneChange.GetAt(Index) : Get the time of the scene change at the specified index in ms. Index is between 0 and GetCount()-1
SceneChange.GetNext(TimeMs) : Get the time in ms of the next scene change superior or equal to TimeMs
SceneChange.GetPrevious(TimeMs) : Get the time in ms of the next scene change inferior or equal to TimeMs
SceneChange.Contains(Start,Stop) : Check if there is a scene change between [Start,Stop]



VisualSubSyncNET only (new, not in VisualSubSync 1.0.1 or Enhanced):

VSSCore.GetAudioLevel(Start, Stop) : Audio level between Start and Stop (ms), in percent of the loudest peak of the file. -1 without waveform.
VSSCore.FindAudio(From, To, Level, Above) : First time (ms) from From to To (backward if To < From) where the audio stays 30 ms above Level (Above true) or below it (Above false). Backward, the end of that stretch is returned. -1 if not found.
