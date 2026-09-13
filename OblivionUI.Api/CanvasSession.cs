using System.Text.Json;
namespace OblivionUI;
public sealed record CanvasMarker {public required string Id {get;init;}public UiPoint Position {get;init;}public bool Draggable {get;init;}=true;public string Label {get;init;}="";}
public sealed record CanvasRegion {public required string Id {get;init;}public UiPoint Position {get;init;}public double Width {get;init;}public double Height {get;init;}}
public sealed record CanvasPointerEvent(string? Id,UiPoint Position);
public sealed class CanvasSession
{
    public double Width {get;}public double Height {get;}
    public bool DrawingEnabled {get;set;}
    public UiColor BrushColor {get;set;}=new(.18f,.1f,.04f);
    public double BrushThickness {get;set;}=3;
    public Action<CanvasPointerEvent>? OnClick {get;set;}
    public Action<CanvasPointerEvent>? OnMarkerMoved {get;set;}
    public double Zoom {get;private set;}=1;public UiPoint Pan {get;private set;}
    internal int Revision;private readonly List<CanvasShape> _strokes=[];private readonly Stack<CanvasShape[]> _undo=[],_redo=[];
    private CanvasMarker[] _markers=[];private CanvasRegion[] _regions=[];
    public CanvasSession(double width,double height){if(!double.IsFinite(width)||!double.IsFinite(height)||width is <32 or >630||height is <32 or >2000)throw new ArgumentException("Invalid drawing dimensions.");Width=width;Height=height;}
    public CanvasShape[] Strokes {get {lock(Hud.Gate)return Copy(_strokes);}}
    public CanvasMarker[] Markers {get {lock(Hud.Gate)return [.._markers];}set {if(value is null||value.Length>64||value.Any(m=>m is null||string.IsNullOrWhiteSpace(m.Id)||!Inside(m.Position))||value.Select(m=>m.Id).Distinct().Count()!=value.Length)throw new ArgumentException("Invalid markers.");lock(Hud.Gate){_markers=[..value];Revision++;}}}
    public CanvasRegion[] Regions {get {lock(Hud.Gate)return [.._regions];}set {if(value is null||value.Length>64||value.Any(m=>m is null||string.IsNullOrWhiteSpace(m.Id)||!Inside(m.Position)||!double.IsFinite(m.Width)||!double.IsFinite(m.Height)||m.Width<=0||m.Height<=0||!Inside(new(m.Position.X+m.Width,m.Position.Y+m.Height))))throw new ArgumentException("Invalid regions.");lock(Hud.Gate)_regions=[..value];}}
    public void SetView(double zoom,UiPoint pan){if(!double.IsFinite(zoom)||zoom is <.25 or >8||!double.IsFinite(pan.X)||!double.IsFinite(pan.Y))throw new ArgumentException("Invalid view.");lock(Hud.Gate){Zoom=zoom;Pan=new(Math.Clamp(pan.X,-Width,Width),Math.Clamp(pan.Y,-Height,Height));Revision++;}}
    public UiPoint ToWorld(UiPoint local)=>new(local.X/Zoom+Pan.X,local.Y/Zoom+Pan.Y);
    public UiPoint ToView(UiPoint world)=>new((world.X-Pan.X)*Zoom,(world.Y-Pan.Y)*Zoom);
    public void AddStroke(params UiPoint[] points){lock(Hud.Gate){var shape=new CanvasShape{Kind=CanvasShapeKind.Polyline,Points=points,Color=BrushColor,Thickness=BrushThickness};var shapes=CanvasArt.Validate(new(){Id="stroke",Width=Width,Height=Height,Shapes=[.._strokes,shape]});Checkpoint();_strokes.Clear();_strokes.AddRange(shapes);Revision++;}}
    public void Clear(){lock(Hud.Gate){Checkpoint();_strokes.Clear();Revision++;}}
    public void Undo(){lock(Hud.Gate){if(_undo.Count==0)return;_redo.Push(Copy(_strokes));_strokes.Clear();_strokes.AddRange(_undo.Pop());Revision++;}}
    public void Redo(){lock(Hud.Gate){if(_redo.Count==0)return;_undo.Push(Copy(_strokes));_strokes.Clear();_strokes.AddRange(_redo.Pop());Revision++;}}
    public void Save(string name){lock(Hud.Gate)UiStorage.Write(name+"-drawing",JsonSerializer.Serialize(new DrawingFile(Width,Height,Copy(_strokes),[.._markers])));}
    public void Load(string name){var file=JsonSerializer.Deserialize<DrawingFile>(UiStorage.Read(name+"-drawing"))??throw new ArgumentException("Invalid drawing.");if(file.Width!=Width||file.Height!=Height)throw new ArgumentException("Drawing dimensions differ.");var shapes=CanvasArt.Validate(new(){Id="load",Width=Width,Height=Height,Shapes=file.Shapes});lock(Hud.Gate){Markers=file.Markers;Checkpoint();_strokes.Clear();_strokes.AddRange(shapes);Revision++;}}
    internal bool Inside(UiPoint p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&p.X>=0&&p.Y>=0&&p.X<=Width&&p.Y<=Height;
    internal string? MarkerAt(UiPoint p)=>_markers.LastOrDefault(m=>Math.Pow(ToView(m.Position).X-p.X,2)+Math.Pow(ToView(m.Position).Y-p.Y,2)<=144)?.Id;
    public void MoveMarker(string id,UiPoint p){if(!Inside(p)||!_markers.Any(m=>m.Id==id&&m.Draggable&&m.Position!=p))return;lock(Hud.Gate){_markers=_markers.Select(m=>m.Id==id&&m.Draggable?m with{Position=p}:m).ToArray();Revision++;}OnMarkerMoved?.Invoke(new(id,p));}
    internal void Click(UiPoint p){var region=_regions.LastOrDefault(r=>p.X>=r.Position.X&&p.Y>=r.Position.Y&&p.X<=r.Position.X+r.Width&&p.Y<=r.Position.Y+r.Height);OnClick?.Invoke(new(region?.Id,p));}
    private void Checkpoint(){if(_undo.Count>=32){var keep=_undo.Reverse().Skip(1).ToArray();_undo.Clear();foreach(var entry in keep)_undo.Push(entry);}_undo.Push(Copy(_strokes));_redo.Clear();}
    private static CanvasShape[] Copy(IEnumerable<CanvasShape> shapes)=>shapes.Select(s=>s with{Points=[..s.Points]}).ToArray();
    public sealed record DrawingFile(double Width,double Height,CanvasShape[] Shapes,CanvasMarker[] Markers);
}
