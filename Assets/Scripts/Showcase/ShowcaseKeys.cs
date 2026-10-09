using Vortex;

// FEATURE TOUR KEYS — the Range doubles as the engine's showcase: every milestone has a station you can see, shoot and
// fiddle with (README ▸ "Feature tour"). These keys flip the switches you cannot reach from the scene itself. Lives on
// the "Showcase" entity; every value is editable in the inspector.
public class ShowcaseKeys : VortexBehaviour
{
    public bool   ShowHelpOnStart = true;
    public string BotPrefab      = "Assets/Prefabs/Bot.ventity";
    public float  FogDensity     = 0.03f;     // F8: the volumetric fog density (per metre) while on
    public float  FogAnisotropy  = 0.6f;
    public float  FogSunShafts   = 0.3f;
    public float  FogLights      = 1.4f;
    public float  HelpSeconds    = 7f;

    private bool _physicsDebug, _aiDebug, _fog = true;

    public override void Start()
    {
        if (ShowHelpOnStart) Help();
    }

    public override void Update(float dt)
    {
        if (Input.GetKeyDown("H")) Help();

        // v3.1 Physics v2 — Jolt bodies, joints, contacts and character capsules as lines
        if (Input.GetKeyDown("F3"))
        {
            _physicsDebug = !_physicsDebug;
            Physics.DebugDraw(_physicsDebug);
            Toast(_physicsDebug ? "Physics debug ON — Jolt bodies, joints, contacts (F3)" : "Physics debug OFF (F3)");
        }
        // v3.1 Physics v2 — the player on Jolt's CharacterVirtual instead of the built-in capsule mover
        if (Input.GetKeyDown("F4"))
        {
            string cur = Physics.CharacterController ?? "";
            bool jolt = cur.ToLowerInvariant().IndexOf("jolt") >= 0;
            Physics.SetCharacterController(jolt ? "builtin" : "jolt");
            Toast(jolt ? "Player controller: BUILT-IN capsule (F4)" : "Player controller: JOLT CharacterVirtual (F4) — stairs, slopes, pushing crates");
        }
        // v3.2 AI & Navigation — drop another combat bot where you look; it joins the navmesh, the perception and the tree
        if (Input.GetKeyDown("F6"))
        {
            float yawRad = PlayerRig.Yaw * 0.0174532925f, pitchRad = PlayerRig.Pitch * 0.0174532925f;
            float cy = (float)System.Math.Cos(yawRad), sy = (float)System.Math.Sin(yawRad);
            float cp = (float)System.Math.Cos(pitchRad), sp = (float)System.Math.Sin(pitchRad);
            Vector3 dir = new Vector3(sy * cp, -sp, cy * cp);
            RaycastHit hit;
            Vector3 at = Physics.Raycast(PlayerRig.EyePos, dir, 60f, out hit) ? hit.Point + hit.Normal * 0.1f : PlayerRig.EyePos + dir * 8f;
            long bot = Scene.Instantiate(BotPrefab, at, PlayerRig.Yaw + 180f);
            Toast(bot != 0 ? "Bot spawned (F6) — it patrols, hears your shots and engages from cover" : "Bot prefab missing: " + BotPrefab);
        }
        // v3.2 AI & Navigation — the navmesh, the agents' paths and the vision cones as overlays
        if (Input.GetKeyDown("F7"))
        {
            _aiDebug = !_aiDebug;
            Navigation.DebugDraw(_aiDebug, _aiDebug);
            Perception.DebugDraw(_aiDebug);
            Toast(_aiDebug ? "AI debug ON — navmesh, agent paths, vision cones (F7)" : "AI debug OFF (F7)");
        }
        // v3.3 VFX — the volumetric fog on / off (the Environment panel holds the authored values)
        if (Input.GetKeyDown("F8"))
        {
            _fog = !_fog;
            if (_fog) Atmosphere.SetVolumetricFog(FogDensity, FogAnisotropy, 60f, 0.45f, 6f, 0.3f, FogSunShafts, FogLights, 0.35f, 24, true);
            else Atmosphere.ClearVolumetricFog();
            Toast(_fog ? "Volumetric fog ON — look into the lamps and the sun for shafts (F8)" : "Volumetric fog OFF (F8)");
        }
    }

    private void Help()
    {
        Toast("FEATURE TOUR  H help · F3 physics debug · F4 Jolt character · F6 spawn bot · F7 AI debug · F8 volumetric fog · P debug cam");
        PlayerRig.ToastT = HelpSeconds;
    }

    private void Toast(string text)
    {
        PlayerRig.Toast = text;
        PlayerRig.ToastT = 3.5f;
    }
}
