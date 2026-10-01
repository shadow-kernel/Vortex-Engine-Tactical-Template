using Vortex;

// LOADOUT — the weapons you carry and the CoD swap: 1 / 2 pick a slot, the mouse wheel or Y cycles. The current gun
// plays its Unequip clip, then the next one its Equip clip (no firing in between). Weapons are viewmodel entities in
// the scene (each with an FPWeapon behaviour); list their entity names in slot order.
// Lives on the Player entity.
public class WeaponLoadout : VortexBehaviour
{
    public string Slots = "Viewmodel_UZI,Viewmodel_Scorpion";   // entity names, primary first
    public int StartSlot = 0;

    private long[] _ids = new long[0];
    private FPWeapon[] _weapons = new FPWeapon[0];
    private int _active = -1, _pending = -1;
    private bool _k1, _k2, _kY;
    private bool _started;

    public int ActiveSlot { get { return _active; } }

    public override void Update(float dt)
    {
        if (!_started) { Init(); return; }
        if (_weapons.Length == 0) return;

        // finish a swap: holster done -> hide it, equip the next
        if (_pending >= 0)
        {
            FPWeapon cur = _active >= 0 ? _weapons[_active] : null;
            if (cur == null || cur.HolsterDone)
            {
                if (cur != null) cur.Hide();
                _active = _pending; _pending = -1;
                _weapons[_active].Equip();
                PlayerRig.WeaponSlot = _active;
            }
            PlayerRig.Switching = _pending >= 0;
            return;
        }
        PlayerRig.Switching = false;
        if (!Cursor.Locked) return;

        bool k1 = Input.GetKey("D1"), k2 = Input.GetKey("D2"), kY = Input.GetKey("Y") || Input.GetGamepadButton("Y");
        int want = -1;
        if (k1 && !_k1) want = 0;
        if (k2 && !_k2) want = 1;
        if (kY && !_kY) want = (_active + 1) % _weapons.Length;
        float wheel = Input.ScrollDelta;
        if (wheel > 0.5f) want = (_active + _weapons.Length - 1) % _weapons.Length;
        else if (wheel < -0.5f) want = (_active + 1) % _weapons.Length;
        _k1 = k1; _k2 = k2; _kY = kY;
        if (want >= 0 && want < _weapons.Length && want != _active) SwitchTo(want);
    }

    public void SwitchTo(int slot)
    {
        if (slot < 0 || slot >= _weapons.Length || _weapons[slot] == null) return;
        _pending = slot;
        PlayerRig.Switching = true;
        if (_active >= 0) _weapons[_active].Holster();
    }

    private void Init()
    {
        string[] names = (Slots ?? "").Split(',');
        var ids = new System.Collections.Generic.List<long>();
        var ws = new System.Collections.Generic.List<FPWeapon>();
        for (int i = 0; i < names.Length; i++)
        {
            string n = names[i].Trim();
            if (n == "") continue;
            long id = Scene.Find(n);
            FPWeapon w = id != 0 ? Scene.GetBehaviour<FPWeapon>(id) : null;
            if (w == null) continue;
            ids.Add(id); ws.Add(w);
        }
        _ids = ids.ToArray(); _weapons = ws.ToArray();
        if (_weapons.Length == 0) { _started = true; return; }   // nothing to carry (yet)
        for (int i = 0; i < _weapons.Length; i++) _weapons[i].Hide();
        _active = -1;
        int start = StartSlot < _weapons.Length ? StartSlot : 0;
        _active = start; _weapons[start].Equip();
        PlayerRig.WeaponSlot = start;
        _started = true;
    }
}
