namespace OblivionUI.Client;

internal static partial class BookRuntime
{
    private sealed class ArtPrimitive
    {
        internal nint Widget,Ink,Slot;
        internal bool Visible;
        internal (double X,double Y,double W,double H,double Angle,UiColor Color)? Last;
    }
    private static readonly Dictionary<nint,List<ArtPrimitive>> ArtPrimitives=new();
    private static void ClearArtworkCache()=>ArtPrimitives.Clear();
    private static void RenderArtwork(UnrealBookBridge u,nint widget,BookElement element,double width)
    {
        u.Call(widget,"SetVisibility",("InVisibility",(byte)3)); // Decorative art does not intercept clicks on other controls.
        u.Visible(u.Object(widget,"Label"),false);u.Visible(u.Object(widget,"Description"),false);
        if(element.Kind==BookControl.Image) {
            u.Visible(u.Object(widget,"ArtworkScale"),true);
            u.Call(u.Object(widget,"Artwork"),"SetDesiredSizeOverride",("DesiredSize",(128*element.ImageAspectRatio,128d)));
            u.SetText(widget,"SetArtwork",element.ImagePath);
            Tint(u,u.Object(widget,"Artwork"),element.Tint);
            return;
        }
        var canvas=u.Object(widget,"ArtCanvas"); u.Visible(canvas,true);
        if(!ArtPrimitives.TryGetValue(widget,out var primitives)){primitives=[];ArtPrimitives.Add(widget,primitives);}
        int used=0;
        double scale=width/element.Width;
        IEnumerable<CanvasShape> shapes=element.Shapes;
        if(element.Canvas is {} session){
            shapes=shapes.Concat(session.Strokes);
            if(session==_gesture&&Stroke.Count>=2)shapes=shapes.Append(new(){Kind=CanvasShapeKind.Polyline,Points=Stroke.ToArray(),Color=session.BrushColor,Thickness=session.BrushThickness});
            shapes=shapes.Select(s=>s with{Points=s.Points.Select(session.ToView).ToArray()});
            foreach(var marker in session.Markers){var p=session.ToView(marker.Position);shapes=shapes.Append(new(){Kind=CanvasShapeKind.Rectangle,Points=[new(p.X-5,p.Y-5),new(p.X+5,p.Y+5)],Color=new(.5f,.18f,.04f)});}
        }
        foreach(var shape in shapes) {
            if(shape.Kind==CanvasShapeKind.Rectangle) {
                var a=shape.Points[0];var b=shape.Points[1];
                Add(Math.Min(a.X,b.X)*scale,Math.Min(a.Y,b.Y),Math.Abs(a.X-b.X)*scale,Math.Abs(a.Y-b.Y),0,shape.Color);
            } else for(int i=1;i<shape.Points.Length;i++) {
                var a=shape.Points[i-1];var b=shape.Points[i];
                double dx=(b.X-a.X)*scale,dy=b.Y-a.Y;
                Add(a.X*scale,a.Y,Math.Sqrt(dx*dx+dy*dy),shape.Thickness,Math.Atan2(dy,dx)*180/Math.PI,shape.Color);
            }
        }
        // Keep unused segments owned by their canvas for undo/redo and the next stroke.
        // The page's ClearChildren releases the entire pool on navigation/close.
        for(int i=used;i<primitives.Count;i++)if(primitives[i].Visible){u.Visible(primitives[i].Widget,false);primitives[i].Visible=false;}
        void Add(double x,double y,double w,double h,double angle,UiColor color) {
            if(w<.01||h<.01)return;
            ArtPrimitive p;
            if(used==primitives.Count){
                var row=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",_controlClass),("OwningPlayer",_controller));
                u.Visible(u.Object(row,"Label"),false);u.Visible(u.Object(row,"Description"),false);
                var ink=u.Object(row,"Ink");u.Visible(ink,true);
                var slot=u.Pointer(canvas,"AddChildToCanvas",("Content",row));
                u.Call(row,"SetRenderTransformPivot",("Pivot",(0d,0d)));
                p=new(){Widget=row,Ink=ink,Slot=slot};primitives.Add(p);
            }else p=primitives[used];
            used++;
            if(!p.Visible){u.Call(p.Widget,"SetVisibility",("InVisibility",(byte)3));p.Visible=true;}
            var previous=p.Last;
            if(previous is not {} a||a.X!=x||a.Y!=y)u.Call(p.Slot,"SetPosition",("InPosition",(x,y)));
            if(previous is not {} b||b.W!=w||b.H!=h)u.Call(p.Slot,"SetSize",("InSize",(w,h)));
            if(previous is not {} c||c.Angle!=angle)u.Call(p.Widget,"SetRenderTransformAngle",("Angle",(float)angle));
            if(previous is not {} d||d.Color!=color)Tint(u,p.Ink,color);
            p.Last=(x,y,w,h,angle,color);
        }
    }
    private static void Tint(UnrealBookBridge u,nint image,UiColor color)=>u.Call(image,"SetColorAndOpacity",("InColorAndOpacity",(color.R,color.G,color.B,color.A)));
}
