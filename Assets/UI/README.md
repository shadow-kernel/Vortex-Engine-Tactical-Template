# UI

The menus of this template are **engine UI assets** (`.vui`). Open them in the editor —
**Window ▸ UI Editor**, or double-click the file in the Project panel — and change the layout, colours,
wording or the widget ranges without touching a line of C#.

| File | What it is |
|---|---|
| `PauseMenu.vui` | The ESC pause menu: title, RESUME / OPTIONS / QUIT GAME, hint line. |
| `Options.vui` | The settings screen: tab rail (General · Graphics · Display · Audio · Controls) and every slider, toggle and stepper behind it. |

`Assets/Scripts/UI/EscMenu.cs` only **wires** these screens up: it shows and hides them, reads the widgets by
their id and applies the values to `UserSettings` and the engine. It draws nothing itself, so anything you can
express in the UI editor lands in the game as-is.

## Changing something

* **Move or restyle a control** — edit the `.vui` in the UI editor. Nothing else to do.
* **Change a slider's range** — edit its `min`/`max` there; the script reads whatever the widget reports.
* **Add a new setting** — add the widget in the UI editor, give it a stable `id`, then read it in
  `EscMenu.TickOptions()` with `GetSlider(id)` / `GetToggle(id)` / `GetStep(id)` and apply it in `ApplyAll()`.
* **Rename a control** — keep the `id`; that is what the script looks up. The visible text is `text`.

The ids follow the widget they belong to: `fovSlider` + `fovLabel`, `vsToggle`, `resStepper`, `tab2Button`,
`tabDisplay` (the panel that tab shows), `tab2Marker` (the accent bar next to the active tab).

## Why the HUD is not a `.vui`

`Assets/Scripts/UI/HudManager.cs` stays immediate-mode on purpose. The compass draws a different set of ticks
and letters every frame depending on where the player looks, and the minimap plots one dot per live contact —
both are a *variable number of primitives per frame*, which a retained element tree cannot describe. The parts
that are a fixed layout (health bar, ammo readout, weapon name, crosshair) could move into a `Hud.vui`; they
are kept with the compass and the minimap so the whole HUD has one owner instead of two.
