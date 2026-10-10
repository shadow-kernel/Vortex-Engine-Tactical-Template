using Vortex;

// FIRST-PERSON WEAPON — Call-of-Duty-style handling for an ANIMATED viewmodel (arms + gun rigged together, e.g. the
// "First Person Animations" packs): Equip / Idle / Walk / Run / Aim_In / Aim_Out / Fire / Reload / Reload_Empty /
// Inspect / Firemode / Unequip clips played by the Animator, plus everything CoD layers on top procedurally:
//   * the viewmodel is placed so the pack's AUTHORING CAMERA node sits on the game camera — the arms frame exactly as
//     the animator framed them and the Aim_In end pose puts the sights on the screen centre (no hand-tuned offsets);
//   * ADS: aim clips (or a procedural sight offset), world FOV zoom from the weapon, own viewmodel FOV, steadier sway;
//   * recoil: the AIM climbs per shot (vertical + random horizontal drift, first-shot kick, ADS scale) and only part
//     of it recovers, the camera punches and springs back, the gun kicks back and up on its own springs;
//   * hip spread that grows with movement and sustained fire; ADS is pin-point;
//   * sprint lowers the gun (Run clip), firing is blocked until the sprint-out time has passed;
//   * muzzle flash + tracer + surface impacts (VFX), bullet-hole / blood decals, barrel smoke after sustained fire,
//     shell ejection, layered fire sound, dry fire, reload sound cues;
//   * for the views that look AT the player (debug cam, spectators) the tracers start and the brass ejects at the
//     third-person gun's sockets (PlayerRig.Tp*, published by ThirdPersonBody); the first-person flash lives on the
//     viewmodel layer, the third-person copy on layer 2 — every camera sees exactly one (#194)
//   * tactical vs empty reload (one in the chamber), inspect (I), fire mode toggle (B).
// Lives on the viewmodel root entity (the one with the Animator). WeaponLoadout equips it; only the equipped weapon
// runs. Every number here is a public field: tune it in the inspector while playing.
public class FPWeapon : VortexBehaviour
{
    // ---------------- identity / ballistics ----------------
    public string WeaponName = "UZI";
    public float  Damage = 26f;
    public float  Range = 160f;
    public float  CraterRadius = 0.45f;       // v3.4 terrain: a bullet digs a small crater into the heightfield (0 = off)
    public float  CraterDepth = 0.06f;
    public float  FireRate = 900f;          // rounds per minute
    public bool   Automatic = true;
    public bool   HasSemiMode = true;       // B toggles auto / semi
    public int    MagazineSize = 32;
    public int    ReserveAmmo = 160;
    public float  BulletImpulse = 5f;       // N·s pushed into a hit rigid body

    // ---------------- spread (degrees) ----------------
    public float HipSpread = 2.0f;
    public float ShotLoudness = 1.6f;   // how far the bots hear a shot (AI Perception loudness; footsteps are ~0.35)
    public float MoveSpread = 1.8f;         // added at full run speed
    public float AdsSpread = 0.0f;
    public float ShotSpread = 0.45f;        // per shot while hip-firing, decays
    public float SpreadRecovery = 6f;

    // ---------------- recoil ----------------
    public float RecoilUp = 0.55f;          // deg the aim climbs per shot
    public float RecoilSide = 0.30f;        // deg random horizontal per shot
    public float RecoilDrift = 0.10f;       // deg per shot of steady drift (+ right) — the "pattern" you learn
    public float FirstShotKick = 1.3f;      // multiplier on the first shot of a burst
    public float AdsRecoilScale = 0.7f;
    public float RecoilRecovery = 0.45f;    // share of the climb that returns after you stop firing
    public float RecoveryTime = 0.22f;      // seconds for that return
    public float CamPunch = 0.75f;          // visual view punch per shot (springs back)
    public float KickBack = 0.028f;         // viewmodel pushed back (m)
    public float KickPitch = 2.4f;          // viewmodel muzzle rise (deg)
    public float KickRoll = 1.6f;           // viewmodel roll wobble (deg)
    public float AdsKickScale = 0.45f;

    // ---------------- feel ----------------
    public float AdsZoom = 1.15f;           // ADS magnification of the world view: iron sights ~1.15, red dot ~1.3, ACOG ~2.5
    public float ViewmodelFov = 62f;
    public float AdsViewmodelFov = 50f;
    public float AdsTime = 0.22f;           // aim-in duration when the pack has no aim clips
    public float SwayAmount = 1.0f;         // look sway (deg per unit of mouse delta, clamped)
    public float SwaySmooth = 10f;
    public float AdsSwayScale = 0.2f;
    public float SprintOutTime = 0.18f;
    public Vector3 CameraFix = Vector3.Zero;                // authoring-camera node axes -> engine camera axes (a camera NODE from an
                                                            // FBX looks down its local X: (0,90,0); a camera BONE looks down +Z: 0)
    public Vector3 HipOffset = Vector3.Zero;                // camera-space offset (m, +X right, +Y up, +Z forward) of the hip pose:
    public Vector3 HipRotation = Vector3.Zero;              // ... and tilt (deg) — the CoD lower-right framing; both fade out in ADS
    public Vector3 AdsOffset = Vector3.Zero;                // camera-space offset while aiming: the whole sight alignment on packs
                                                            // without an Aim_In clip, a small push (rear sight off the eye) on the rest
    public float  CameraAnimScale = 1.0f;                   // how much of the pack's camera animation shakes the VIEW (0 = none)
    public float  AdsCameraAnimScale = 0.35f;               // ... while aiming

    // ---------------- sights: ADS puts the rear + front sight exactly on the line of sight (CoD) ----------------
    public string  SightBone = "";                          // bone the sights ride on (the weapon body); empty = clip/AdsOffset only
    public Vector3 SightRear = Vector3.Zero;                // bone-local rear sight (aperture / notch centre)
    public Vector3 SightFront = Vector3.Zero;               // bone-local front sight post tip
    public float   EyeRelief = 0.075f;                      // eye -> rear sight while aiming (m)

    // ---------------- animation ----------------
    public string CameraNode = "Camera";
    public string ClipIdle = "Idle", ClipWalk = "Walk", ClipRun = "Run", ClipFire = "Fire";
    public string ClipAimIn = "Aim_In", ClipAimOut = "Aim_Out";
    public string ClipReload = "Reload", ClipReloadEmpty = "Reload_Empty", ClipInspect = "Inspect";
    public string ClipEquip = "Equip", ClipUnequip = "Unequip", ClipFiremode = "Firemode";
    public float  EquipTime = 1.0f, UnequipTime = 0.6f, ReloadTime = 2.0f, ReloadEmptyTime = 3.0f, InspectTime = 5f, FiremodeTime = 0.35f;
    public float  ReloadRefillAt = 0.6f;    // share of the reload when the new magazine counts
    public float  EquipReadyAt = 0.55f;     // share of the equip clip after which the gun can fire
    public bool   HasAimClips = true;

    // ---------------- sockets (bones of the weapon rig) ----------------
    public string MuzzleBone = "";
    public Vector3 MuzzleOffset = Vector3.Zero;            // bone-local (m)
    public string EjectBone = "";
    public Vector3 EjectOffset = Vector3.Zero;

    // ---------------- audio / effects ----------------
    public string FireSound = "Assets/Audio/gun_rifle.vsndc";
    public string FireLayerSound = "Assets/Audio/gun_sub.wav";
    public string FireTailSound = "Assets/Audio/gun_tail.wav";
    public string DrySound = "Assets/Audio/dry_click.wav";
    public string AdsSound = "Assets/Audio/ads_tick.wav";
    public string FiremodeSound = "Assets/Audio/gun_action.wav";
    public string ReloadCues = "0.18:Assets/Audio/mag_out_1.wav;0.55:Assets/Audio/mag_in.vsndc";
    public string ReloadEmptyCues = "0.15:Assets/Audio/mag_out_1.wav;0.48:Assets/Audio/mag_in.vsndc;0.78:Assets/Audio/bolt_rack.wav";
    public string MuzzleVfx = "Assets/VFX/MuzzleFlash_Rifle.vfx";
    public string TracerVfx = "Assets/VFX/Tracer.vfx";
    public int    TracerEvery = 3;
    public string ImpactDefaultVfx = "Assets/VFX/Impact_Concrete.vfx";
    public string ImpactMetalVfx = "Assets/VFX/Impact_Metal.vfx";
    public string ImpactWoodVfx = "Assets/VFX/Impact_Wood.vfx";
    public string ImpactDirtVfx = "Assets/VFX/Impact_Dirt.vfx";
    public string ImpactFleshVfx = "Assets/VFX/Impact_Flesh.vfx";
    public string ShellPrefab = "Assets/Prefabs/Shell.ventity";
    // decals (#178): projected onto whatever the bullet hits (not onto rigidbodies — they move); blood lands on the
    // surface BEHIND a hit enemy
    public string DecalDefault = "Assets/Materials/Decals/BulletHole_Concrete.vmat";
    public string DecalMetal   = "Assets/Materials/Decals/BulletHole_Metal.vmat";
    public string DecalWood    = "Assets/Materials/Decals/BulletHole_Wood.vmat";
    public string DecalBlood   = "Assets/Materials/Decals/Blood_Splat.vmat";
    public float  DecalSize    = 0.085f;    // bullet hole across (m)
    public float  BloodSize    = 0.55f;     // blood splat across (m)
    public float  DecalLifetime = 60f;      // seconds (0 = forever; the newest 512 stay)
    // barrel smoke (#178): every shot heats the barrel, a hot barrel smokes while it cools
    public string BarrelSmokeVfx = "Assets/VFX/BarrelSmoke.vfx";
    public float  HeatPerShot = 0.11f;
    public float  HeatCooling = 0.28f;      // per second (faster when hotter)
    public float  SmokeAbove  = 0.4f;       // heat at which the wisps start

    // ---------------- runtime ----------------
    public bool Equipped;                   // set by WeaponLoadout
    public int  Mag = -1, Reserve = -1;
    public bool IsReady { get { return Equipped && _state == State.Idle && _equipT >= EquipReadyAt * EquipTime; } }

    private enum State { Hidden, Equip, Idle, Reload, Inspect, Firemode, Unequip }
    private State _state = State.Hidden;
    private float _stateT, _equipT;
    private bool _reloadEmpty, _refilled;
    private string _cues = ""; private int _cueIndex;
    private string _loco = "";
    private bool _adsAnim;                  // the aim clip pose is active
    private float _ads;                     // 0 hip .. 1 aimed (drives FOVs, spread, sway)
    private bool _fireHeld, _rHeld, _iHeld, _bHeld;
    private bool _semi;
    private float _cooldown, _sinceShot = 99f, _spreadKick;
    private int _burst, _shots;
    private float _climbP, _climbY;         // aim climbed during the current burst (for the partial recovery)
    private float _recoverP, _recoverY, _recoverLeft;
    private float _kz, _kzV, _kp, _kpV, _kr, _krV, _ky, _kyV;   // viewmodel kick springs
    private float _swayY, _swayP;
    private float _prevYaw, _prevPitch; private bool _hasPrev;
    private Vector3 _camPos; private Quaternion _camRot = Quaternion.Identity;           // camera node, model space, this frame
    private bool _restKnown; private Vector3 _restPos; private Quaternion _restRot = Quaternion.Identity;   // ... at rest (idle)
    private bool _sprintBlock; private float _sprintOut;
    private bool _adsPrev;
    private System.Random _rng = new System.Random(4711);

    /// <summary>Every weapon in the scene (ammo crates refill them all).</summary>
    public static readonly System.Collections.Generic.List<FPWeapon> All = new System.Collections.Generic.List<FPWeapon>();
    public override void OnDestroy() { All.Remove(this); }

    public override void Start()
    {
        if (!All.Contains(this)) All.Add(this);
        if (Mag < 0) { Mag = MagazineSize; Reserve = ReserveAmmo; }
        _semi = !Automatic;
    }

    // ================================================================= equip / holster (called by WeaponLoadout)
    public void Equip()
    {
        Equipped = true;
        Scene.SetActive(EntityId, true);
        _hasPrev = false;
        SetState(State.Equip, EquipTime);
        _equipT = 0f;
        Play(ClipEquip, 0f);
        _loco = ""; _adsAnim = false;
        PlayerRig.ActiveWeapon = this;
        PlayerRig.MagSize = MagazineSize; PlayerRig.Ammo = Mag;
    }

    public void Holster() { SetState(State.Unequip, UnequipTime); Play(ClipUnequip, 0.08f); }
    public bool HolsterDone { get { return _state == State.Unequip && _stateT >= UnequipTime; } }

    public void Hide()
    {
        Equipped = false; _state = State.Hidden;
        Scene.SetActive(EntityId, false);
        if (PlayerRig.ActiveWeapon == this) { PlayerRig.ActiveWeapon = null; PlayerRig.CamAnimRot = Quaternion.Identity; PlayerRig.CamAnimPos = Vector3.Zero; }
    }

    private void SetState(State s, float length) { _state = s; _stateT = 0f; _stateLen = length; }
    private void Play(string clip, float fade) { if (!string.IsNullOrEmpty(clip)) Animation.Play(EntityId, clip, fade); }
    private float _stateLen;

    // ================================================================= per frame
    public override void Update(float dt)
    {
        if (!Equipped || dt <= 0f) return;
        _stateT += dt; _equipT += dt; _sinceShot += dt;
        if (_cooldown > 0f) _cooldown -= dt;
        if (_heat > 0f) { _heat -= HeatCooling * dt * (0.5f + _heat); if (_heat < 0f) _heat = 0f; }
        if (_smokeT > 0f) _smokeT -= dt;
        if (_heat > SmokeAbove && _sinceShot > 0.1f && _smokeT <= 0f && BarrelSmokeVfx != "") BarrelSmoke();
        TickAutoReload(dt);
        if (_spreadKick > 0f) { _spreadKick -= SpreadRecovery * dt * (0.4f + _spreadKick); if (_spreadKick < 0f) _spreadKick = 0f; }
        PlayerRig.Ammo = Mag; PlayerRig.MagSize = MagazineSize;
        PlayerRig.ReserveAmmo = Reserve; PlayerRig.WeaponName = WeaponName; PlayerRig.FireModeSemi = _semi;
        bool menu = !Cursor.Locked;

        // ---- sprint: gun down (Run clip), no fire until the sprint-out time has passed ----
        bool sprinting = PlayerRig.IsSprinting && !PlayerRig.IsSliding;
        if (sprinting) _sprintOut = SprintOutTime; else if (_sprintOut > 0f) _sprintOut -= dt;
        PlayerRig.SprintOut = _sprintOut > 0f ? _sprintOut : 0f;

        // ---- state machine ----
        switch (_state)
        {
            case State.Equip:
                if (_stateT >= _stateLen) { SetState(State.Idle, 0f); _loco = ""; }
                break;
            case State.Reload:
                TickCues();
                if (!_refilled && _stateT >= _stateLen * ReloadRefillAt) { Refill(); _refilled = true; }
                if (_stateT >= _stateLen || PlayerRig.IsSprinting && _stateT > _stateLen * 0.9f) { SetState(State.Idle, 0f); _loco = ""; }
                break;
            case State.Inspect:
            case State.Firemode:
                if (_stateT >= _stateLen) { SetState(State.Idle, 0f); _loco = ""; }
                break;
            case State.Unequip:
                return;   // WeaponLoadout swaps when HolsterDone
        }

        // ---- input ----
        bool fire = !menu && Input.GetKey("LButton");
        bool rKey = !menu && Input.GetKey("R"), iKey = !menu && Input.GetKey("I"), bKey = !menu && Input.GetKey("B");
        bool wantAds = !menu && PlayerRig.Ads && !sprinting && !PlayerRig.Mantling;
        PlayerRig.FireHeld = fire;

        if (_state == State.Idle || _state == State.Inspect)
        {
            if (rKey && !_rHeld && Mag < MagazineSize + (Mag > 0 ? 1 : 0) && Reserve > 0 && Mag <= MagazineSize) StartReload();
            else if (iKey && !_iHeld && _state == State.Idle && !wantAds) { SetState(State.Inspect, InspectTime); Play(ClipInspect, 0.15f); _loco = "inspect"; }
            else if (bKey && !_bHeld && HasSemiMode && Automatic && _state == State.Idle)
            {
                _semi = !_semi; SetState(State.Firemode, FiremodeTime);
                Play(ClipFiremode, 0.08f); _loco = "firemode";
                if (FiremodeSound != "") Audio.PlayOneShot2D(FiremodeSound, 0.5f, 1.15f);
            }
            if (_state == State.Inspect && (fire || wantAds || sprinting)) { SetState(State.Idle, 0f); _loco = ""; }
        }
        _rHeld = rKey; _iHeld = iKey; _bHeld = bKey;

        // ---- ADS ----
        bool canAds = wantAds && (_state == State.Idle || (_state == State.Equip && _equipT > EquipReadyAt * EquipTime));
        float adsRate = dt / System.Math.Max(0.05f, AdsTime);
        _ads += canAds ? adsRate : -adsRate * 1.2f;
        if (_ads < 0f) _ads = 0f; else if (_ads > 1f) _ads = 1f;
        if (canAds != _adsPrev && AdsSound != "") Audio.PlayOneShot2D(AdsSound, 0.3f, canAds ? 1f : 0.9f);
        _adsPrev = canAds;
        PlayerRig.AdsBlend = _ads;

        // ---- locomotion / aim clips (only while idle) ----
        if (_state == State.Idle)
        {
            if (HasAimClips && canAds && !_adsAnim) { Play(ClipAimIn, 0.05f); _adsAnim = true; _loco = "aim"; }
            else if (HasAimClips && !canAds && _adsAnim) { Play(ClipAimOut, 0.05f); _adsAnim = false; _loco = "aimout"; _aimOutT = 0f; }
            if (!_adsAnim)
            {
                _aimOutT += dt;
                bool aimOutPlaying = _loco == "aimout" && _aimOutT < 0.5f;
                if (!aimOutPlaying && _sinceShot > 0.25f)
                {
                    string want = sprinting ? ClipRun : (PlayerRig.Speed > 0.6f && PlayerRig.Grounded ? ClipWalk : ClipIdle);
                    if (want != _loco) { Play(want, _loco == "" ? 0.12f : 0.22f); _loco = want; }
                }
            }
        }
        else _adsAnim = false;

        // ---- fire ----
        bool triggerPull = _semi ? (fire && !_fireHeld) : fire;
        _fireHeld = fire;
        bool canFire = (_state == State.Idle || (_state == State.Equip && _equipT >= EquipReadyAt * EquipTime))
                       && !sprinting && _sprintOut <= 0f && !PlayerRig.Mantling && !PlayerRig.Switching;
        if (triggerPull && canFire && _cooldown <= 0f)
        {
            if (Mag <= 0)
            {
                if (fire && _sinceShot > 0.2f && DrySound != "") { Audio.PlayOneShot2D(DrySound, 0.7f, 1f); _sinceShot = 0f; _cooldown = 0.25f; }
                if (Reserve > 0 && _state == State.Idle) StartReload();
            }
            else Shoot();
        }
        if (!fire || _state != State.Idle) { if (_sinceShot > 60f / System.Math.Max(1f, FireRate) * 1.6f) _burst = 0; }

        // ---- recoil recovery: part of the climb returns once you stop shooting ----
        if (_burst == 0 && (_climbP != 0f || _climbY != 0f))
        {
            _recoverP = _climbP * RecoilRecovery; _recoverY = _climbY * RecoilRecovery; _recoverLeft = RecoveryTime;
            _climbP = 0f; _climbY = 0f;
        }
        if (_recoverLeft > 0f)
        {
            float k = System.Math.Min(dt, _recoverLeft) / RecoveryTime;
            PlayerRig.AimKickPitch -= _recoverP * k; PlayerRig.AimKickYaw -= _recoverY * k;
            _recoverLeft -= dt;
        }

        // ---- spread for the crosshair ----
        float moveN = PlayerRig.Speed / 6.5f; if (moveN > 1f) moveN = 1f;
        float hip = HipSpread + MoveSpread * moveN + _spreadKick;
        PlayerRig.CurrentSpread = hip + (AdsSpread - hip) * _ads;
        PlayerRig.Firing = _sinceShot < 0.05f;
        PlayerRig.Reloading = _state == State.Reload;
    }
    private float _aimOutT = 1f;
    private float _clock;

    // ================================================================= placement (after the camera moved)
    public override void LateUpdate(float dt)
    {
        if (!Equipped || dt <= 0f || !PlayerRig.Ready) return;
        if (!ReadCameraNode()) return;
        // the camera node's rest pose = where it sits while idle (packs animate it only in the other clips)
        if (!_restKnown && _state == State.Idle) { _restPos = _camPos; _restRot = _camRot; _restKnown = true; }
        Vector3 p0 = _restKnown ? _restPos : _camPos;
        Quaternion n0 = _restKnown ? _restRot : _camRot;

        // look sway: the gun trails the turn, then settles (much steadier while aiming)
        if (_hasPrev)
        {
            float dYaw = DeltaAngle(PlayerRig.Yaw, _prevYaw), dPitch = PlayerRig.Pitch - _prevPitch;
            float scale = SwayAmount * (1f - (1f - AdsSwayScale) * _ads);
            float ty = Clamp(-dYaw * 0.5f * scale, -4f, 4f), tp = Clamp(-dPitch * 0.5f * scale, -4f, 4f);
            float k = System.Math.Min(1f, SwaySmooth * dt);
            _swayY += (ty - _swayY) * k; _swayP += (tp - _swayP) * k;
        }
        _prevYaw = PlayerRig.Yaw; _prevPitch = PlayerRig.Pitch; _hasPrev = true;

        // kick springs (critically-damped-ish)
        Spring(ref _kz, ref _kzV, 260f, 26f, dt); Spring(ref _kp, ref _kpV, 300f, 24f, dt);
        Spring(ref _kr, ref _krV, 220f, 20f, dt); Spring(ref _ky, ref _kyV, 260f, 22f, dt);

        // ADS breathing: a slow figure-eight while aimed (CoD idle sway)
        _clock += dt; float tNow = _clock;
        float breathP = (float)System.Math.Sin(tNow * 1.3f) * 0.06f * _ads, breathY = (float)System.Math.Sin(tNow * 0.65f) * 0.05f * _ads;

        // Placement: the authoring camera node AT REST sits on the game camera (plus the hip framing and every procedural
        // layer), so the arms frame exactly as the animator framed them. Whatever the node does beyond its rest in the
        // current clip (the pack's camera animation) shakes the VIEW instead — the world moves, like CoD's camera anims.
        float adsK = Smooth(_ads), hipK = 1f - adsK;
        Quaternion camQ = Quaternion.FromEuler(new Vector3(PlayerRig.Pitch, PlayerRig.Yaw, PlayerRig.Roll));
        Quaternion procQ = Quaternion.FromEuler(new Vector3(_swayP - _kp + breathP + HipRotation.X * hipK, _swayY + _ky + breathY + HipRotation.Y * hipK, _kr + HipRotation.Z * hipK));
        Vector3 procP = new Vector3(_swayY * 0.003f, _swayP * 0.003f, -_kz)
                      + HipOffset * hipK
                      + AdsOffset * adsK;
        Quaternion fix = Quaternion.FromEuler(CameraFix);
        Quaternion vmQ = camQ * procQ * fix * n0.Inverse;
        Vector3 vmP = PlayerRig.EyePos + camQ.Rotate(procP) - vmQ.Rotate(p0);
        // ADS: solve the pose that puts the sight line (rear -> front sight, from the CURRENT animated pose) on the view
        // axis with the rear sight EyeRelief ahead of the eye, and blend toward it — exact sights on any pack, aim clip or not
        Vector3 rearM, upM, dirM;
        if (adsK > 0.001f && ReadSights(out rearM, out upM, out dirM))
        {
            Quaternion adsQ = camQ * procQ * Quaternion.LookRotation(dirM, upM).Inverse;
            Vector3 adsP = PlayerRig.EyePos + camQ.Rotate(procP + new Vector3(0f, 0f, EyeRelief)) - adsQ.Rotate(rearM);
            vmQ = Quaternion.Slerp(vmQ, adsQ, adsK);
            vmP = Vector3.Lerp(vmP, adsP, adsK);
        }
        Scene.SetWorldPose(EntityId, vmP, vmQ.ToEuler());

        // camera animation (relative to the rest pose, in game-camera axes) -> CoDMovement adds it to the view next frame
        float camK = CameraAnimScale + (AdsCameraAnimScale - CameraAnimScale) * adsK;
        Quaternion toCam = fix * n0.Inverse;
        Quaternion delta = toCam * _camRot * fix.Inverse;
        PlayerRig.CamAnimRot = Quaternion.Slerp(Quaternion.Identity, delta, camK);
        PlayerRig.CamAnimPos = toCam.Rotate(_camPos - p0) * camK;

        Camera.SetViewmodelFieldOfView(ViewmodelFov + (AdsViewmodelFov - ViewmodelFov) * adsK);
        if (_dbg && (_dbgT += dt) > 0.5f)
        {
            _dbgT = 0f;
            Debug.Log("[FPWeapon] " + WeaponName + " state=" + _state + " cam=" + _camPos + " rest=" + _restKnown + " p0=" + p0 + " camAnim=" + PlayerRig.CamAnimRot.ToEuler() + " ads=" + _ads.ToString("0.00"));
        }
    }

    /// <summary>The authoring camera node (camera bone or camera node of the pack) in this entity's model space, from the
    /// pose the animation last evaluated.</summary>
    private bool ReadCameraNode()
    {
        Vector3 bp, be, ep, ee;
        if (!Animation.TryGetBoneTransform(EntityId, CameraNode, out bp, out be)) return false;
        if (!Scene.TryGetWorldPose(EntityId, out ep, out ee)) return false;
        Quaternion inv = Quaternion.FromEuler(ee).Inverse;
        _camPos = inv.Rotate(bp - ep);
        _camRot = inv * Quaternion.FromEuler(be);
        return true;
    }
    /// <summary>Rear sight, weapon up and sight direction in this entity's model space (current animated pose).</summary>
    private bool ReadSights(out Vector3 rear, out Vector3 up, out Vector3 dir)
    {
        rear = Vector3.Zero; up = Vector3.Up; dir = Vector3.Forward;
        if (string.IsNullOrEmpty(SightBone)) return false;
        Vector3 bp, be, ep, ee;
        if (!Animation.TryGetBoneTransform(EntityId, SightBone, out bp, out be)) return false;
        if (!Scene.TryGetWorldPose(EntityId, out ep, out ee)) return false;
        Quaternion inv = Quaternion.FromEuler(ee).Inverse;
        Vector3 pos = inv.Rotate(bp - ep);
        Quaternion rot = inv * Quaternion.FromEuler(be);
        rear = pos + rot.Rotate(SightRear);
        Vector3 front = pos + rot.Rotate(SightFront);
        dir = (front - rear).Normalized;
        up = rot.Rotate(Vector3.Up);
        return dir.Length > 0.5f;
    }

    private bool _dbg = System.Environment.GetEnvironmentVariable("VM_WLOG") == "1";
    private float _dbgT;

    // ================================================================= shooting
    private void Shoot()
    {
        _cooldown = 60f / System.Math.Max(1f, FireRate);
        Mag--; _shots++; _burst++; _sinceShot = 0f; PlayerRig.ShotsFired++;
        _heat += HeatPerShot; if (_heat > 1f) _heat = 1f;
        float r1 = (float)_rng.NextDouble(), r2 = (float)_rng.NextDouble();

        // sound: recorded shot (random container) + low punch + tail
        float pitch = 0.96f + 0.08f * r1;
        if (FireSound != "") Audio.PlayOneShot2D(FireSound, 0.9f, pitch);
        if (FireLayerSound != "") Audio.PlayOneShot2D(FireLayerSound, 0.5f, pitch * 0.98f);
        if (FireTailSound != "" && (_shots % 2) == 0) Audio.PlayOneShot2D(FireTailSound, 0.35f, 0.95f + 0.1f * r2);

        // hip: the fire clip; aimed: procedural only (the clip would pull the sights off the centre)
        if (_ads < 0.5f && !string.IsNullOrEmpty(ClipFire)) { Play(ClipFire, 0.02f); _loco = "fire"; _adsAnim = false; }

        // recoil: the aim climbs (pattern = up + drift + random side), camera punch, gun kick
        float adsK = 1f + (AdsRecoilScale - 1f) * _ads;
        float first = _burst == 1 ? FirstShotKick : 1f;
        float up = RecoilUp * adsK * first * (0.9f + 0.2f * r1);
        float side = (RecoilDrift + RecoilSide * (r2 * 2f - 1f)) * adsK;
        PlayerRig.AimKickPitch += up; PlayerRig.AimKickYaw += side;
        _climbP += up; _climbY += side; _recoverLeft = 0f;
        PlayerRig.CamKickPitch -= CamPunch * adsK * 6f;
        PlayerRig.CamKickYaw += side * 4f;
        float kk = 1f + (AdsKickScale - 1f) * _ads;
        _kzV += KickBack * 18f * kk; _kpV += KickPitch * 18f * kk;
        _krV += KickRoll * 14f * (r2 > 0.5f ? 1f : -1f) * kk; _kyV += side * 10f * kk;

        // ballistics: spread cone around the view, raycast, damage, push, impact
        float hipSpread = HipSpread + MoveSpread * System.Math.Min(1f, PlayerRig.Speed / 6.5f) + _spreadKick;
        float spread = (hipSpread + (AdsSpread - hipSpread) * _ads) * 0.0174532925f;
        if (_ads < 0.5f) _spreadKick += ShotSpread;
        float rr = spread * (float)System.Math.Sqrt(_rng.NextDouble()), ph = (float)(_rng.NextDouble() * 6.2831853);
        float yawRad = PlayerRig.Yaw * 0.0174532925f + rr * (float)System.Math.Cos(ph);
        float pitchRad = PlayerRig.Pitch * 0.0174532925f + rr * (float)System.Math.Sin(ph);
        float cy = (float)System.Math.Cos(yawRad), sy = (float)System.Math.Sin(yawRad);
        float cp = (float)System.Math.Cos(pitchRad), sp = (float)System.Math.Sin(pitchRad);
        Vector3 dir = new Vector3(sy * cp, -sp, cy * cp);
        Vector3 end = PlayerRig.EyePos + dir * Range;
        RaycastHit hit;
        bool didHit = Physics.Raycast(PlayerRig.EyePos, dir, Range, out hit);
        Perception.MakeNoise(PlayerRig.EyePos, ShotLoudness, EntityId);   // the bots hear every shot (AI Perception)
        if (didHit)
        {
            end = hit.Point;
            SendMessage(hit.EntityId, "damage", Damage);
            if (BulletImpulse > 0f && Physics.HasRigidbody(hit.EntityId)) Physics.AddImpulseAtPoint(hit.EntityId, dir * BulletImpulse, hit.Point);
            string tag = Scene.TagOf(hit.EntityId);
            bool flesh = tag == "Enemy" || tag == "Monster";
            if (flesh) { PlayerRig.HitMarkerT = 0.14f; PlayerRig.HitMarkerKill = false; }   // range targets mark their own hits
            string vfx = flesh ? ImpactFleshVfx : SurfaceVfx(hit.EntityId);
            if (vfx != "") Vfx.SpawnAt(vfx, hit.Point + hit.Normal * 0.01f, Quaternion.LookRotation(hit.Normal, System.Math.Abs(hit.Normal.Y) > 0.9f ? new Vector3(1f, 0f, 0f) : Vector3.Up));
            SpawnDecal(hit, dir, flesh, r1, r2);
            // v3.4 terrain: bullets dig small craters into the heightfield — the render chunks and the collision follow
            if (!flesh && CraterDepth > 0f && Terrain.IsTerrain(hit.EntityId)) Terrain.Deform(hit.Point, CraterRadius, CraterDepth);
        }

        // muzzle flash (first-person layer) + tracer from the muzzle
        Vector3 muzzle = MuzzlePosition(dir);
        if (MuzzleVfx != "") Vfx.SpawnAt(MuzzleVfx, muzzle, Quaternion.LookRotation(dir, Vector3.Up), 1f, 1);
        // the views that look at the player see the tracer leave the third-person gun, not the hidden viewmodel at the head
        bool external = Camera.IsExternalView && PlayerRig.TpSocketsValid;
        if (TracerVfx != "" && TracerEvery > 0 && (_shots % TracerEvery) == 0) Vfx.Beam(external ? PlayerRig.TpMuzzlePos : muzzle + dir * 0.4f, end, TracerVfx);

        // brass
        if (ShellPrefab != "")
        {
            Vector3 ej = external ? PlayerRig.TpEjectPos : BonePoint(EjectBone, EjectOffset, muzzle - dir * 0.25f);
            Quaternion camQ = Quaternion.FromEuler(new Vector3(PlayerRig.Pitch, PlayerRig.Yaw, 0f));
            long shell = Scene.Instantiate(ShellPrefab, ej, PlayerRig.Yaw);
            if (shell != 0) SendMessage(shell, "eject", camQ.Rotate(new Vector3(1.6f + r1, 1.4f + r2, -0.2f)));
        }
        if (Mag == 0 && Reserve > 0) { /* CoD: the next trigger pull reloads; auto-reload after a beat */ _autoReload = 0.3f; }
    }
    private float _autoReload = -1f;
    private float _heat, _smokeT;

    // A bullet hole on the surface a bullet hit (a blood splat on the surface behind an enemy): the projected decal
    // system (#120); nothing on rigidbodies — a decal is world-fixed and would float once the body moves.
    private void SpawnDecal(RaycastHit hit, Vector3 dir, bool flesh, float r1, float r2)
    {
        if (flesh)
        {
            RaycastHit behind;
            if (DecalBlood != "" && Physics.Raycast(hit.Point + dir * 0.05f, dir, 6f, out behind, ~0, hit.EntityId) && !Physics.HasRigidbody(behind.EntityId))
                Decal.Spawn(behind.Point, behind.Normal, DecalBlood, new Vector3(BloodSize * (0.7f + 0.6f * r1), BloodSize * 0.5f, BloodSize * (0.7f + 0.6f * r2)),
                    DecalLifetime, r1 * 360f, DecalBlend.Multiply, 1f, 1f, 1f, 0.9f);
            return;
        }
        if (Physics.HasRigidbody(hit.EntityId)) return;
        string vfx = SurfaceVfx(hit.EntityId);
        string material = vfx == ImpactMetalVfx ? DecalMetal : vfx == ImpactWoodVfx ? DecalWood : DecalDefault;
        if (material != "") Decal.Spawn(hit.Point, hit.Normal, material, DecalSize * (0.85f + 0.3f * r2), DecalLifetime);
    }

    // Wisps of smoke from a hot barrel, at the first-person muzzle (viewmodel layer) and the third-person one (layer 2).
    private void BarrelSmoke()
    {
        float yawRad = PlayerRig.Yaw * 0.0174532925f, pitchRad = PlayerRig.Pitch * 0.0174532925f;
        float cy = (float)System.Math.Cos(yawRad), sy = (float)System.Math.Sin(yawRad);
        float cp = (float)System.Math.Cos(pitchRad), sp = (float)System.Math.Sin(pitchRad);
        Vector3 dir = new Vector3(sy * cp, -sp, cy * cp);
        float scale = 0.6f + _heat * 0.8f;
        Vfx.SpawnAt(BarrelSmokeVfx, MuzzlePosition(dir), Quaternion.LookRotation(dir, Vector3.Up), scale, 1);
        if (PlayerRig.TpSocketsValid) Vfx.SpawnAt(BarrelSmokeVfx, PlayerRig.TpMuzzlePos, Quaternion.LookRotation(PlayerRig.TpMuzzleDir, Vector3.Up), scale, 2);
        _smokeT = 0.18f + (1f - _heat) * 0.35f;
    }

    private string SurfaceVfx(long entity)
    {
        string n = Scene.NameOf(entity).ToLowerInvariant();
        if (n.IndexOf("metal") >= 0 || n.IndexOf("cont_") >= 0 || n.IndexOf("container") >= 0 || n.IndexOf("barrel") >= 0 || n.IndexOf("steel") >= 0 || n.IndexOf("pipe") >= 0
            || n.IndexOf("car") >= 0 || n.IndexOf("plate") >= 0 || n.IndexOf("pillar") >= 0 || n.IndexOf("post") >= 0 || n.IndexOf("hinge") >= 0) return ImpactMetalVfx;
        if (n.IndexOf("wood") >= 0 || n.IndexOf("crate") >= 0 || n.IndexOf("pallet") >= 0 || n.IndexOf("target") >= 0 || n.IndexOf("plank") >= 0
            || n.IndexOf("booth") >= 0 || n.IndexOf("table") >= 0) return ImpactWoodVfx;
        if (n.IndexOf("ground") >= 0 || n.IndexOf("dirt") >= 0 || n.IndexOf("sand") >= 0 || n.IndexOf("grass") >= 0 || n.IndexOf("berm") >= 0 || n.IndexOf("dune") >= 0
            || n.IndexOf("bags") >= 0 || n.IndexOf("rock") >= 0) return ImpactDirtVfx;
        return ImpactDefaultVfx;
    }

    private Vector3 MuzzlePosition(Vector3 dir)
    {
        return BonePoint(MuzzleBone, MuzzleOffset, PlayerRig.EyePos + dir * 0.75f + new Vector3(0f, -0.06f, 0f));
    }

    private Vector3 BonePoint(string bone, Vector3 local, Vector3 fallback)
    {
        if (string.IsNullOrEmpty(bone)) return fallback;
        Vector3 p, e;
        if (!Animation.TryGetBoneTransform(EntityId, bone, out p, out e)) return fallback;
        return p + Quaternion.FromEuler(e).Rotate(local);
    }

    // ================================================================= reload
    private void StartReload()
    {
        _reloadEmpty = Mag == 0;
        float len = _reloadEmpty ? ReloadEmptyTime : ReloadTime;
        SetState(State.Reload, len);
        _refilled = false;
        Play(_reloadEmpty ? ClipReloadEmpty : ClipReload, 0.12f);
        _loco = "reload"; _adsAnim = false;
        _cues = _reloadEmpty ? ReloadEmptyCues : ReloadCues; _cueIndex = 0;
        _autoReload = -1f;
    }

    private void Refill()
    {
        // tactical reload keeps one in the chamber (CoD: 30+1)
        int cap = MagazineSize + (!_reloadEmpty && Mag > 0 ? 1 : 0);
        int need = cap - Mag; int take = need < Reserve ? need : Reserve;
        if (take < 0) take = 0;
        Mag += take; Reserve -= take;
    }

    private void TickCues()
    {
        if (string.IsNullOrEmpty(_cues)) return;
        string[] parts = _cues.Split(';');
        while (_cueIndex < parts.Length)
        {
            string c = parts[_cueIndex];
            int colon = c.IndexOf(':');
            float at;
            if (colon <= 0 || !float.TryParse(c.Substring(0, colon), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out at)) { _cueIndex++; continue; }
            if (_stateT < at * _stateLen) break;
            Audio.PlayOneShot2D(c.Substring(colon + 1), 0.8f, 1f);
            _cueIndex++;
        }
    }

    private void TickAutoReload(float dt)
    {
        if (_autoReload > 0f) { _autoReload -= dt; if (_autoReload <= 0f && Equipped && _state == State.Idle && Mag == 0 && Reserve > 0) StartReload(); }
    }

    // ================================================================= helpers
    private static void Spring(ref float x, ref float v, float k, float d, float dt)
    {
        v += (-k * x - d * v) * dt; x += v * dt;
    }
    private static float Clamp(float v, float a, float b) { return v < a ? a : (v > b ? b : v); }
    private static float Smooth(float t) { return t * t * (3f - 2f * t); }
    private static float DeltaAngle(float a, float b)
    {
        float d = (a - b) % 360f; if (d > 180f) d -= 360f; else if (d < -180f) d += 360f; return d;
    }

    /// <summary>Refill everything (ammo crates / respawn).</summary>
    public void RefillAll() { Mag = MagazineSize; Reserve = ReserveAmmo; }
}
