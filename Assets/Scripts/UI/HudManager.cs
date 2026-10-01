using Vortex;

// Modern-shooter HUD (Call of Duty layout): compass strip top-centre, minimap top-left, weapon panel bottom-right
// (name, big magazine count, reserve, a round-by-round strip, fire mode), a dynamic crosshair that opens with the
// weapon's spread and fades out while aiming, hit markers, the "RELOAD" / "LOW AMMO" prompt and a slim health bar
// bottom-left. Everything reads PlayerRig, which the movement and weapon scripts fill. One entity.
public class HudManager : VortexBehaviour
{
    public float MapRange = 40f;     // metres shown across the minimap
    public float MapSize = 200f;     // minimap size (px)
    public bool  ShowCompass = true;

    private float _t;
    private static readonly Color White = Color.Rgba(242, 244, 247, 250);
    private static readonly Color Dim = Color.Rgba(170, 176, 186, 220);
    private static readonly Color Shadow = Color.Rgba(0, 0, 0, 130);
    private static readonly Color Accent = Color.Rgba(255, 196, 64, 245);

    public override void Update(float dt)
    {
        float W = UI.Width, H = UI.Height; if (W < 10f) return;
        _t += dt;
        if (PlayerRig.HitMarkerT > 0f) PlayerRig.HitMarkerT -= dt;
        DrawCrosshair(W, H);
        DrawWeapon(W, H);
        DrawHealth(W, H);
        DrawMiniMap();
        if (ShowCompass) DrawCompass(W);
        DrawRangeStats(W);
        DrawToast(W, H, dt);
    }

    // ---------------------------------------------------------------- gun range stats (top-right) + notices
    private void DrawRangeStats(float W)
    {
        int shots = PlayerRig.ShotsFired, hits = PlayerRig.TargetHits;
        if (shots == 0 && hits == 0) return;
        float x = W - 236f, y = 24f, w = 212f;
        UI.Rect(x - 2f, y - 2f, w + 4f, 74f, Color.Rgba(0, 0, 0, 90), 6f);
        UI.Rect(x, y, w, 70f, Color.Rgba(18, 22, 26, 170), 6f);
        UI.Text("GUN RANGE", x + 12f, y + 6f, 190f, 16f, 11f, Accent, 0, 800);
        int acc = shots > 0 ? (int)System.Math.Round(100.0 * System.Math.Min(hits, shots) / shots) : 0;
        Stat("HITS", hits.ToString(), x + 12f, y + 26f);
        Stat("ACC", acc + "%", x + 82f, y + 26f);
        Stat("DOWN", PlayerRig.TargetsDown.ToString(), x + 148f, y + 26f);
    }

    private void Stat(string label, string value, float x, float y)
    {
        UI.Text(value, x, y, 64f, 24f, 18f, White, 0, 800);
        UI.Text(label, x, y + 24f, 64f, 14f, 9.5f, Dim, 0, 700);
    }

    private void DrawToast(float W, float H, float dt)
    {
        if (PlayerRig.ToastT <= 0f || string.IsNullOrEmpty(PlayerRig.Toast)) return;
        PlayerRig.ToastT -= dt;
        float a = System.Math.Min(1f, PlayerRig.ToastT / 0.3f);
        UI.Text(PlayerRig.Toast, W * 0.5f - 200f, H * 0.62f, 400f, 26f, 16f, Accent.WithAlpha(a), 1, 800);
    }

    // ---------------------------------------------------------------- crosshair + hit marker
    private void DrawCrosshair(float W, float H)
    {
        float cx = W * 0.5f, cy = H * 0.5f;
        if (PlayerRig.HitMarkerT > 0f)
        {
            float a = System.Math.Min(1f, PlayerRig.HitMarkerT / 0.08f);
            Color mc = PlayerRig.HitMarkerKill ? Color.Rgba(255, 64, 56, (int)(250 * a)) : Color.Rgba(255, 255, 255, (int)(240 * a));
            float i = 6f, o = 14f;
            UI.Line(cx - i, cy - i, cx - o, cy - o, mc, 2.2f); UI.Line(cx + i, cy - i, cx + o, cy - o, mc, 2.2f);
            UI.Line(cx - i, cy + i, cx - o, cy + o, mc, 2.2f); UI.Line(cx + i, cy + i, cx + o, cy + o, mc, 2.2f);
        }
        float vis = 1f - PlayerRig.AdsBlend * 2.2f;   // gone halfway into the aim, like CoD
        if (vis <= 0f || PlayerRig.IsSprinting || PlayerRig.Switching || PlayerRig.Reloading || PlayerRig.ActiveWeapon == null) return;
        float gap = 7f + PlayerRig.CurrentSpread * (H / 75f);
        float len = 8f, th = 2f;
        int al = (int)(225 * vis);
        Color line = Color.Rgba(245, 245, 245, al), sh = Color.Rgba(0, 0, 0, (int)(110 * vis));
        Bar(cx - gap - len, cy - th / 2f, len, th, line, sh); Bar(cx + gap, cy - th / 2f, len, th, line, sh);
        Bar(cx - th / 2f, cy - gap - len, th, len, line, sh); Bar(cx - th / 2f, cy + gap, th, len, line, sh);
    }

    private static void Bar(float x, float y, float w, float h, Color c, Color shadow)
    {
        UI.Rect(x - 1f, y - 1f, w + 2f, h + 2f, shadow); UI.Rect(x, y, w, h, c);
    }

    // ---------------------------------------------------------------- weapon panel (bottom-right)
    private void DrawWeapon(float W, float H)
    {
        if (PlayerRig.ActiveWeapon == null) return;
        int mag = PlayerRig.Ammo, size = PlayerRig.MagSize < 1 ? 1 : PlayerRig.MagSize, reserve = PlayerRig.ReserveAmmo;
        float right = W - 36f, bottom = H - 30f;

        // round strip: one tick per round in the magazine (CoD's bullet bar), empty ticks dimmed
        int ticks = size > 60 ? 60 : size;
        float tw = 3f, tg = 2f, stripW = ticks * (tw + tg);
        float sx = right - stripW, sy = bottom - 12f;
        for (int i = 0; i < ticks; i++)
        {
            bool full = i < (int)System.Math.Ceiling(mag * (ticks / (float)size));
            UI.Rect(sx + i * (tw + tg), sy, tw, 9f, full ? Color.Rgba(242, 244, 247, 235) : Color.Rgba(242, 244, 247, 55));
        }

        // big magazine number | reserve
        Color mc = mag == 0 ? Color.Rgba(255, 70, 60, 250) : (mag <= size / 4 ? Accent : White);
        if (mag == 0) mc = mc.WithAlpha(0.55f + 0.45f * (float)System.Math.Abs(System.Math.Sin(_t * 6.0)));
        UI.Text(reserve.ToString(), right - 70f, bottom - 50f, 70f, 26f, 20f, Dim, 2, 700);
        UI.Text(mag.ToString(), right - 190f, bottom - 74f, 112f, 56f, 48f, mc, 2, 800);
        UI.Rect(right - 74f, bottom - 62f, 2f, 34f, Color.Rgba(242, 244, 247, 120));

        // weapon name + fire mode
        string name = (PlayerRig.WeaponName ?? "").ToUpperInvariant();
        UI.Text(name, right - 260f, bottom - 100f, 260f, 22f, 15f, White, 2, 800);
        string mode = PlayerRig.FireModeSemi ? "SEMI" : "AUTO";
        UI.Text(mode, right - 260f, bottom - 80f, 180f, 18f, 11.5f, Dim, 2, 700);

        // reload prompt (centre, below the crosshair)
        if (!PlayerRig.Reloading && (mag == 0 || mag <= size / 5))
        {
            string msg = mag == 0 ? (reserve > 0 ? "RELOAD" : "NO AMMO") : "LOW AMMO";
            Color pc = mag == 0 ? Color.Rgba(255, 80, 70, 240) : Accent;
            pc = pc.WithAlpha(0.6f + 0.4f * (float)System.Math.Abs(System.Math.Sin(_t * 4.0)));
            UI.Text(msg, W * 0.5f - 120f, H * 0.5f + 46f, 240f, 22f, 15f, pc, 1, 800);
            if (mag == 0 && reserve > 0) UI.Text("[R]", W * 0.5f - 120f, H * 0.5f + 66f, 240f, 18f, 11f, Dim, 1, 700);
        }
    }

    // ---------------------------------------------------------------- health (bottom-left, slim)
    private void DrawHealth(float W, float H)
    {
        float max = PlayerRig.MaxHealth < 1f ? 100f : PlayerRig.MaxHealth;
        float frac = PlayerRig.Health / max; if (frac < 0f) frac = 0f; else if (frac > 1f) frac = 1f;
        float x = 36f, y = H - 42f, w = 220f, h = 6f;
        UI.Rect(x - 1f, y - 1f, w + 2f, h + 2f, Shadow, 2f);
        UI.Rect(x, y, w, h, Color.Rgba(255, 255, 255, 40), 2f);
        Color c = frac > 0.5f ? Color.Rgba(242, 244, 247, 235) : (frac > 0.25f ? Accent : Color.Rgba(255, 70, 60, 245));
        UI.Rect(x, y, w * frac, h, c, 2f);
        UI.Text(((int)PlayerRig.Health).ToString(), x, y - 26f, 80f, 22f, 16f, White, 0, 800);
        if (frac < 0.35f)
        {
            // CoD low-health vignette (red edges)
            int a = (int)(90 * (1f - frac / 0.35f) * (0.7f + 0.3f * (float)System.Math.Sin(_t * 3.0)));
            Color v = Color.Rgba(150, 0, 0, a);
            UI.Rect(0f, 0f, W, 40f, v); UI.Rect(0f, H - 40f, W, 40f, v); UI.Rect(0f, 0f, 40f, H, v); UI.Rect(W - 40f, 0f, 40f, H, v);
        }
    }

    // ---------------------------------------------------------------- compass (top-centre)
    private void DrawCompass(float W)
    {
        float cw = 520f, cx = W * 0.5f, y = 22f;
        UI.Rect(cx - cw * 0.5f, y + 18f, cw, 1.5f, Color.Rgba(255, 255, 255, 60));
        float yaw = PlayerRig.Yaw; while (yaw < 0f) yaw += 360f; yaw %= 360f;
        float pxPerDeg = cw / 120f;   // 120° visible
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        for (int d = 0; d < 360; d += 15)
        {
            float delta = d - yaw; if (delta > 180f) delta -= 360f; else if (delta < -180f) delta += 360f;
            if (delta < -60f || delta > 60f) continue;
            float x = cx + delta * pxPerDeg;
            float fade = 1f - System.Math.Abs(delta) / 60f;
            if (d % 45 == 0)
            {
                string n = names[d / 45];
                UI.Text(n, x - 20f, y - 2f, 40f, 18f, n.Length == 1 ? 15f : 12f, (n == "N" ? Accent : White).WithAlpha(0.35f + 0.65f * fade), 1, 800);
            }
            else UI.Rect(x - 0.75f, y + 10f, 1.5f, 7f, Color.Rgba(255, 255, 255, (int)(170 * fade)));
        }
        UI.Rect(cx - 1f, y + 15f, 2f, 9f, Accent);
        UI.Text(((int)yaw).ToString(), cx - 30f, y + 24f, 60f, 16f, 11f, Dim, 1, 700);
    }

    // ---------------------------------------------------------------- minimap (top-left)
    private void DrawMiniMap()
    {
        float ms = MapSize, mx = 24f, my = 24f;
        UI.Rect(mx - 2f, my - 2f, ms + 4f, ms + 4f, Color.Rgba(0, 0, 0, 110), 6f);
        UI.Rect(mx, my, ms, ms, Color.Rgba(18, 22, 26, 185), 6f);
        float cx = mx + ms * 0.5f, cy = my + ms * 0.5f;
        float scale = (ms * 0.5f - 8f) / MapRange;
        Vector3 p = PlayerRig.EyePos;
        double yawRad = PlayerRig.Yaw * System.Math.PI / 180.0;
        // grid rings
        UI.Rect(cx - 0.5f, my + 6f, 1f, ms - 12f, Color.Rgba(255, 255, 255, 18));
        UI.Rect(mx + 6f, cy - 0.5f, ms - 12f, 1f, Color.Rgba(255, 255, 255, 18));
        DrawBlips("Enemy", Color.Rgba(255, 70, 60, 240), p, yawRad, cx, cy, scale, ms);
        DrawBlips("Target", Color.Rgba(255, 180, 60, 220), p, yawRad, cx, cy, scale, ms);
        Color pc = Accent;
        UI.Line(cx, cy - 8f, cx - 5.5f, cy + 6f, pc, 2.4f);
        UI.Line(cx, cy - 8f, cx + 5.5f, cy + 6f, pc, 2.4f);
        UI.Line(cx - 5.5f, cy + 6f, cx + 5.5f, cy + 6f, pc, 2.4f);
    }

    private void DrawBlips(string tag, Color col, Vector3 p, double yawRad, float cx, float cy, float scale, float ms)
    {
        long[] ents = Scene.FindByTag(tag);
        if (ents == null) return;
        float s = (float)System.Math.Sin(yawRad), c = (float)System.Math.Cos(yawRad);
        float lim = ms * 0.5f - 6f;
        for (int i = 0; i < ents.Length; i++)
        {
            Vector3 ep = Scene.PositionOf(ents[i]);
            float dx = ep.X - p.X, dz = ep.Z - p.Z;
            float bx = (dx * c - dz * s) * scale, by = -(dx * s + dz * c) * scale;
            if (bx < -lim || bx > lim || by < -lim || by > lim) continue;
            UI.Rect(cx + bx - 2.5f, cy + by - 2.5f, 5f, 5f, col, 2.5f);
        }
    }
}
