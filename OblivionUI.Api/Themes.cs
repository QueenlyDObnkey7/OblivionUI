namespace OblivionUI;
public sealed record UiTheme
{
    public double Scale { get; init; } = 1;
    public double FontScale { get; init; } = 1;
    public double Spacing { get; init; } = 1;
    public double AnimationSpeed { get; init; } = 1;
    public UiColor Ink { get; init; } = new(.09f,.055f,.03f);
    public UiColor Accent { get; init; } = new(.48f,.3f,.1f);
    public UiColor PaperTint { get; init; } = new(1,1,1);
    public string PaperImagePath { get; init; } = "";
}
public static class UiThemes
{
    private static UiTheme _current=new();internal static int Revision;
    public static UiTheme Current {get {lock(Hud.Gate)return _current;}set {Validate(value);lock(Hud.Gate){_current=value;Revision++;}}}
    internal static void Validate(UiTheme t) {ArgumentNullException.ThrowIfNull(t);if(!double.IsFinite(t.Scale)||t.Scale is <.5 or >1.5||!double.IsFinite(t.FontScale)||t.FontScale is <.75 or >1.5||!double.IsFinite(t.Spacing)||t.Spacing is <.5 or >2||!double.IsFinite(t.AnimationSpeed)||t.AnimationSpeed is <.1 or >5||!CanvasArt.Color(t.Ink)||!CanvasArt.Color(t.Accent)||!CanvasArt.Color(t.PaperTint)||t.PaperImagePath is null||(t.PaperImagePath.Length>0&&!CanvasArt.AssetPath(t.PaperImagePath)))throw new ArgumentException("Invalid UI theme.");}
}
