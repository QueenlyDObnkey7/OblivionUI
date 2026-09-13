namespace OblivionUI;
public sealed record HudPanelDefinition
{
    public string Title {get;init;}="Status";public string Text {get;init;}="";
    public double X {get;init;}=.02;public double Y {get;init;}=.2;
    public double Width {get;init;}=320;public double Opacity {get;init;}=1;
    public bool ShowProgress {get;init;}=true;public double Progress {get;init;}
    /// <summary>Text-free engraved metal bar. X/Y locate its centre rather than its top-left.</summary>
    public bool ProgressOnly {get;init;}
    public bool Visible {get;init;}=true;
}
public static class HudPanels
{
    internal static readonly List<HudPanelHandle> Active=[];
    public static HudPanelHandle Show(HudPanelDefinition d){Validate(d);lock(Hud.Gate){if(Active.Count>=8)throw new InvalidOperationException("At most 8 HUD panels.");var h=new HudPanelHandle(d);Active.Add(h);return h;}}
    internal static void Validate(HudPanelDefinition d){ArgumentNullException.ThrowIfNull(d);if(d.Title is null||d.Text is null||d.Title.Length>80||d.Text.Length>400||(!double.IsFinite(d.X)||!double.IsFinite(d.Y)||!double.IsFinite(d.Width)||!double.IsFinite(d.Opacity)||!double.IsFinite(d.Progress))||d.X is <0 or >1||d.Y is <0 or >1||d.Width is <220 or >700||d.Opacity is <0 or >1||d.Progress is <0 or >1)throw new ArgumentException("Invalid HUD panel.");}
}
public sealed class HudPanelHandle:IDisposable
{
    internal HudPanelDefinition Definition;internal int Revision;internal bool Closed;
    internal HudPanelHandle(HudPanelDefinition d){Definition=d;}
    public void Update(Func<HudPanelDefinition,HudPanelDefinition> update){ArgumentNullException.ThrowIfNull(update);lock(Hud.Gate){if(Closed)return;var d=update(Definition);if(d==Definition)return;HudPanels.Validate(d);Definition=d;Revision++;}}
    public void Dispose(){lock(Hud.Gate){Closed=true;HudPanels.Active.Remove(this);}}
}
