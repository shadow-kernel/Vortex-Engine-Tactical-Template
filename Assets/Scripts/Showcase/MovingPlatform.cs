using Vortex;

// KINEMATIC MOVER (v3.1 Physics v2, #362): a platform that glides between its start and start + Travel. Its Rigidbody
// is KINEMATIC, so the Jolt world moves the collider with it, characters standing on it ride along and anything in the
// way is pushed. Edit Travel / Period / Phase in the inspector — or give it a bigger box and let the bots path over it.
public class MovingPlatform : VortexBehaviour
{
    public Vector3 Travel = new Vector3(4f, 0f, 0f);   // offset from the start position (m)
    public float   Period = 6f;                         // seconds for a full there-and-back
    public float   Phase  = 0f;                         // 0..1 — stagger several platforms

    private Vector3 _start;
    private float _t;

    public override void Start()
    {
        _start = Scene.WorldPositionOf(EntityId);
    }

    public override void Update(float dt)
    {
        _t += dt;
        double period = System.Math.Max(0.1f, Period);
        float s = 0.5f - 0.5f * (float)System.Math.Cos((_t / period + Phase) * 2.0 * System.Math.PI);
        Scene.SetWorldPositionOf(EntityId, _start + Travel * s);
    }
}
