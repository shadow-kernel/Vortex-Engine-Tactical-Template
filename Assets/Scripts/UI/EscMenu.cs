using Vortex;

// Shared game settings that gameplay scripts read (the menu writes them live).
public static class UserSettings
{
    public static float MouseSensitivity = 0.09f;
    public static float Brightness       = 1.0f;   // multiplies HorrorAtmosphere exposure
    public static float MasterVolume     = 0.9f;
    public static float SfxVolume        = 1.0f;
    public static float MusicVolume      = 0.8f;
    public static float Fov              = 90f;   // HORIZONTAL degrees (CoD-style); CoDMovement converts per aspect
    public static float RenderScale      = 1.0f;
    public static bool  VSync            = false;
    public static bool  Fullscreen       = false;
    public static bool  AdsToggle        = true;   // true = press RMB once to aim, again to lower; false = hold
    public static int   ResIndex         = 2;   // 1280/1600/1920/2560
    public static int   DlssMode         = 0;   // 0 Off · 1 Quality · 2 Balanced · 3 Performance · 4 Ultra
    public static int   FrameGen         = 0;   // 0 Off · 1 x2 · 2 x3 · 3 x4
}

// The ESC menu, built from the engine's own retained UI. Every screen is its own .vui asset, so each one
// opens in the editor as the single, uncluttered thing it is:
//
//   PauseMenu.vui        the pause screen
//   Options.vui          the settings FRAME — title, tab rail, BACK (no tab content)
//   OptionsGeneral.vui   one screen per tab, laid over the frame's content area
//   OptionsGraphics.vui
//   OptionsDisplay.vui
//   OptionsAudio.vui
//   OptionsControls.vui
//
// The frame and the active tab are shown together: VuiStack routes input top-first and only a blocksInput
// screen stops it, so the tab screens are non-blocking and clicks fall through to the rail underneath.
//
// Buttons are wired by their "Click action" in the editor, routed to PauseMenuActions / OptionsActions.
// This file owns the state and the value plumbing only — it draws nothing.
public static class EscMenu
{
    public static bool IsOpen;

    private const int TabCount = 5;
    private static readonly string[] TabScreens =
        { "OptionsGeneral.vui", "OptionsGraphics.vui", "OptionsDisplay.vui", "OptionsAudio.vui", "OptionsControls.vui" };

    private static VuiHandle _pause, _frame;
    private static readonly VuiHandle[] _tabs = new VuiHandle[TabCount];
    private static int _view;            // 0 = pause menu · 1 = settings
    private static int _tab;             // active settings tab
    private static int _shownView = -1, _shownTab = -1;
    private static bool _loaded;
    private static readonly int[] ResW = { 1280, 1600, 1920, 2560 };
    private static readonly int[] ResH = { 720, 900, 1080, 1440 };

    // ---------------------------------------------------------------- public surface (CoDMovement + actions)
    public static void HandleToggle()
    {
        if (!IsOpen) { IsOpen = true; _view = 0; }
        else if (_view == 1) _view = 0;   // ESC in Options backs out to the pause menu
        else IsOpen = false;              // ESC on the pause menu resumes
    }
    public static void Resume() { IsOpen = false; _view = 0; }
    public static void Close() { Resume(); }
    public static void OpenOptions() { _view = 1; }
    public static void CloseOptions() { _view = 0; }
    public static void ShowTab(int index) { if (index >= 0 && index < TabCount) _tab = index; }

    public static void Tick(float dt)
    {
        EnsureLoaded();
        SyncScreens();
        if (!IsOpen) return;
        if (_view == 1) ReadSettings();
        ApplyAll();
    }

    // ---------------------------------------------------------------- screens
    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _pause = Gui.Load("PauseMenu.vui");
        _frame = Gui.Load("Options.vui");
        for (int i = 0; i < TabCount; i++) _tabs[i] = Gui.Load(TabScreens[i]);
        PushSettingsToWidgets();
        RefreshDlssRows();
    }

    // Show exactly what the current state asks for, and only when it changes.
    private static void SyncScreens()
    {
        int view = IsOpen ? _view : -1;
        if (view == _shownView && (view != 1 || _tab == _shownTab)) return;

        if (_pause != null) { if (view == 0) _pause.Show(); else _pause.Hide(); }
        if (_frame != null) { if (view == 1) _frame.Show(); else _frame.Hide(); }
        for (int i = 0; i < TabCount; i++)
        {
            if (_tabs[i] == null) continue;
            if (view == 1 && i == _tab) _tabs[i].Show(); else _tabs[i].Hide();
        }
        // The accent bar next to the active rail entry.
        if (_frame != null)
            for (int i = 0; i < TabCount; i++) _frame.SetVisible("tab" + i + "Marker", i == _tab);

        if (view == 1 && _tab != _shownTab) RefreshDlssRows();
        _shownView = view; _shownTab = _tab;
    }

    // ---------------------------------------------------------------- widgets -> settings
    private static void ReadSettings()
    {
        var general = _tabs[0]; var graphics = _tabs[1]; var display = _tabs[2];
        var audio = _tabs[3]; var controls = _tabs[4];

        if (_tab == 0 && Valid(general))
        {
            UserSettings.Fov = general.GetSlider("fovSlider");
            general.SetText("fovLabel", "Field of View    " + Round(UserSettings.Fov));
            UserSettings.MouseSensitivity = general.GetSlider("sensSlider");
            general.SetText("sensLabel", "Mouse Sensitivity    " + Two(UserSettings.MouseSensitivity));
            UserSettings.AdsToggle = general.GetToggle("adsToggle");
            UserSettings.Brightness = general.GetSlider("briSlider");
            general.SetText("briLabel", "Brightness    " + Two(UserSettings.Brightness));
        }
        else if (_tab == 1 && Valid(graphics))
        {
            UserSettings.RenderScale = graphics.GetSlider("rsSlider");
            graphics.SetText("rsLabel", "Render Scale    " + Pct(UserSettings.RenderScale));
            if (Settings.DlssSupported)
            {
                UserSettings.DlssMode = graphics.GetStep("dlssStepper");
                UserSettings.FrameGen = graphics.GetStep("fgStepper");
            }
            UserSettings.VSync = graphics.GetToggle("vsToggle");
            graphics.SetText("fpsLabel", "Real FPS    " + Settings.CurrentFps);
        }
        else if (_tab == 2 && Valid(display))
        {
            UserSettings.ResIndex = display.GetStep("resStepper");
            UserSettings.Fullscreen = display.GetToggle("fsToggle");
            UserSettings.VSync = display.GetToggle("vs2Toggle");
        }
        else if (_tab == 3 && Valid(audio))
        {
            UserSettings.MasterVolume = audio.GetSlider("mvSlider");
            UserSettings.SfxVolume = audio.GetSlider("sfxSlider");
            UserSettings.MusicVolume = audio.GetSlider("musSlider");
            audio.SetText("mvLabel", "Master Volume    " + Pct(UserSettings.MasterVolume));
            audio.SetText("sfxLabel", "Effects Volume    " + Pct(UserSettings.SfxVolume));
            audio.SetText("musLabel", "Music Volume    " + Pct(UserSettings.MusicVolume));
        }
        else if (_tab == 4 && Valid(controls))
        {
            UserSettings.MouseSensitivity = controls.GetSlider("sens2Slider");
            controls.SetText("sens2Label", "Mouse Sensitivity    " + Two(UserSettings.MouseSensitivity));
        }

        // V-Sync and sensitivity each have a row on two tabs: keep the other copy in step.
        Mirror();
    }

    // Sensitivity lives on General and Controls, V-Sync on Graphics and Display — whichever the user just
    // moved is the truth; push it to the twin so switching tabs never shows a stale value.
    private static void Mirror()
    {
        if (Valid(_tabs[0]) && _tab != 0) { _tabs[0].SetValue("sensSlider", UserSettings.MouseSensitivity);
            _tabs[0].SetText("sensLabel", "Mouse Sensitivity    " + Two(UserSettings.MouseSensitivity)); }
        if (Valid(_tabs[4]) && _tab != 4) { _tabs[4].SetValue("sens2Slider", UserSettings.MouseSensitivity);
            _tabs[4].SetText("sens2Label", "Mouse Sensitivity    " + Two(UserSettings.MouseSensitivity)); }
        if (Valid(_tabs[1]) && _tab != 1) _tabs[1].SetValue("vsToggle", UserSettings.VSync ? 1f : 0f);
        if (Valid(_tabs[2]) && _tab != 2) _tabs[2].SetValue("vs2Toggle", UserSettings.VSync ? 1f : 0f);
    }

    // DLSS / frame generation only exist on a capable GPU.
    private static void RefreshDlssRows()
    {
        if (!Valid(_tabs[1])) return;
        bool dlss = Settings.DlssSupported;
        _tabs[1].SetVisible("dlssRow", dlss);
        _tabs[1].SetVisible("fgRow", dlss);
        _tabs[1].SetVisible("dlssUnsupported", !dlss);
    }

    // Open showing the truth, not the authored defaults.
    private static void PushSettingsToWidgets()
    {
        if (Valid(_tabs[0]))
        {
            _tabs[0].SetValue("fovSlider", UserSettings.Fov);
            _tabs[0].SetValue("sensSlider", UserSettings.MouseSensitivity);
            _tabs[0].SetValue("briSlider", UserSettings.Brightness);
            _tabs[0].SetValue("adsToggle", UserSettings.AdsToggle ? 1f : 0f);
        }
        if (Valid(_tabs[1]))
        {
            _tabs[1].SetValue("rsSlider", UserSettings.RenderScale);
            _tabs[1].SetValue("dlssStepper", UserSettings.DlssMode);
            _tabs[1].SetValue("fgStepper", UserSettings.FrameGen);
            _tabs[1].SetValue("vsToggle", UserSettings.VSync ? 1f : 0f);
        }
        if (Valid(_tabs[2]))
        {
            _tabs[2].SetValue("resStepper", UserSettings.ResIndex);
            _tabs[2].SetValue("fsToggle", UserSettings.Fullscreen ? 1f : 0f);
            _tabs[2].SetValue("vs2Toggle", UserSettings.VSync ? 1f : 0f);
        }
        if (Valid(_tabs[3]))
        {
            _tabs[3].SetValue("mvSlider", UserSettings.MasterVolume);
            _tabs[3].SetValue("sfxSlider", UserSettings.SfxVolume);
            _tabs[3].SetValue("musSlider", UserSettings.MusicVolume);
        }
        if (Valid(_tabs[4])) _tabs[4].SetValue("sens2Slider", UserSettings.MouseSensitivity);
    }

    private static bool Valid(VuiHandle h) { return h != null && h.IsValid; }
    private static string Round(float v) { return ((int)System.Math.Round((double)v)).ToString(); }
    private static string Pct(float v) { return ((int)System.Math.Round((double)(v * 100f))).ToString() + "%"; }
    private static string Two(float v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }

    // ---------------------------------------------------------------- apply to engine (only on change)
    private static float _aFov = -1, _aRs = -1, _aMv = -1, _aSfx = -1, _aMus = -1;
    private static bool _aVs, _aFs, _aInit;
    private static int _aRes = -1, _aDlss = -1, _aFg = -1;
    private static void ApplyAll()
    {
        if (!_aInit)
        {
            _aInit = true; _aVs = !UserSettings.VSync; _aFs = !UserSettings.Fullscreen;
            // the sliders start at the volumes the game already plays at (a shipped game restores the player's saved
            // choice at start) — writing the defaults above here used to overwrite them the first time
            UserSettings.MasterVolume = _aMv = Audio.GetBusVolume("Master");
            UserSettings.SfxVolume = _aSfx = Audio.GetBusVolume("SFX");
            UserSettings.MusicVolume = _aMus = Audio.GetBusVolume("Music");
        }
        if (Chg(ref _aFov, UserSettings.Fov, 0.1f)) Settings.SetFieldOfView(CoDMovement.VerticalFov(UserSettings.Fov));
        if (Chg(ref _aRs, UserSettings.RenderScale, 0.005f)) Settings.SetRenderScale(UserSettings.RenderScale);
        if (Chg(ref _aMv, UserSettings.MasterVolume, 0.005f)) Audio.SetBusVolume("Master", UserSettings.MasterVolume);
        if (Chg(ref _aSfx, UserSettings.SfxVolume, 0.005f)) Audio.SetBusVolume("SFX", UserSettings.SfxVolume);
        if (Chg(ref _aMus, UserSettings.MusicVolume, 0.005f)) Audio.SetBusVolume("Music", UserSettings.MusicVolume);
        if (_aVs != UserSettings.VSync) { _aVs = UserSettings.VSync; Settings.SetVSync(UserSettings.VSync); }
        if (_aFs != UserSettings.Fullscreen) { _aFs = UserSettings.Fullscreen; Settings.SetFullscreen(UserSettings.Fullscreen); }
        if (_aRes != UserSettings.ResIndex && UserSettings.ResIndex >= 0 && UserSettings.ResIndex < ResW.Length)
        { _aRes = UserSettings.ResIndex; Settings.SetResolution(ResW[UserSettings.ResIndex], ResH[UserSettings.ResIndex]); }
        if (_aDlss != UserSettings.DlssMode) { _aDlss = UserSettings.DlssMode; Settings.SetDlssMode(UserSettings.DlssMode); }
        if (_aFg != UserSettings.FrameGen) { _aFg = UserSettings.FrameGen; Settings.SetFrameGenMode(UserSettings.FrameGen); }
    }
    private static bool Chg(ref float last, float cur, float eps) { if (System.Math.Abs(cur - last) > eps) { last = cur; return true; } return false; }
}
