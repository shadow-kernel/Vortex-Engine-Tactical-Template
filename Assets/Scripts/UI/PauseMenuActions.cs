using Vortex;

// Button actions for PauseMenu.vui — ONE class per UI screen. The engine routes a button's clickAction to
// the method of the same name here (PauseMenu.vui -> PauseMenuActions), so the editor's "Click action" field
// is the whole wiring: no polling, no ids repeated in code.
//
// The menu's state lives in EscMenu; these are just the three entry points the buttons name.
public class PauseMenuActions : VortexBehaviour
{
    public void OnResume()   { EscMenu.Resume(); }
    public void OnOptions()  { EscMenu.OpenOptions(); }
    public void OnQuitGame() { Application.Quit(); }
}
