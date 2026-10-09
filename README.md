<div align="center">

<img src="preview.png" alt="Vortex Tactical Shooter" width="640"/>

# Vortex Engine — Tactical Shooter Template

### A Call-of-Duty-style first-person shooter foundation for the **[Vortex Engine](https://github.com/shadow-kernel/Vortex-Engine)**.

<br/>

[![Vortex Engine](https://img.shields.io/badge/POWERED%20BY-Vortex%20Engine-6C5CE7?style=for-the-badge)](https://github.com/shadow-kernel/Vortex-Engine)
[![License](https://img.shields.io/badge/CODE-MIT-3DA639?style=for-the-badge&logo=opensourceinitiative&logoColor=white)](LICENSE)
[![Assets](https://img.shields.io/badge/ASSETS-CC--BY%20%2F%20CC0-00B894?style=for-the-badge)](ATTRIBUTIONS.md)

<br/>

**A desert gun range in the afternoon sun. Five booths, pop-ups at 10, 25 and 50 metres, steel ringing at 75.**

Press Play and you stand in booth 3 with an animated UZI in your hands: aim down the iron sights, walk the
recoil down onto the steel, switch to the CZ Scorpion, clear the kill house. Weapon handling, movement and HUD are
built to feel like Call of Duty — and every bit of it is a plain project script you can read and change.

</div>

---

## 🎮 Controls

| Input | Action |
|-------|--------|
| **WASD** + mouse | move + look (CoD acceleration / friction) |
| **Shift** | sprint — double-tap **W** for tactical sprint. The gun lowers; firing waits for the sprint-out |
| **Ctrl / C** | crouch — while sprinting: **slide** |
| **Space** | jump — into chest-high cover it **mantles** you up |
| **LMB** | fire · **B** toggles AUTO / SEMI |
| **RMB** / **Left Alt** | aim down sights. Toggle by default (works on a trackpad / Magic Mouse); **ESC ▸ Aim** switches to hold |
| **R** | reload — tactical (one stays in the chamber: 32+1) or empty (bolt) |
| **I** | inspect the weapon |
| **1 / 2** · **Y** · wheel | switch weapon (holster + draw animations) |
| **E** | interact — the **ammo crate** in the armory refills every weapon |
| **ESC** | settings (sensitivity, horizontal FOV, aim toggle, audio, video) |
| **P** (editor / dev builds) | free camera; **Caps Lock** hands control back to the player while you watch |

Gamepad: left stick move, right stick look, RT fire, LT aim, X reload, Y switch weapon.

---

## 🔫 Weapon handling — what makes it feel like CoD

Both weapons are complete **first-person animation packs** (arms + gun rigged together): equip, idle, walk, sprint,
fire, tactical reload, empty reload, inspect, unequip — and on the UZI aim-in / aim-out and fire-mode clips.
`Assets/Scripts/Weapons/FPWeapon.cs` plays them and layers everything CoD adds on top:

| Feature | How |
|---------|-----|
| **Exact iron sights** | ADS solves the pose that puts the rear sight and front post on the line of sight (`SightBone`, `SightRear`, `SightFront`, `EyeRelief`) — from the *current* animated pose, so it works with or without an aim clip (the Scorpion has none) |
| **Authored framing** | the pack's own camera node is placed on the game camera, so the arms frame as the animator framed them; `HipOffset` / `HipRotation` push it into CoD's lower-right hip position |
| **Camera animation** | whatever the pack's camera does during a clip (reload shake, equip roll) is applied to the **view** — the world moves, the arms keep their framing (`CameraAnimScale`) |
| **Recoil** | the aim climbs per shot (vertical + learnable drift + random side, first-shot kick, ADS scale) and only part of it recovers (`RecoilRecovery`) — you pull down like in CoD |
| **Kick & punch** | camera punch spring + weapon kick-back / muzzle-rise / roll springs, softer while aiming |
| **Spread** | hip cone grows with movement and sustained fire, the crosshair opens with it; ADS is pin-point |
| **Feel** | look sway (much steadier aimed), ADS breathing, sprint-out delay, shells ejected from the port, muzzle flash on the viewmodel layer, tracers, surface impacts (metal / wood / dirt / concrete); bullet-hole and blood decals (projected onto walls, blood on the surface behind a hit enemy), wisps of smoke from a hot barrel, and for the cameras that look at the player (debug cam `P`, spectators) the flash, tracers and brass come from the third-person gun (#178, #194) |
| **FOV** | horizontal FOV like CoD's slider (default 90), weapon-specific ADS zoom (`AdsZoom`), separate viewmodel FOV |

Every number is a public field — tune it in the inspector while playing.

## 🗺️ The Range

| Area | What's there |
|------|--------------|
| **Shooting booths** (spawn) | five covered lanes, numbered, counters you can mantle over |
| **Lanes** | pop-up silhouettes at 10 m and 25 m (all lanes) and 50 m (lanes 2 + 4), swinging **steel plates** at 75 m, distance boards, sandbag cover, sand berm and backstop |
| **Kill house** | open-top concrete-block CQB house, rooms and corridors, ten pop-up targets |
| **Yard** | shipping containers (stacked), sandbag walls, barriers, cars, barrels, tyres, and a **movement course**: mantle walls, a slide gap, steps |
| **Armory** | weapon tables, gear, **ammo resupply crate** (E) |

Targets (`Assets/Scripts/Range/TargetBoard.cs`) fall when shot down and spring back up; steel rings and swings.
The HUD (`UI/HudManager.cs`) counts hits, accuracy and knock-downs. Light props are rigid bodies — shoot them.

## 🧍 The third-person body

The player has a full body: a tactical operator (helmet, NVGs, plate carrier) with the equipped weapon in his hands.
Other cameras see it: press **P** for the debug camera and look at yourself, a mirror, a spectator, and later other
players or bots. The local first-person camera never draws it (its meshes are on render layer 2, *third person only*).

`Assets/Scripts/Player/ThirdPersonBody.cs` drives it from the same `PlayerRig` state the first-person side uses:

| | |
|---|---|
| **Locomotion** | idle / aim, walk, run, strafe, backwards, sprint, jump — crossfaded Mixamo rifle clips |
| **Stance** | crouch lowers the body and **Foot IK** keeps the feet on the ground (the knees bend); a slide lowers it further and leans back |
| **Look** | the body turns with the view, the spine bends with the look pitch |
| **Weapon** | a third-person copy of the equipped gun (`TP_UZI`, `TP_Scorpion`) sits in the right hand; the left hand is IK'd onto the foregrip exactly where the first-person left hand holds it |
| **Overlays** | every shot plays a recoil overlay on the upper body; a reload plays the reload clip and the support hand lets go |
| **Death** | at zero health the body turns into a **ragdoll** |

To use another character: any Mixamo-rigged model works with the clips. Swap the meshes under `PlayerBody` and keep
the component setup.

## 🧩 Adding your own weapon pack

1. Put the model (glTF / FBX with the arms + gun skinned together) under `Assets/Weapons/<Name>/` and extract its
   clips to `animations/*.vanim` (Animation editor ▸ extract clips).
2. Create a viewmodel entity like `Viewmodel_UZI`: an **Animator** listing the clips, one child mesh per submesh
   with **Render layer = First-person**, and the **FPWeapon** script.
3. Set `CameraNode` (the pack's camera node or camera bone) and `CameraFix` (`0,90,0` for an FBX camera node,
   `0,0,0` for a camera bone), the clip names and lengths, and `SightBone` + `SightRear` / `SightFront`
   (bone-local positions of the rear sight and front post) for ADS.
4. Add its entity name to `Slots` on the Player's `Loadout`.
5. For the third-person body: add a `TP_<Name>` child under `PlayerBody` (a static model of the gun with a **Bone
   Attachment** on `mixamorig:RightHand`) and its support-hand offset to `ThirdPersonBody`.

> Packs converted to glTF by Sketchfab sometimes carry broken skin data (inverse bind matrices in centimetres,
> vertex data scaled per axis) and need their handedness flipped for the engine. The two packs here were repaired
> once; see `ATTRIBUTIONS.md` for what was changed.

---

## 📄 License

Template code: **MIT** ([LICENSE](LICENSE)). Assets: the weapon packs are **CC-BY 4.0** — credit is required and
given in [ATTRIBUTIONS.md](ATTRIBUTIONS.md) (keep that credit when you ship); environment textures and props are
**CC0** (Poly Haven); range textures and sounds are generated for this template.
