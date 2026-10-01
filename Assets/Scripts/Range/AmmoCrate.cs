using Vortex;

// AMMO RESUPPLY: look at the crate and press E (tagged "Interactable", the Interactor sends "interact") — every
// carried weapon is refilled (magazine + reserve), like the ammo boxes in CoD's gun range.
public class AmmoCrate : VortexBehaviour
{
    public string Sound = "Assets/Audio/mag_in.vsndc";
    public float  Cooldown = 1.0f;
    private float _cd;

    public override void Update(float dt) { if (_cd > 0f) _cd -= dt; }

    public override void OnMessage(string message, object arg)
    {
        if (message != "interact" || _cd > 0f) return;
        _cd = Cooldown;
        for (int i = 0; i < FPWeapon.All.Count; i++) FPWeapon.All[i].RefillAll();
        if (Sound != "") Audio.PlayOneShot2D(Sound, 0.8f, 1f);
        PlayerRig.Toast = "AMMO REFILLED"; PlayerRig.ToastT = 1.6f;
    }
}
