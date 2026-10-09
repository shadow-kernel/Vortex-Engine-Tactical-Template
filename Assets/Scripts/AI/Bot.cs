using Vortex;

// COMBAT BOT — the enemy operator of the kill house. The decisions (engage / investigate / search / patrol) live in
// Assets/AI/Bot.vbt and drive the Nav Agent and AI Perception through the engine's built-in tasks; this script is the
// soldier's body and trigger finger:
//   * locomotion clips from the agent's real speed, the aim pose while engaged, a masked fire overlay per shot
//   * cover: on every "bot.engage" it samples navmesh points around itself and keeps the first that breaks the
//     player's line of sight — the tree runs there, holds, peeks, repeats
//   * aim error: a reaction time before the first shot, a spread that tightens while the target stays in view,
//     bursts with gaps — three presets (recruit / regular / veteran) in Difficulty
//   * shots are raycasts ("damage" to whoever is hit, like the player's weapons), with a muzzle sound and a noise
//     every other bot hears; hits on the bot flinch the spine (additive), at zero health the body ragdolls with the
//     bullet's impulse and despawns later
// All of it is a project script — the engine ticks the tree, moves the agent and senses; the game decides what it means.
public class Bot : VortexBehaviour
{
    public string TreePath   = "Assets/AI/Bot.vbt";
    public string ClipDir    = "Assets/Characters/Operator/animations/";
    public string Difficulty = "regular";   // recruit | regular | veteran (also sets the fields below unless Custom)
    public bool   Custom     = false;       // true = keep the hand-edited values below
    public float  MaxHealth  = 100f;
    public float  Damage     = 11f;
    public float  ReactionTime = 0.55f;     // seconds from first sight to the first shot
    public float  SpreadStart = 6f;         // degrees of aim error when the shooting starts…
    public float  SpreadEnd   = 2f;         // …tightening to this…
    public float  TightenSeconds = 2f;      // …over this many seconds of continuous sight
    public int    BurstShots  = 3;
    public float  BurstInterval = 0.09f;
    public float  BurstGap    = 0.65f;
    public float  FireRange   = 45f;
    public float  RunSpeed    = 3.2f;
    public float  Fade        = 0.18f;
    public string ShotSound   = "Assets/Audio/rifle_shot_1.wav";
    public float  DespawnAfter = 10f;
    public string UpperMask   = "mixamorig:Spine1+";

    private float _hp, _flinch, _sight, _nextShot, _despawn;
    private int _burstLeft;
    private bool _engaged, _dead;
    private string _clip = "", _state = "";
    private long _target;
    private Vector3 _lastHitFrom;
    private static readonly System.Random _rng = new System.Random();

    public override void Start()
    {
        ApplyPreset();
        _hp = MaxHealth;
        if (!BehaviorTree.IsRunning(EntityId)) BehaviorTree.Run(EntityId, TreePath);
        PlayClip("rifle_idle");
    }

    private void ApplyPreset()
    {
        if (Custom) return;
        switch ((Difficulty ?? "").ToLowerInvariant())
        {
            case "recruit": ReactionTime = 0.9f; SpreadStart = 9f; SpreadEnd = 3.5f; TightenSeconds = 2.5f; Damage = 8f; BurstGap = 0.9f; break;
            case "veteran": ReactionTime = 0.3f; SpreadStart = 4f; SpreadEnd = 1.2f; TightenSeconds = 1.5f; Damage = 14f; BurstGap = 0.45f; break;
            default:        ReactionTime = 0.55f; SpreadStart = 6f; SpreadEnd = 2f; TightenSeconds = 2f; Damage = 11f; BurstGap = 0.65f; break;
        }
    }

    public override void Update(float dt)
    {
        if (_dead)
        {
            _despawn -= dt;
            if (_despawn <= 0f) Scene.Destroy(EntityId);
            return;
        }

        // ---- locomotion from the agent's velocity ----
        Vector3 v = Navigation.Velocity(EntityId);
        float speed = (float)System.Math.Sqrt(v.X * v.X + v.Z * v.Z);
        string want = speed < 0.25f ? (_engaged ? "rifle_aim" : "rifle_idle") : (speed < RunSpeed ? "walk" : "rifle_run");
        if (want != _clip) PlayClip(want);

        // ---- flinch: an additive spine kick that decays ----
        if (_flinch > 0f)
        {
            _flinch = System.Math.Max(0f, _flinch - dt * 5f);
            SetBoneAdditiveRotation("mixamorig:Spine1", new Vector3(-18f * _flinch, 0f, 0f));
            if (_flinch <= 0f) ClearBoneOverrides();
        }

        // ---- shooting while the target is in view ----
        if (!_engaged) { _sight = 0f; return; }
        if (_target == 0) _target = BlackboardTarget();
        if (_target == 0 || !Perception.CanSee(EntityId, _target)) { _sight = 0f; return; }
        _sight += dt;
        Vector3 me = WorldPosition, tp = Scene.WorldPositionOf(_target);
        float dx = tp.X - me.X, dz = tp.Z - me.Z;
        float dist = (float)System.Math.Sqrt(dx * dx + dz * dz);
        if (speed < 0.25f && dist > 0.01f) Rotation = new Vector3(0f, (float)(System.Math.Atan2(dx, dz) * 180.0 / System.Math.PI), 0f);
        if (dist > FireRange || _sight < ReactionTime) return;
        _nextShot -= dt;
        if (_nextShot > 0f) return;
        if (_burstLeft <= 0) _burstLeft = BurstShots;
        Fire(me, tp, dist);
        _burstLeft--;
        _nextShot = _burstLeft > 0 ? BurstInterval : BurstGap + (float)_rng.NextDouble() * 0.3f;
    }

    private void Fire(Vector3 me, Vector3 target, float dist)
    {
        // aim error: the tracking time since first sight tightens the spread from SpreadStart to SpreadEnd
        float t = TightenSeconds <= 0f ? 1f : System.Math.Min(1f, System.Math.Max(0f, (_sight - ReactionTime) / TightenSeconds));
        float spreadDeg = SpreadStart + (SpreadEnd - SpreadStart) * t;
        Vector3 muzzle = new Vector3(me.X, me.Y + 1.55f, me.Z);
        Vector3 aim = new Vector3(target.X, target.Y + 1.25f, target.Z);
        Vector3 dir = Norm(aim - muzzle);
        // jitter the aim point sideways / up by up to spread degrees
        float err = (float)System.Math.Tan(spreadDeg * System.Math.PI / 180.0) * System.Math.Max(dist, 1f);
        Vector3 right = Norm(new Vector3(dir.Z, 0f, -dir.X));
        float r1 = (float)(_rng.NextDouble() * 2 - 1), r2 = (float)(_rng.NextDouble() * 2 - 1);
        aim = aim + right * (err * r1) + new Vector3(0f, err * r2 * 0.6f, 0f);
        dir = Norm(aim - muzzle);

        RaycastHit hit;
        if (Physics.Raycast(muzzle, dir, FireRange + 5f, out hit) && hit.EntityId == _target)
            SendMessage(hit.EntityId, "damage", Damage);
        PlayAnimationLayered(ClipDir + "rifle_fire.vanim", 1, UpperMask, 1f, 0.03f);
        if (ShotSound != "") Audio.PlayOneShot(ShotSound, muzzle, 0.75f);
        Perception.MakeNoise(muzzle, 1.4f, EntityId);   // the other bots hear it
    }

    // ---- the tree talks: SendMessage nodes in Bot.vbt; the player's weapons: "damage" ----
    public override void OnMessage(string message, object arg)
    {
        switch (message)
        {
            case "damage":
                if (_dead) return;
                _hp -= arg is float ? (float)arg : 20f;
                _flinch = 1f;
                long player = Scene.Find("Player");
                if (player != 0) _lastHitFrom = Scene.WorldPositionOf(player);
                if (_hp <= 0f) Die();
                break;
            case "bot.engage":
                SetState("engage");
                _engaged = true; _target = BlackboardTarget(); _nextShot = 0f; _burstLeft = 0;
                PickCover();
                break;
            case "bot.investigate": SetState("investigate"); _engaged = false; break;
            case "bot.search":      SetState("search");      _engaged = false; break;
            case "bot.patrol":      SetState("patrol");      _engaged = false; break;
        }
    }

    /// <summary>A navmesh point near the bot that the player cannot see — written to the blackboard as CoverPoint.</summary>
    private void PickCover()
    {
        var bb = BehaviorTree.BlackboardOf(EntityId);
        if (bb == null || _target == 0) return;
        Vector3 me = WorldPosition, tp = Scene.WorldPositionOf(_target);
        Vector3 eye = new Vector3(tp.X, tp.Y + 1.5f, tp.Z);
        for (int i = 0; i < 14; i++)
        {
            Vector3 p;
            if (!Navigation.RandomPoint(me, 8f, out p)) continue;
            float dx = p.X - tp.X, dz = p.Z - tp.Z;
            if (dx * dx + dz * dz < 5f * 5f) continue;                       // not toward the player's feet
            Vector3 head = new Vector3(p.X, p.Y + 1.4f, p.Z);
            if (Perception.HasLineOfSight(eye, head, _target)) continue;   // still visible from there
            bb.Set("CoverPoint", p);
            return;
        }
        bb.Remove("CoverPoint");   // nothing hides: fight from here
    }

    private void Die()
    {
        _dead = true;
        _despawn = DespawnAfter;
        SetState("dead");
        PlayerRig.HitMarkerKill = true;
        BehaviorTree.Stop(EntityId);
        Navigation.Stop(EntityId);
        ClearBoneOverrides();
        Vector3 me = WorldPosition;
        Vector3 away = Norm(new Vector3(me.X - _lastHitFrom.X, 0.35f, me.Z - _lastHitFrom.Z));
        Ragdoll.Activate(EntityId, away * 180f, new Vector3(me.X, me.Y + 1.2f, me.Z));
    }

    private void PlayClip(string clip) { _clip = clip; PlayAnimation(clip, Fade); }
    private void SetState(string s) { if (s != _state) { _state = s; Debug.Log("Bot " + EntityId + ": " + s); } }

    private long BlackboardTarget()
    {
        var bb = BehaviorTree.BlackboardOf(EntityId);
        return bb != null ? bb.GetEntity("Target") : 0;
    }

    private static Vector3 Norm(Vector3 v)
    {
        float l = (float)System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        return l > 1e-6f ? new Vector3(v.X / l, v.Y / l, v.Z / l) : new Vector3(0f, 0f, 1f);
    }
}
