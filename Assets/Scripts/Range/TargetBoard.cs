using Vortex;

// FIRING-RANGE TARGET (Call of Duty gun range): a pop-up silhouette on a spring hinge — enough damage knocks it flat,
// it pops back up a few seconds later — or a STEEL PLATE that swings and rings on every hit and never falls.
// Lives on the target's PIVOT entity: the hinge at the bottom edge, which also carries the collider (+ a kinematic
// rigidbody so the collider tilts with it); the visible board is a child. Shots reach it as "damage" messages from
// the weapon; a RangeDrill can "raise" / "lower" it. Hits and knock-downs are counted for the range HUD.
public class TargetBoard : VortexBehaviour
{
    public bool   Steel = false;          // steel plate: swings + rings, never falls
    public float  Health = 60f;           // damage that knocks a pop-up down
    public float  ResetTime = 3.0f;       // seconds until it pops up again (0 = stays down until raised)
    public float  FallAngle = -88f;       // hinge pitch when down (deg)
    public float  FallTime = 0.16f;
    public float  RiseTime = 0.32f;
    public bool   StartDown = false;      // drill targets wait for "raise"
    public string HitSound = "Assets/Audio/Range/target_hit.wav";
    public string SteelSound = "Assets/Audio/Range/steel_ding.wav";
    public string FallSound = "Assets/Audio/Range/target_fall.wav";
    public string RiseSound = "Assets/Audio/Range/target_rise.wav";

    public bool IsUp { get { return _state == TState.Up; } }

    private enum TState { Up, Falling, Down, Rising }
    private TState _state = TState.Up;
    private float _hp, _t, _angle, _swing, _swingV;
    private Vector3 _baseRot;
    private System.Random _rng;

    public override void Start()
    {
        _hp = Health;
        _baseRot = Rotation;
        _rng = new System.Random((int)(EntityId & 0x7fffffff));
        if (StartDown) { _state = TState.Down; _angle = FallAngle; _t = -1f; Apply(); }
    }

    public override void OnMessage(string message, object arg)
    {
        if (message == "damage") Hit(arg is float ? (float)arg : 20f);
        else if (message == "raise") Raise();
        else if (message == "lower") { if (_state == TState.Up || _state == TState.Rising) Fall(false); _t = -1f; }
    }

    private void Hit(float damage)
    {
        PlayerRig.TargetHits++;
        if (Steel)
        {
            // ring + swing: the plate hangs on its top chain — kick it back, a damped spring swings it out
            _swingV += 140f + (float)_rng.NextDouble() * 60f;
            Audio.PlayOneShot(SteelSound, Position, 0.9f, 0.94f + 0.12f * (float)_rng.NextDouble());
            PlayerRig.HitMarkerT = 0.14f; PlayerRig.HitMarkerKill = false;
            return;
        }
        if (_state != TState.Up && _state != TState.Rising) return;   // already going down: the shot passes through the hole
        Audio.PlayOneShot(HitSound, Position, 0.8f, 0.9f + 0.2f * (float)_rng.NextDouble());
        _hp -= damage;
        bool kill = _hp <= 0f;
        PlayerRig.HitMarkerT = 0.14f; PlayerRig.HitMarkerKill = kill;
        if (kill) { PlayerRig.TargetsDown++; Fall(true); }
    }

    private void Fall(bool sound)
    {
        _state = TState.Falling; _t = 0f;
        if (sound) Audio.PlayOneShot(FallSound, Position, 0.8f, 1f);
    }

    public void Raise()
    {
        if (_state == TState.Up || _state == TState.Rising) return;
        _state = TState.Rising; _t = 0f; _hp = Health;
        Audio.PlayOneShot(RiseSound, Position, 0.7f, 1f);
    }

    public override void Update(float dt)
    {
        if (dt <= 0f) return;
        switch (_state)
        {
            case TState.Falling:
                _t += dt;
                {
                    float k = System.Math.Min(1f, _t / FallTime);
                    _angle = FallAngle * k * k;                                  // accelerates like it is hinged
                    if (k >= 1f) { _state = TState.Down; _t = 0f; _swingV = -60f; }   // small bounce on the stop
                }
                break;
            case TState.Down:
                if (_t >= 0f) { _t += dt; if (ResetTime > 0f && _t >= ResetTime) Raise(); }
                _angle = FallAngle;
                break;
            case TState.Rising:
                _t += dt;
                {
                    float k = System.Math.Min(1f, _t / RiseTime);
                    float e = 1f - (1f - k) * (1f - k);                         // spring snaps it up, slows at the top
                    _angle = FallAngle * (1f - e);
                    if (k >= 1f) { _state = TState.Up; _angle = 0f; _swingV += 90f; }   // overshoot wobble
                }
                break;
        }
        // damped swing on top (steel plates live on it; pop-ups wobble after a stop)
        _swingV += (-180f * _swing - 9f * _swingV) * dt; _swing += _swingV * dt;
        Apply();
    }

    private void Apply()
    {
        float swing = Steel ? _swing * 0.25f : _swing * 0.05f;
        Rotation = new Vector3(_baseRot.X + _angle + swing, _baseRot.Y, _baseRot.Z);
    }
}
