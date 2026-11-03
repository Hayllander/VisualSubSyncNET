// Audio sync
// VisualSubSyncNET: a new plugin, not part of VisualSubSync 1.0.1 or Enhanced.
// It compares the subtitle start with the audio (the waveform peaks) and only reports:
// background music or noise can make it wrong, so every error must be checked by ear.

VSSPlugin = {
  // ----- Plugin constant -----
  Name : 'Audio sync',
  Description : 'Compares the start of each subtitle with the audio. An error is detected when ' +
    'the subtitle starts before the voice (silence at the beginning), when the voice started ' +
    'before the subtitle, or when no voice is found during the subtitle. Background music or ' +
    'noise can give false errors: raise the voice level if needed. Needs the waveform.',
  Color : 0x00A0A0,
  Message : 'Subtitle out of sync with the audio :',

  // ----- Plugin parameters available from VSS GUI (name must start with "Param") -----
  ParamVoiceLevel : { Value : 10, Unit : '%', Description :
    'Audio level that counts as voice, in percent of the loudest peak of the file (default: 10 %).' },
  ParamMaxEarlyStart : { Value : 300, Unit : 'ms', Description :
    'Maximum silence allowed between the subtitle start and the voice (default: 300 ms).' },
  ParamMaxLateStart : { Value : 300, Unit : 'ms', Description :
    'Maximum time the voice may start before the subtitle (default: 300 ms).' },
  ParamLookBack : { Value : 2000, Unit : 'ms', Description :
    'How far before the subtitle to look for the start of the voice (default: 2000 ms).' },
  ParamReportNoVoice : { Value : true, Unit : '', Description :
    'Report subtitles with no voice at all (default: true).' },

  // ----- HasError method called for each subtitle during the error checking -----
  HasError : function(CurrentSub, PreviousSub, NextSub) {
    // No waveform: nothing to compare with.
    if (VSSCore.GetAudioLevel(CurrentSub.Start, CurrentSub.Stop) < 0) {
      return '';
    }

    var level = this.ParamVoiceLevel.Value;
    var voice = VSSCore.FindAudio(CurrentSub.Start, CurrentSub.Stop, level, true);
    if (voice < 0) {
      return this.ParamReportNoVoice.Value ? 'no voice found' : '';
    }

    var early = voice - CurrentSub.Start;
    if (early > this.ParamMaxEarlyStart.Value) {
      return 'starts ' + early + ' ms before the voice';
    }

    // The voice is already there when the subtitle starts: look back for the silence before it,
    // but not before the end of the previous subtitle.
    if (early <= 10) {
      var limit = CurrentSub.Start - this.ParamLookBack.Value;
      if (PreviousSub != null && PreviousSub.Stop > limit) {
        limit = PreviousSub.Stop;
      }
      if (limit < CurrentSub.Start) {
        var silenceEnd = VSSCore.FindAudio(CurrentSub.Start, limit, level, false);
        if (silenceEnd >= 0) {
          var late = CurrentSub.Start - silenceEnd;
          if (late > this.ParamMaxLateStart.Value) {
            return 'starts ' + late + ' ms after the voice';
          }
        }
      }
    }

    return '';
  }
};
