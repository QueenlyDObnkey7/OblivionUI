namespace OblivionUI;

public readonly record struct UiPoint(double X, double Y);
public readonly record struct UiColor(float R, float G, float B, float A = 1);
public enum CanvasShapeKind { Rectangle, Line, Polyline }
public sealed record CanvasShape
{
    public CanvasShapeKind Kind { get; init; }
    public UiPoint[] Points { get; init; } = [];
    public UiColor Color { get; init; } = new(.18f,.10f,.04f,1);
    public double Thickness { get; init; } = 2;
}
internal static class CanvasArt
{
    internal static bool AssetPath(string path) => path.Length is >0 and <=512 && path.StartsWith("/Game/",StringComparison.Ordinal) &&
        path.Contains('.') && !path.Contains("..") && path.All(c=>char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '.');
    internal static bool Color(UiColor c) => new[]{c.R,c.G,c.B,c.A}.All(v=>float.IsFinite(v) && v>=0 && v<=1);
    internal static CanvasShape[] Validate(BookElement e)
    {
        if(e.ImagePath is null || (e.ImagePath.Length>0 && !AssetPath(e.ImagePath)) || (e.Kind==BookControl.Image && e.ImagePath.Length==0)) throw new ArgumentException("Images need a cooked /Game/.../Texture.Texture asset path.");
        if(!double.IsFinite(e.ImageAspectRatio) || e.ImageAspectRatio is <.05 or >20 || !Color(e.Tint)) throw new ArgumentException("Invalid image ratio or tint.");
        if(e.Shapes is null || e.Shapes.Length>128) throw new ArgumentException("A canvas supports at most 128 shapes.");
        int segments=0;
        var copy=e.Shapes.Select(s=>{
            if(s is null || !Enum.IsDefined(s.Kind) || !Color(s.Color) || !double.IsFinite(s.Thickness) || s.Thickness is <.5 or >32 || s.Points is null || s.Points.Length<2 || (s.Kind!=CanvasShapeKind.Polyline && s.Points.Length!=2)) throw new ArgumentException("Invalid canvas shape.");
            if(s.Points.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||p.X<0||p.Y<0||p.X>e.Width||p.Y>e.Height)) throw new ArgumentException("Drawing points must fit within the element's Width and Height.");
            segments+=s.Points.Length-1;
            if(segments>512)throw new ArgumentException("A canvas supports at most 512 segments.");
            return s with {Points=[..s.Points]};
        }).ToArray();
        return copy;
    }
}
