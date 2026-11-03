// Line balance
// VisualSubSyncNET: a new plugin, not part of VisualSubSync 1.0.1 or Enhanced.
// Finds subtitles broken in 2 lines that fit in one line, or whose 2 lines are very
// unbalanced (e.g. "He went out through the front|door"). FixError joins the lines and
// breaks them again at the best point. Dialogs (lines starting with "-") are ignored.

VSSPlugin = {
  // ----- Plugin constant -----
  Name : 'Line balance',
  Description : 'An error is detected when a subtitle in 2 lines fits in one line, or when ' +
    'its 2 lines are very unbalanced. The fix joins the lines and, if the text does not fit ' +
    'in one line, breaks it again at the space that gives the most balanced lines. ' +
    'Dialogs are ignored. Tags are not counted in the line length.',
  Color : 0xFF8C00,
  Message : 'Subtitle lines can be better balanced :',

  // ----- Plugin parameters available from VSS GUI (name must start with "Param") -----
  ParamMaxPerLine : { Value : 0, Unit : 'Characters', Description :
    'Maximum characters per line. 0 uses the value of the "Too long line" plugin (or 42).' },
  ParamMinRatio : { Value : 50, Unit : '%', Description :
    'The shorter line must have at least this percentage of the longer one (default: 50 %).' },

  // Text without tags ({...} and <...>).
  Strip : function(Text) {
    return Text.replace(/\{[^}]*\}/g, '').replace(/<[^>]*>/g, '');
  },

  MaxPerLine : function() {
    if (this.ParamMaxPerLine.Value > 0) {
      return this.ParamMaxPerLine.Value;
    }
    var Value = 0;
    try { Value = parseInt(VSSCore.GetPluginParamValue('Too long line', 'ParamMaxPerLine')); } catch (err) {}
    return Value > 0 ? Value : 42;
  },

  // The 2 lines of the subtitle, or null when it is not a 2-line subtitle that can be checked.
  Lines : function(CurrentSub) {
    var Lines = CurrentSub.Text.split('\r\n');
    if (Lines.length != 2 || CurrentSub.Text.match(/^\s*-/mg)) {
      return null;
    }
    return Lines;
  },

  // Best break of the text (one line) as [first, second], or [text] when it fits in one line.
  Rebreak : function(Text, Max) {
    if (this.Strip(Text).length <= Max) {
      return [Text];
    }
    var Best = null;
    var BestDiff = 0;
    var Depth = 0;
    for (var i = 0; i < Text.length; i++) {
      var c = Text.charAt(i);
      if (c == '<' || c == '{') Depth++;
      else if (c == '>' || c == '}') Depth--;
      else if (c == ' ' && Depth == 0) {
        var First = this.Strip(Text.substring(0, i)).length;
        var Second = this.Strip(Text.substring(i + 1)).length;
        var Diff = Math.abs(First - Second);
        // Prefer a break after punctuation; avoid one right after a short word (article,
        // preposition), which would separate it from the next word.
        var Before = this.Strip(Text.substring(0, i));
        var LastWord = Before.substring(Before.lastIndexOf(' ') + 1);
        if (/[,.;:!?]$/.test(LastWord)) Diff -= 3;
        else if (LastWord.length <= 3) Diff += 3;
        if (First <= Max && Second <= Max && (Best == null || Diff < BestDiff)) {
          Best = i;
          BestDiff = Diff;
        }
      }
    }
    if (Best == null) {
      return null;
    }
    return [Text.substring(0, Best), Text.substring(Best + 1)];
  },

  // ----- HasError method called for each subtitle during the error checking -----
  HasError : function(CurrentSub, PreviousSub, NextSub) {
    var Lines = this.Lines(CurrentSub);
    if (Lines == null) {
      return '';
    }
    var Max = this.MaxPerLine();
    var Len1 = this.Strip(Lines[0]).trim().length;
    var Len2 = this.Strip(Lines[1]).trim().length;
    var Joined = Lines[0].trim() + ' ' + Lines[1].trim();
    if (this.Strip(Joined).length <= Max) {
      return 'fits in one line (' + this.Strip(Joined).length + ' characters)';
    }
    var Shorter = Math.min(Len1, Len2);
    var Longer = Math.max(Len1, Len2);
    if (Longer > 0 && Shorter * 100 < Longer * this.ParamMinRatio.Value) {
      var Better = this.Rebreak(Joined, Max);
      if (Better != null && Better.length == 2) {
        var NewDiff = Math.abs(this.Strip(Better[0]).length - this.Strip(Better[1]).length);
        if (NewDiff < Longer - Shorter) {
          return 'unbalanced lines (' + Len1 + '/' + Len2 + ')';
        }
      }
    }
    return '';
  },

  FixError : function(CurrentSub, PreviousSub, NextSub) {
    var Lines = this.Lines(CurrentSub);
    if (Lines == null) {
      return;
    }
    var Better = this.Rebreak(Lines[0].trim() + ' ' + Lines[1].trim(), this.MaxPerLine());
    if (Better != null) {
      CurrentSub.Text = Better.join('\r\n');
    }
  }
};
