using Vortex;

// Button actions for Options.vui — ONE class per UI screen (see PauseMenuActions). The five rail buttons and
// BACK name these methods in their "Click action" field; EscMenu owns which tab screen is on.
public class OptionsActions : VortexBehaviour
{
    public void OnTabGeneral()  { EscMenu.ShowTab(0); }
    public void OnTabGraphics() { EscMenu.ShowTab(1); }
    public void OnTabDisplay()  { EscMenu.ShowTab(2); }
    public void OnTabAudio()    { EscMenu.ShowTab(3); }
    public void OnTabControls() { EscMenu.ShowTab(4); }
    public void OnOptionsBack() { EscMenu.CloseOptions(); }
}
