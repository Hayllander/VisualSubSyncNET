using VisualSubSync.Core.Hotkeys;
using VisualSubSync.Core.Plugins;
using VisualSubSync.Core.Projects;
using VisualSubSync.Core.Text;

namespace VisualSubSync.Core.Settings;

/// <summary>
/// Parte do VisualSubSync.ini usada pela janela principal (TConfigObject e TMainForm.LoadSettings/
/// SaveSettings). Chaves, seções e valores padrão são os do original.
/// </summary>
public sealed class VssSettings
{
    public static readonly FontSpec DefaultSubListFont = new("Arial", 8, 0, 0, "clWindowText");
    public static readonly FontSpec DefaultSubTextFont = new("Arial", 10, 1, 0, "clWindowText");

    private IniDocument _ini = IniDocument.Parse(string.Empty);

    public string FileName { get; private set; } = string.Empty;

    /// <summary>[Fonts] SubList: fonte da lista de legendas.</summary>
    public FontSpec SubListFont { get; set; } = DefaultSubListFont;

    /// <summary>[Fonts] SubText: fonte do editor, do contador de linhas e do texto na forma de onda.</summary>
    public FontSpec SubTextFont { get; set; } = DefaultSubTextFont;

    /// <summary>[Misc] SwapSubtitlesList: editor acima e lista embaixo.</summary>
    public bool SwapSubtitlesList { get; set; }

    /// <summary>[Misc] QuickSmartSilentZonePanels (Enhanced): painéis Quick &amp; Smart Delay, Silent Zone e Smart Desynch.</summary>
    public bool QuickSmartSilentZonePanels { get; set; } = true;

    /// <summary>[Misc] AutoScrollWavDisplay (Enhanced).</summary>
    public bool AutoScrollWavDisplay { get; set; }

    /// <summary>[Misc] AutoScrollSubtitles (Enhanced).</summary>
    public bool AutoScrollSubtitles { get; set; }

    /// <summary>[WAVDisplay] ShowTextInWAVDisplay.</summary>
    public bool ShowTextInWavDisplay { get; set; } = true;

    /// <summary>[Misc] EnableMouseSnapping: o mouse encaixa no espaço mínimo e nas mudanças de cena.</summary>
    public bool EnableMouseSnapping { get; set; } = true;

    /// <summary>[Misc] EnableMouseAntiOverlapping: o mouse não sobrepõe a legenda vizinha.</summary>
    public bool EnableMouseAntiOverlapping { get; set; }

    /// <summary>[Misc] SpaceKeyBlankBetweenSubtitles: espaço mínimo entre legendas (ms).</summary>
    public int SpaceKeyBlankBetweenSubtitles { get; set; } = 120;

    /// <summary>[Mouse] WheelTimeScrollModifier: a tecla que, com a roda, rola a forma de onda (Ctrl).</summary>
    public WheelModifier MouseWheelTimeScrollModifier { get; set; } = WheelModifier.Ctrl;

    /// <summary>[Mouse] WheelVZoomModifier: zoom vertical (Shift).</summary>
    public WheelModifier MouseWheelVZoomModifier { get; set; } = WheelModifier.Shift;

    /// <summary>[Mouse] WheelHZoomModifier: zoom horizontal (roda sem tecla).</summary>
    public WheelModifier MouseWheelHZoomModifier { get; set; } = WheelModifier.None;

    /// <summary>[Backup] EnableBackup: cópia .bak na pasta Backup antes de sobrescrever.</summary>
    public bool EnableBackup { get; set; } = true;

    /// <summary>[Backup] AutoBackupEvery: minutos entre as cópias automáticas (0 desliga).</summary>
    public int AutoBackupEvery { get; set; }

    /// <summary>[Backup] AutoSaveWhenPlaying: salva ao começar a tocar.</summary>
    public bool AutoSaveWhenPlaying { get; set; }

    /// <summary>[Mouse] EnableSSATimingMode: no modo Timing, botão esquerdo = início e direito = fim.</summary>
    public bool MouseEnableSsaTimingMode { get; set; }

    /// <summary>[Misc] DisableSubtitleEdition: no modo Timing o editor fica desabilitado.</summary>
    public bool DisableSubtitleEdition { get; set; } = true;

    /// <summary>[Misc] EnableToggleCreation: no modo Timing, segurar Espaço cria ou ajusta a legenda.</summary>
    public bool EnableToggleCreation { get; set; } = true;

    /// <summary>[Misc] SpaceKeyModifyTiming: Espaço ajusta a legenda focada em vez de criar uma nova.</summary>
    public bool SpaceKeyModifyTiming { get; set; } = true;

    /// <summary>[Misc] SpaceKeyCPSTarget: caracteres por segundo para calcular o fim.</summary>
    public int SpaceKeyCpsTarget { get; set; } = 18;

    /// <summary>[Misc] SpaceKeyMinimalDuration (ms).</summary>
    public int SpaceKeyMinimalDuration { get; set; } = 1000;

    /// <summary>[Misc] MaximumSubtitleDuration (ms): VSSCore.MaximumDuration dos plugins.</summary>
    public int MaximumSubtitleDuration { get; set; } = 6000;

    /// <summary>ListJSPlugin: os plugins de erro, lidos por <see cref="LoadPlugins"/>.</summary>
    public PluginSettings? Plugins { get; private set; }

    /// <summary>[Hotkeys]: atalhos das ações nos modos Normal e Timing.</summary>
    public HotkeyTable Hotkeys { get; private set; } = HotkeyTable.CreateDefault();

    /// <summary>[WAVDisplay] ShowSceneChange.</summary>
    public bool ShowSceneChange { get; set; }

    /// <summary>[WAVDisplay] SceneChangeStartOffset (ms antes da mudança de cena).</summary>
    public int SceneChangeStartOffset { get; set; } = 130;

    /// <summary>[WAVDisplay] SceneChangeStopOffset (ms depois da mudança de cena).</summary>
    public int SceneChangeStopOffset { get; set; } = 130;

    /// <summary>[WAVDisplay] SceneChangeFilterOffset: mudanças de cena no meio da legenda são ignoradas.</summary>
    public int SceneChangeFilterOffset { get; set; } = 250;

    /// <summary>[General] Volume (0 a 100).</summary>
    public int Volume { get; set; } = 100;

    /// <summary>[Windows] DetachedVideo (StartupDetachVideo): vídeo em janela separada; padrão dos projetos novos.</summary>
    public bool DetachedVideo { get; set; }

    /// <summary>[Windows] ShowVideo (StartupShowVideo): mostrar o vídeo; padrão dos projetos novos.</summary>
    public bool ShowVideo { get; set; }

    /// <summary>[General] Dictionnary (StartupDictionnary): dicionário padrão dos projetos novos.</summary>
    public string Dictionary { get; set; } = string.Empty;

    /// <summary>[MRUList] Recent0..7: File > Open Recent.</summary>
    public RecentFiles RecentProjects { get; } = new();

    /// <summary>[Misc] LoadMostRecentProjectOnStartup (Preferences): sem arquivo na linha de comando, abre o mais recente.</summary>
    public bool LoadMostRecentProjectOnStartup { get; set; }

    /// <summary>[Misc] CheckUpdates (Enhanced, Preferences): procura uma versão nova ao iniciar.</summary>
    public bool CheckUpdates { get; set; }

    /// <summary>[WebServer] Port (Preferences): porta do servidor web de sugestões.</summary>
    public int WebServerPort { get; set; } = 80;

    /// <summary>[WebServer] EnableCompression: deflate nas páginas quando o navegador aceita.</summary>
    public bool WebServerEnableCompression { get; set; }

    /// <summary>[Windows] MainForm_*: posição normal e estado (0 normal, 1 minimizado, 2 maximizado).</summary>
    public WindowPlacement? MainForm { get; set; }

    /// <summary>[Windows] MainForm_PanelTop_Height (forma de onda e vídeo).</summary>
    public int PanelTopHeight { get; set; } = 225;

    /// <summary>[Windows] MainForm_PanelBottom_Height (editor, ou a lista quando trocada).</summary>
    public int PanelBottomHeight { get; set; } = 104;

    /// <summary>[Windows] MemoSubtitleVO_Width.</summary>
    public int MemoSubtitleVOWidth { get; set; } = 400;

    /// <summary>[TextPipe] ShowTextPipe.</summary>
    public bool ShowTextPipe { get; set; }

    /// <summary>[TextPipe] TextPipeAutoOption (0 colorir, 1 apagar, 2 apagar tudo antes).</summary>
    public int TextPipeAutoOption { get; set; }

    /// <summary>[General] ColumnsWidth: larguras das colunas na ordem #, Start, Stop, Style, Text, VO.</summary>
    public IReadOnlyList<int> ColumnsWidth { get; set; } = [];

    /// <summary>
    /// [General] ColumnsPosition: a posição na tela de cada coluna, na mesma ordem de ColumnsWidth.
    /// Valor inválido = MaxInt (vai para o fim), como StrToIntDef(..., MaxInt).
    /// </summary>
    public IReadOnlyList<int> ColumnsPosition { get; set; } = [];

    /// <summary>
    /// GetIniFilename: VisualSubSync.ini ao lado do executável; se a pasta ou o arquivo forem só de
    /// leitura, %APPDATA%\VisualSubSync.ini.
    /// </summary>
    public static string ResolvePath(string exePath, string? userDataDirectory = null)
    {
        string local = Path.ChangeExtension(exePath, ".ini");
        return StoragePaths.CanWriteFile(local)
            ? local
            : Path.Combine(userDataDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VisualSubSync.ini");
    }

    /// <summary>Preserve a protected local INI on first use of per-user storage.</summary>
    public static VssSettings LoadForExecutable(string exePath, string? userDataDirectory = null)
    {
        string destination = ResolvePath(exePath, userDataDirectory);
        string local = Path.ChangeExtension(exePath, ".ini");
        var settings = Load(File.Exists(destination) ? destination : File.Exists(local) ? local : destination);
        settings.FileName = destination;
        return settings;
    }

    public static VssSettings Load(string path)
    {
        var settings = new VssSettings { FileName = path };
        if (File.Exists(path))
        {
            settings._ini = IniDocument.LoadAnsi(path);
        }

        var ini = settings._ini;
        settings.LoadIni(ini, isPresets: false);
        settings.QuickSmartSilentZonePanels = ini.GetBool("Misc", "QuickSmartSilentZonePanels", true);
        settings.AutoScrollWavDisplay = ini.GetBool("Misc", "AutoScrollWavDisplay", false);
        settings.AutoScrollSubtitles = ini.GetBool("Misc", "AutoScrollSubtitles", false);
        settings.RecentProjects.Load(ini);
        settings.Volume = ini.GetInt("General", "Volume", 100);
        settings.DetachedVideo = ini.GetBool("Windows", "DetachedVideo", false);
        settings.ShowVideo = ini.GetBool("Windows", "ShowVideo", false);
        settings.Dictionary = ini.GetString("General", "Dictionnary", string.Empty);
        settings.PanelTopHeight = ini.GetInt("Windows", "MainForm_PanelTop_Height", 225);
        settings.PanelBottomHeight = ini.GetInt("Windows", "MainForm_PanelBottom_Height", 104);
        settings.MemoSubtitleVOWidth = ini.GetInt("Windows", "MemoSubtitleVO_Width", 400);
        settings.ShowTextPipe = ini.GetBool("TextPipe", "ShowTextPipe", false);
        settings.TextPipeAutoOption = ini.GetInt("TextPipe", "TextPipeAutoOption", 0);
        settings.VisibleExtraColumns = DelphiText.SplitCommaText(ini.Get("General", "VisibleExtraColumns") ?? string.Empty);
        settings.HiddenByUserCoreColumns = DelphiText.SplitCommaText(ini.Get("General", "HiddenByUserCoreColumns") ?? string.Empty);
        settings.ColumnsWidth = [.. (ini.Get("General", "ColumnsWidth") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => DelphiText.StrToIntDef(DelphiText.Trim(v.Trim('"')), -1))];
        settings.ColumnsPosition = [.. DelphiText.SplitCommaText(ini.Get("General", "ColumnsPosition") ?? string.Empty)
            .Select(v => DelphiText.StrToIntDef(v, int.MaxValue))];
        if (ini.Get("Windows", "MainForm_Width") is not null)
        {
            settings.MainForm = new WindowPlacement(
                ini.GetInt("Windows", "MainForm_Left", 205),
                ini.GetInt("Windows", "MainForm_Top", 93),
                ini.GetInt("Windows", "MainForm_Width", 780),
                ini.GetInt("Windows", "MainForm_Height", 581),
                ini.GetInt("Windows", "MainForm_State", 0));
        }

        return settings;
    }

    /// <summary>
    /// TConfigObject.LoadIni: cada chave presente troca o valor atual. Num arquivo de presets o servidor
    /// web, o backup e as fontes ficam como estão.
    /// </summary>
    private void LoadIni(IniDocument ini, bool isPresets)
    {
        SwapSubtitlesList = ini.GetBool("Misc", "SwapSubtitlesList", SwapSubtitlesList);
        DisableSubtitleEdition = ini.GetBool("Misc", "DisableSubtitleEdition", DisableSubtitleEdition);
        EnableToggleCreation = ini.GetBool("Misc", "EnableToggleCreation", EnableToggleCreation);
        SpaceKeyModifyTiming = ini.GetBool("Misc", "SpaceKeyModifyTiming", SpaceKeyModifyTiming);
        SpaceKeyCpsTarget = ini.GetInt("Misc", "SpaceKeyCPSTarget", SpaceKeyCpsTarget);
        SpaceKeyMinimalDuration = ini.GetInt("Misc", "SpaceKeyMinimalDuration", SpaceKeyMinimalDuration);
        EnableMouseAntiOverlapping = ini.GetBool("Misc", "EnableMouseAntiOverlapping", EnableMouseAntiOverlapping);
        EnableMouseSnapping = ini.GetBool("Misc", "EnableMouseSnapping", EnableMouseSnapping);
        SpaceKeyBlankBetweenSubtitles = ini.GetInt("Misc", "SpaceKeyBlankBetweenSubtitles", SpaceKeyBlankBetweenSubtitles);
        MaximumSubtitleDuration = ini.GetInt("Misc", "MaximumSubtitleDuration", MaximumSubtitleDuration);
        LoadMostRecentProjectOnStartup = ini.GetBool("Misc", "LoadMostRecentProjectOnStartup", LoadMostRecentProjectOnStartup);
        CheckUpdates = ini.GetBool("Misc", "CheckUpdates", CheckUpdates);

        if (!isPresets)
        {
            WebServerPort = ini.GetInt("WebServer", "Port", WebServerPort);
            WebServerEnableCompression = ini.GetBool("WebServer", "EnableCompression", WebServerEnableCompression);
        }

        Plugins?.Load(ini);
        Hotkeys.Load(ini);

        MouseWheelTimeScrollModifier = (WheelModifier)ini.GetInt("Mouse", "WheelTimeScrollModifier", (int)MouseWheelTimeScrollModifier);
        MouseWheelVZoomModifier = (WheelModifier)ini.GetInt("Mouse", "WheelVZoomModifier", (int)MouseWheelVZoomModifier);
        MouseWheelHZoomModifier = (WheelModifier)ini.GetInt("Mouse", "WheelHZoomModifier", (int)MouseWheelHZoomModifier);
        MouseEnableSsaTimingMode = ini.GetBool("Mouse", "EnableSSATimingMode", MouseEnableSsaTimingMode);

        if (!isPresets)
        {
            EnableBackup = ini.GetBool("Backup", "EnableBackup", EnableBackup);
            AutoBackupEvery = ini.GetInt("Backup", "AutoBackupEvery", AutoBackupEvery);
            AutoSaveWhenPlaying = ini.GetBool("Backup", "AutoSaveWhenPlaying", AutoSaveWhenPlaying);
            SubListFont = FontSpec.Parse(ini.Get("Fonts", "SubList"), SubListFont);
            SubTextFont = FontSpec.Parse(ini.Get("Fonts", "SubText"), SubTextFont);
        }

        ShowSceneChange = ini.GetBool("WAVDisplay", "ShowSceneChange", ShowSceneChange);
        SceneChangeStartOffset = ini.GetInt("WAVDisplay", "SceneChangeStartOffset", SceneChangeStartOffset);
        SceneChangeStopOffset = ini.GetInt("WAVDisplay", "SceneChangeStopOffset", SceneChangeStopOffset);
        SceneChangeFilterOffset = ini.GetInt("WAVDisplay", "SceneChangeFilterOffset", SceneChangeFilterOffset);
        ShowTextInWavDisplay = ini.GetBool("WAVDisplay", "ShowTextInWAVDisplay", ShowTextInWavDisplay);
    }

    /// <summary>LoadPresetFile: um arquivo de presets por cima das preferências atuais (LoadIni(IniFile, True)).</summary>
    public void LoadPreset(string path)
    {
        LoadIni(IniDocument.Load(path), isPresets: true);
    }

    /// <summary>UpdatePluginList seguido do trecho "Error plugin" de LoadIni.</summary>
    public void LoadPlugins(PluginSettings plugins)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        plugins.Load(_ini);
        Plugins = plugins;
    }

    /// <summary>A ação [js] registrada pelo plugin geral entra na tabela de atalhos.</summary>
    public void AddJavascriptHotkey(string actionName, string caption, string defaultShortcut) =>
        Hotkeys.AddAction(actionName, "[js] " + caption, DelphiShortcut.FromText(defaultShortcut), _ini);

    /// <summary>[General] VisibleExtraColumns: os títulos das colunas extras à mostra.</summary>
    public IReadOnlyList<string> VisibleExtraColumns { get; set; } = [];

    /// <summary>[General] HiddenByUserCoreColumns: as colunas que o usuário escondeu pelo cabeçalho.</summary>
    public IReadOnlyList<string> HiddenByUserCoreColumns { get; set; } = [];

    /// <summary>LoadFormPosition: [Windows] Nome_Left/Top/Width/Height/State de uma janela auxiliar.</summary>
    public WindowPlacement? GetFormPlacement(string formName)
    {
        if (_ini.Get("Windows", formName + "_Width") is null)
        {
            return null;
        }

        return new WindowPlacement(
            _ini.GetInt("Windows", formName + "_Left", 0),
            _ini.GetInt("Windows", formName + "_Top", 0),
            _ini.GetInt("Windows", formName + "_Width", 0),
            _ini.GetInt("Windows", formName + "_Height", 0),
            _ini.GetInt("Windows", formName + "_State", 0));
    }

    /// <summary>SaveFormPosition: vai para o arquivo no próximo <see cref="Save"/>.</summary>
    public void SetFormPlacement(string formName, WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        _ini.SetInt("Windows", formName + "_Left", placement.Left);
        _ini.SetInt("Windows", formName + "_Top", placement.Top);
        _ini.SetInt("Windows", formName + "_Width", placement.Width);
        _ini.SetInt("Windows", formName + "_Height", placement.Height);
        _ini.SetInt("Windows", formName + "_State", placement.State);
    }

    /// <summary>TConfigObject.SaveIni: todas as preferências, como o original grava ao sair.</summary>
    private void SavePreferences(IniDocument ini)
    {
        ini.SetBool("Misc", "SwapSubtitlesList", SwapSubtitlesList);
        ini.SetBool("Misc", "DisableSubtitleEdition", DisableSubtitleEdition);
        ini.SetBool("Misc", "EnableToggleCreation", EnableToggleCreation);
        ini.SetBool("Misc", "SpaceKeyModifyTiming", SpaceKeyModifyTiming);
        ini.SetInt("Misc", "SpaceKeyCPSTarget", SpaceKeyCpsTarget);
        ini.SetInt("Misc", "SpaceKeyMinimalDuration", SpaceKeyMinimalDuration);
        ini.SetBool("Misc", "EnableMouseAntiOverlapping", EnableMouseAntiOverlapping);
        ini.SetBool("Misc", "EnableMouseSnapping", EnableMouseSnapping);
        ini.SetInt("Misc", "SpaceKeyBlankBetweenSubtitles", SpaceKeyBlankBetweenSubtitles);
        ini.SetInt("Misc", "MaximumSubtitleDuration", MaximumSubtitleDuration);
        ini.SetBool("Misc", "LoadMostRecentProjectOnStartup", LoadMostRecentProjectOnStartup);
        ini.SetBool("Misc", "CheckUpdates", CheckUpdates);

        ini.SetInt("WebServer", "Port", WebServerPort);
        ini.SetBool("WebServer", "EnableCompression", WebServerEnableCompression);

        Plugins?.Save(ini);
        Hotkeys.Save(ini);

        ini.SetInt("Mouse", "WheelTimeScrollModifier", (int)MouseWheelTimeScrollModifier);
        ini.SetInt("Mouse", "WheelVZoomModifier", (int)MouseWheelVZoomModifier);
        ini.SetInt("Mouse", "WheelHZoomModifier", (int)MouseWheelHZoomModifier);
        ini.SetBool("Mouse", "EnableSSATimingMode", MouseEnableSsaTimingMode);

        ini.SetBool("Backup", "EnableBackup", EnableBackup);
        ini.SetInt("Backup", "AutoBackupEvery", AutoBackupEvery);
        ini.SetBool("Backup", "AutoSaveWhenPlaying", AutoSaveWhenPlaying);

        ini.Set("Fonts", "SubList", SubListFont.ToString());
        ini.Set("Fonts", "SubText", SubTextFont.ToString());

        ini.SetBool("WAVDisplay", "ShowSceneChange", ShowSceneChange);
        ini.SetInt("WAVDisplay", "SceneChangeStartOffset", SceneChangeStartOffset);
        ini.SetInt("WAVDisplay", "SceneChangeStopOffset", SceneChangeStopOffset);
        ini.SetInt("WAVDisplay", "SceneChangeFilterOffset", SceneChangeFilterOffset);
        ini.SetBool("WAVDisplay", "ShowTextInWAVDisplay", ShowTextInWavDisplay);
    }

    /// <summary>
    /// Grava só as chaves que esta versão controla; as demais linhas do arquivo são preservadas.
    /// Como o TIniFile, o arquivo fica na página ANSI do sistema.
    /// </summary>
    public void Save()
    {
        var ini = _ini;
        if (MainForm is { } p)
        {
            ini.SetInt("Windows", "MainForm_Left", p.Left);
            ini.SetInt("Windows", "MainForm_Top", p.Top);
            ini.SetInt("Windows", "MainForm_Width", p.Width);
            ini.SetInt("Windows", "MainForm_Height", p.Height);
            ini.SetInt("Windows", "MainForm_State", p.State);
        }

        RecentProjects.Save(ini);
        SavePreferences(ini);
        ini.SetInt("Windows", "MainForm_PanelTop_Height", PanelTopHeight);
        ini.SetInt("Windows", "MainForm_PanelBottom_Height", PanelBottomHeight);
        ini.SetBool("Windows", "DetachedVideo", DetachedVideo);
        ini.SetBool("Windows", "ShowVideo", ShowVideo);
        ini.SetInt("General", "Volume", Volume);
        ini.SetInt("Windows", "MemoSubtitleVO_Width", MemoSubtitleVOWidth);
        ini.SetBool("TextPipe", "ShowTextPipe", ShowTextPipe);
        ini.SetInt("TextPipe", "TextPipeAutoOption", TextPipeAutoOption);
        ini.Set("General", "Dictionnary", Dictionary);
        ini.SetBool("Misc", "AutoScrollWavDisplay", AutoScrollWavDisplay);
        ini.SetBool("Misc", "AutoScrollSubtitles", AutoScrollSubtitles);
        ini.Set("General", "VisibleExtraColumns", DelphiText.ToCommaText(VisibleExtraColumns));
        ini.Set("General", "HiddenByUserCoreColumns", DelphiText.ToCommaText(HiddenByUserCoreColumns));
        if (ColumnsPosition.Count > 0)
        {
            ini.Set("General", "ColumnsPosition", string.Join(',', ColumnsPosition));
        }

        if (ColumnsWidth.Count > 0)
        {
            ini.Set("General", "ColumnsWidth", string.Join(',', ColumnsWidth));
        }

        ini.Save(FileName, TextFileReader.Ansi);
    }
}

/// <summary>TMouseWheelModifier: Ord(ssShift), Ord(ssAlt), Ord(ssCtrl) e mwmNone, como no ini.</summary>
public enum WheelModifier
{
    Shift = 0,
    Alt = 1,
    Ctrl = 2,
    None = 3,
}

/// <summary>Posição normal da janela em pixels (rcNormalPosition) e WindowState do Delphi.</summary>
public sealed record WindowPlacement(int Left, int Top, int Width, int Height, int State);
