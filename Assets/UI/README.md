# UI

Every menu in this template is an **engine UI asset** (`.vui`). Open one in the editor — **Window ▸ UI Editor**,
or double-click it in the Project panel — and change the layout, colours, wording or a widget's range without
touching C#.

| File | What it is |
|---|---|
| `PauseMenu.vui` | The ESC pause screen: title, RESUME / OPTIONS / QUIT GAME, hint line. |
| `Options.vui` | The settings **frame** only: title, the tab rail, BACK. No tab content. |
| `OptionsGeneral.vui` | The General tab's rows (FOV, sensitivity, aim toggle, brightness). |
| `OptionsGraphics.vui` | Render scale, DLSS, frame generation, V-Sync, FPS readout. |
| `OptionsDisplay.vui` | Resolution, fullscreen, V-Sync. |
| `OptionsAudio.vui` | Master / effects / music volume. |
| `OptionsControls.vui` | Sensitivity and the key-binding list. |

**One screen per file, on purpose.** The settings frame and the tab you are on are shown *together* — the
engine's UI stack can have several screens up at once, routes input top-first and renders bottom-to-top. The
tab screens are therefore non-blocking (`blocksInput: false`) so clicks fall through to the rail underneath,
and the frame blocks input so the game below never sees them. Keeping the five tabs in one file would stack
them on top of each other in the editor, which is exactly what you do not want to edit.

## How a button reaches the code

A button names a method in its **Click action** field. The engine routes it to the class named after the
screen — `PauseMenu.vui` → `PauseMenuActions`, `Options.vui` → `OptionsActions` — and calls the paramless
method of that name. That is the whole wiring; nothing repeats the id in code.

| Screen | Actions class | Methods |
|---|---|---|
| `PauseMenu.vui` | `PauseMenuActions` | `OnResume`, `OnOptions`, `OnQuitGame` |
| `Options.vui` | `OptionsActions` | `OnTabGeneral` … `OnTabControls`, `OnOptionsBack` |

`EscMenu.cs` owns the state (which screen, which tab) and the value plumbing: it reads the sliders, toggles and
steppers by id and applies them to `UserSettings` and the engine. It draws nothing.

## Changing something

* **Move or restyle a control** — edit the `.vui` in the UI editor. Nothing else to do.
* **Change a slider's range** — edit its `min`/`max`; the script reads whatever the widget reports.
* **Add a row to a tab** — add it to that tab's file. The rows sit in a vertical layout container, so the ones
  below move down by themselves. Give the widget a stable `id`, then read it in `EscMenu.ReadSettings()` with
  `GetSlider` / `GetToggle` / `GetStep` and apply it in `ApplyAll()`.
* **Add a whole tab** — a new `Options<Name>.vui` + a rail button whose Click action is a new
  `OnTab<Name>` in `OptionsActions`, then extend `TabScreens` in `EscMenu.cs`.
* **Rename a control** — keep the `id`; that is what the script looks up. The visible label is `text`.

## Why the HUD is not a `.vui`

`Assets/Scripts/UI/HudManager.cs` stays immediate-mode on purpose. The compass draws a different set of ticks
and letters every frame depending on where the player looks, and the minimap plots one dot per live contact —
both are a *variable number of primitives per frame*, which a retained element tree cannot describe. The parts
that are a fixed layout (health bar, ammo readout, weapon name, crosshair) could move into a `Hud.vui`; they
are kept with the compass and the minimap so the whole HUD has one owner instead of two.
