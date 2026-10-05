using Vortex;

// THIRD-PERSON BODY — the full character other cameras see: the debug cam (P), a mirror, a spectator, later other
// players and bots. Its meshes are on render layer 2 ("third person only"): the local first-person camera skips them,
// every other view draws them. The body is driven from the shared PlayerRig state that CoDMovement and the weapons
// publish, so it always does what the first-person player does:
//   * stands at the capsule's feet, turned with the look (yaw); the spine bends with the look pitch
//   * locomotion: idle / aim, walk, run, strafe, backwards, sprint, jump — crossfaded Mixamo rifle clips
//   * crouch and slide lower the body (Foot IK keeps the feet on the ground, so the knees bend); a slide leans back
//   * a shot plays a recoil overlay on the upper body, a reload plays the reload overlay (the support hand lets go)
//   * the third-person copy of the equipped weapon sits in the right hand, the left hand is IK'd onto its foregrip
//   * at zero health the body becomes a ragdoll
public class ThirdPersonBody : VortexBehaviour
{
    public string ClipDir   = "Assets/Characters/Operator/animations/";
    public float  RunSpeed  = 4.4f;     // walk -> run above this speed (m/s)
    public float  Fade      = 0.18f;
    public string UpperMask = "mixamorig:Spine1+";
    public float  BodyYawOffset = 0f;   // the operator rig faces +Z (the look direction at yaw 0)

    // spine follows the look up / down
    public string SpineBone0 = "mixamorig:Spine";
    public string SpineBone1 = "mixamorig:Spine1";
    public float  SpineAimGain = 0.9f;
    public float  SpineAimSign = -1f;

    // stance
    public float CrouchDrop = 0.38f;    // body lowered while crouched (Foot IK bends the knees)
    public float CrouchLean = 10f;      // spine leans forward (deg)
    public float SlideDrop  = 0.58f;
    public float SlideLean  = -28f;     // whole body leans back (deg)
    public float StanceLerp = 10f;

    // third-person weapons: child entities by slot (WeaponLoadout order) + where the LEFT hand bone sits on each one,
    // relative to the right hand bone (measured from the first-person pack's own hand placement)
    public string Gun0 = "TP_UZI";
    public string Gun1 = "TP_Scorpion";
    public Vector3 Gun0SupportPos = new Vector3(-0.0194f, 0.1293f, 0.0902f);
    public Vector3 Gun0SupportRot = new Vector3(-5.6542f, -108.7279f, 10.3684f);
    public Vector3 Gun1SupportPos = new Vector3(-0.057f, 0.2607f, 0.0576f);
    public Vector3 Gun1SupportRot = new Vector3(5.6117f, -115.5423f, 34.919f);
    public string SupportTip = "mixamorig:LeftHand";

    private string _base = "";
    private string _want = "";
    private float  _wantT;
    private int    _shotsSeen;
    private float  _fireT;
    private bool   _reloading;
    private bool   _dead;
    private int    _slot = -2;
    private long   _gun0, _gun1;
    private float  _drop, _lean, _spineLean;

    public override void Start()
    {
        _gun0 = FindChild(Gun0);
        _gun1 = FindChild(Gun1);
        Play("rifle_idle", 0f);
        _shotsSeen = PlayerRig.ShotsFired;
    }

    public override void Update(float dt)
    {
        if (!PlayerRig.Ready) return;

        if (PlayerRig.Health <= 0f)
        {
            if (!_dead) { _dead = true; ShowGuns(-1); Ragdoll.Activate(EntityId); }
            return;
        }
        if (_dead)
        {
            _dead = false;
            Ragdoll.Deactivate(EntityId);
            _slot = -2;
            Play("rifle_idle", 0f);
        }

        // ---- base locomotion clip ----
        float speed = PlayerRig.Speed, fwd = PlayerRig.MoveForwardN, right = PlayerRig.MoveRightN;
        string want;
        if (PlayerRig.Mantling || PlayerRig.IsAirborne) want = fwd < -0.3f ? "jump_back" : "jump";
        else if (PlayerRig.IsSliding)                   want = "run";
        else if (PlayerRig.IsSprinting && speed > 2f)   want = "rifle_run";
        else if (speed < 0.25f)                         want = PlayerRig.Ads ? "aim" : "rifle_idle";
        else if (System.Math.Abs(right) > System.Math.Abs(fwd) + 0.35f) want = right > 0f ? "strafe_r" : "strafe_l";
        else if (fwd < -0.3f)                           want = speed > RunSpeed ? "run_back" : "walk_back";
        else                                            want = speed > RunSpeed ? "run" : "walk";

        if (want != _base)
        {
            if (want != _want) { _want = want; _wantT = 0f; }
            _wantT += dt;
            // short dwell so one jittery frame doesn't restart a clip; jumps switch at once
            float dwell = (want == "jump" || want == "jump_back" || _base == "") ? 0f : 0.08f;
            if (_wantT >= dwell) Play(want, Fade);
        }
        else { _want = want; _wantT = 0f; }

        // crouch-walk plays the walk clip slower
        Animation.SetSpeed(EntityId, PlayerRig.IsCrouched && !PlayerRig.IsSliding && speed > 0.25f ? 0.7f : 1f);

        // ---- upper-body overlays: shot recoil, reload ----
        if (PlayerRig.ShotsFired != _shotsSeen)
        {
            _shotsSeen = PlayerRig.ShotsFired;
            if (!_reloading) { _fireT = 0.22f; PlayAnimationLayered(ClipDir + "rifle_fire.vanim", 1, UpperMask, 1f, 0.03f); }
        }
        if (_fireT > 0f) { _fireT -= dt; if (_fireT <= 0f) StopAnimationLayer(1); }

        bool reload = PlayerRig.Reloading;
        if (reload && !_reloading)
        {
            _reloading = true; _fireT = 0f; StopAnimationLayer(1);
            PlayAnimationLayered(ClipDir + "rifle_reload.vanim", 2, UpperMask, 1f, 0.15f);
            SetIkWeight(SupportTip, 0f);   // the support hand leaves the foregrip to swap the mag
        }
        else if (!reload && _reloading)
        {
            _reloading = false; StopAnimationLayer(2); SetIkWeight(SupportTip, 1f);
        }

        // ---- the weapon in the hands ----
        int slot = PlayerRig.Switching && PlayerRig.SwitchLower > 0.5f ? -1 : PlayerRig.WeaponSlot;
        if (slot != _slot) { _slot = slot; ShowGuns(slot); }
    }

    // After the movement resolved the feet this frame: stand there, face the look, lower / lean for the stance.
    public override void LateUpdate(float dt)
    {
        if (!PlayerRig.Ready || _dead) return;
        float a = System.Math.Min(1f, StanceLerp * dt);
        float drop = PlayerRig.IsSliding ? SlideDrop : PlayerRig.IsCrouched ? CrouchDrop : 0f;
        float lean = PlayerRig.IsSliding ? SlideLean : 0f;
        float spineLean = PlayerRig.IsCrouched && !PlayerRig.IsSliding ? CrouchLean : 0f;
        _drop += (drop - _drop) * a; _lean += (lean - _lean) * a; _spineLean += (spineLean - _spineLean) * a;

        Vector3 f = PlayerRig.FootPos;
        SetWorldPose(new Vector3(f.X, f.Y - _drop, f.Z), new Vector3(_lean, PlayerRig.BodyYaw + BodyYawOffset, 0f));
        // Foot IK plants the feet while crouched; a slide (and the air) lets the legs follow the clip
        Animation.SetFootIkWeight(EntityId, PlayerRig.IsSliding || PlayerRig.IsAirborne ? 0f : 1f);

        float pitch = PlayerRig.AimPitch * SpineAimGain * SpineAimSign * 0.5f;
        SetBoneAdditiveRotation(SpineBone0, new Vector3(pitch + _spineLean * 0.5f, 0f, 0f));
        SetBoneAdditiveRotation(SpineBone1, new Vector3(pitch + _spineLean * 0.5f, 0f, 0f));
    }

    private void Play(string clip, float fade)
    {
        PlayAnimation(ClipDir + clip + ".vanim", fade);
        _base = clip; _want = clip; _wantT = 0f;
    }

    private void ShowGuns(int slot)
    {
        if (_gun0 != 0) Scene.SetActive(_gun0, slot == 0);
        if (_gun1 != 0) Scene.SetActive(_gun1, slot == 1);
        if (slot == 0) Animation.SetIkOffset(EntityId, SupportTip, Gun0SupportPos, Gun0SupportRot);
        else if (slot == 1) Animation.SetIkOffset(EntityId, SupportTip, Gun1SupportPos, Gun1SupportRot);
        SetIkWeight(SupportTip, slot >= 0 && !_reloading ? 1f : 0f);
    }

    private long FindChild(string name)
    {
        long[] kids = Scene.Children(EntityId);
        for (int i = 0; kids != null && i < kids.Length; i++) if (Scene.NameOf(kids[i]) == name) return kids[i];
        return 0;
    }
}
