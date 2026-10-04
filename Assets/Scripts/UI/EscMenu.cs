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

// The ESC menu, built from the engine's own retained UI: Assets/UI/PauseMenu.vui and Assets/UI/Options.vui.
// Open both in the editor's UI editor to change the layout, colours or wording — this file only wires the
// widgets to UserSettings and to the engine, it draws nothing itself.
//
// ESC frees the mouse and routes input to the menu; it does not hard-freeze the game. Driven by CoDMovement:
// EscMenu.HandleToggle() on the ESC edge, EscMenu.Tick() every frame.
public static class EscMenu
{
    public static bool IsOpen;

    private static VuiHandle _pause, _options;
    private static int _view;        // 0 = pause menu · 1 = settings
    private static int _tab;         // 0 General · 1 Graphics · 2 Display · 3 Audio · 4 Controls
    private static bool _shown;      // which screen is currently on screen (-1 = none)
    private static int _shownView = -1;
    private static readonly int[] ResW = { 1280, 1600, 1920, 2560 };
    private static readonly int[] ResH = { 720, 900, 1080, 1440 };
    private static readonly string[] TabPanels = { "tabGeneral", "tabGraphics", "tabDisplay", "tabAudio", "tabControls" };

    public static void HandleToggle()
    {
        if (!IsOpen) { IsOpen = true; _view = 0; }
        else if (_view == 1) _view = 0;   // ESC in Options backs out to the pause menu
        else IsOpen = false;              // ESC on the pause menu resumes
    }
    public static void Resume() { IsOpen = false; _view = 0; }
    public static void Close() { Resume(); }

    public static void Tick(float dt)
    {
        EnsureLoaded();
        SyncVisibility();
        if (!IsOpen) return;

        if (_view == 0) TickPause();
        else            TickOptions();
        ApplyAll();
    }

    // ---------------------------------------------------------------- screens
    private static void EnsureLoaded()
    {
        if (_pause == null) _pause = Gui.Load("PauseMenu.vui");
        if (_options == null) { _options = Gui.Load("Options.vui"); PushSettingsToWidgets(); }
    }

    // Show exactly the screen the current state asks for, and only when it changes.
    private static void SyncVisibility()
    {
        int want = IsOpen ? _view : -1;
        if (want == _shownView) return;
        _shownView = want;
        if (_pause != null) { if (want == 0) _pause.Show(); else _pause.Hide(); }
        if (_options != null)
        {
            if (want == 1) { _options.Show(); ApplyTab(); RefreshDlssRows(); }
            else _options.Hide();
        }
        _shown = want >= 0;
    }

    private static void TickPause()
    {
        if (_pause == null || !_pause.IsValid) return;
        if (_pause.WasClicked("resumeButton")) Resume();
        else if (_pause.WasClicked("optionsButton")) _view = 1;
        else if (_pause.WasClicked("quitButton")) Application.Quit();
    }

    private static void TickOptions()
    {
        if (_options == null || !_options.IsValid) return;

        if (_options.WasClicked("backButton")) { _view = 0; return; }
        for (int i = 0; i < TabPanels.Length; i++)
            if (_options.WasClicked("tab" + i + "Button")) { _tab = i; ApplyTab(); }

        // Widgets -> settings. Each row also refreshes its own label, like the authored default text.
        UserSettings.Fov = _options.GetSlider("fovSlider");
        _options.SetText("fovLabel", "Field of View    " + Round(UserSettings.Fov));

        float sens = _tab == 4 ? _options.GetSlider("sens2Slider") : _options.GetSlider("sensSlider");
        UserSettings.MouseSensitivity = sens;
        _options.SetText("sensLabel", "Mouse Sensitivity    " + Two(sens));
        _options.SetText("sens2Label", "Mouse Sensitivity    " + Two(sens));

        UserSettings.AdsToggle = _options.GetToggle("adsToggle");

        UserSettings.Brightness = _options.GetSlider("briSlider");
        _options.SetText("briLabel", "Brightness    " + Two(UserSettings.Brightness));

        UserSettings.RenderScale = _options.GetSlider("rsSlider");
        _options.SetText("rsLabel", "Render Scale    " + Pct(UserSettings.RenderScale));

        if (Settings.DlssSupported)
        {
            UserSettings.DlssMode = _options.GetStep("dlssStepper");
            UserSettings.FrameGen = _options.GetStep("fgStepper");
        }
        _options.SetText("fpsLabel", "Real FPS    " + Settings.CurrentFps);

        // V-Sync has a row on both the Graphics and the Display tab: whichever the user just moved wins.
        bool vsA = _options.GetToggle("vsToggle"), vsB = _options.GetToggle("vs2Toggle");
        if (vsA != UserSettings.VSync) UserSettings.VSync = vsA;
        else if (vsB != UserSettings.VSync) UserSettings.VSync = vsB;
        _options.SetValue("vsToggle", UserSettings.VSync ? 1f : 0f);
        _options.SetValue("vs2Toggle", UserSettings.VSync ? 1f : 0f);

        UserSettings.ResIndex = _options.GetStep("resStepper");
        UserSettings.Fullscreen = _options.GetToggle("fsToggle");

        UserSettings.MasterVolume = _options.GetSlider("mvSlider");
        UserSettings.SfxVolume = _options.GetSlider("sfxSlider");
        UserSettings.MusicVolume = _options.GetSlider("musSlider");
        _options.SetText("mvLabel", "Master Volume    " + Pct(UserSettings.MasterVolume));
        _options.SetText("sfxLabel", "Effects Volume    " + Pct(UserSettings.SfxVolume));
        _options.SetText("musLabel", "Music Volume    " + Pct(UserSettings.MusicVolume));
    }

    // Only the active tab's panel is visible; the rail marker follows it.
    private static void ApplyTab()
    {
        if (_options == null) return;
        for (int i = 0; i < TabPanels.Length; i++)
        {
            _options.SetVisible(TabPanels[i], i == _tab);
            _options.SetVisible("tab" + i + "Marker", i == _tab);
        }
    }

    // DLSS / frame generation only exist on a capable GPU.
    private static void RefreshDlssRows()
    {
        if (_options == null) return;
        bool dlss = Settings.DlssSupported;
        _options.SetVisible("dlssLabel", dlss);
        _options.SetVisible("dlssStepper", dlss);
        _options.SetVisible("fgLabel", dlss);
        _options.SetVisible("fgStepper", dlss);
        _options.SetVisible("dlssUnsupported", !dlss);
    }

    // Push the current UserSettings into the freshly loaded widgets, so the menu opens showing the truth.
    private static void PushSettingsToWidgets()
    {
        if (_options == null) return;
        _options.SetValue("fovSlider", UserSettings.Fov);
        _options.SetValue("sensSlider", UserSettings.MouseSensitivity);
        _options.SetValue("sens2Slider", UserSettings.MouseSensitivity);
        _options.SetValue("briSlider", UserSettings.Brightness);
        _options.SetValue("rsSlider", UserSettings.RenderScale);
        _options.SetValue("mvSlider", UserSettings.MasterVolume);
        _options.SetValue("sfxSlider", UserSettings.SfxVolume);
        _options.SetValue("musSlider", UserSettings.MusicVolume);
        _options.SetValue("adsToggle", UserSettings.AdsToggle ? 1f : 0f);
        _options.SetValue("vsToggle", UserSettings.VSync ? 1f : 0f);
        _options.SetValue("vs2Toggle", UserSettings.VSync ? 1f : 0f);
        _options.SetValue("fsToggle", UserSettings.Fullscreen ? 1f : 0f);
        _options.SetValue("resStepper", UserSettings.ResIndex);
        _options.SetValue("dlssStepper", UserSettings.DlssMode);
        _options.SetValue("fgStepper", UserSettings.FrameGen);
        ApplyTab();
    }

    // ---------------------------------------------------------------- formatting
    private static string Round(float v) { return ((int)System.Math.Round((double)v)).ToString(); }
    private static string Pct(float v) { return ((int)System.Math.Round((double)(v * 100f))).ToString() + "%"; }
    private static string Two(float v) { return v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture); }

    // ---------------------------------------------------------------- apply to engine (only on change)
    private static float _aFov = -1, _aRs = -1, _aMv = -1, _aSfx = -1, _aMus = -1;
    private static bool _aVs, _aFs, _aInit;
    private static int _aRes = -1, _aDlss = -1, _aFg = -1;
    private static void ApplyAll()
    {
        if (!_aInit) { _aInit = true; _aVs = !UserSettings.VSync; _aFs = !UserSettings.Fullscreen; }
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
