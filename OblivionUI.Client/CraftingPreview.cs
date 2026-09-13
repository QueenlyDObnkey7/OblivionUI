namespace OblivionUI.Client;

internal static partial class BookRuntime
{
    private static nint _recipePreview,_previewRoot;
    private static string _previewText="";
    private static bool _previewVisible;
    private static UiTheme? _previewTheme;
    private static double _previewViewportWidth,_previewViewportHeight;
    private static long _previewViewportAt;
    private static (double Width,double Height,double X,double Y)? _previewBounds;
    private static void ClearPreview()
    {
        _recipePreview=_previewRoot=0;_previewText="";_previewVisible=false;_previewTheme=null;
        _previewViewportWidth=_previewViewportHeight=0;_previewViewportAt=0;_previewBounds=null;
    }
    private static void PreviewFrame()
    {
        var u=_ue!;
        // Mouse hover wins over the keyboard focus retained by the first row.
        // FeatureFrame already samples both; do not query each UObject a second time.
        Row? row=null,focused=null;
        foreach(var candidate in Rows){if(candidate.Element.RecipePreview.Length==0)continue;if(candidate.Frame.Hovered){row=candidate;break;}if(focused is null&&candidate.Frame.Focused)focused=candidate;}
        row??=focused;
        if(row is null){if(_previewVisible){u.Visible(_recipePreview,false);_previewVisible=false;}return;}
        if(_recipePreview==0){
            var cls=u.Object(_host,"TooltipClass");if(cls==0)return;
            _recipePreview=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",cls),("OwningPlayer",_controller));
            if(_recipePreview==0)return;
            u.Call(_recipePreview,"AddToViewport",("ZOrder",81));
            _previewRoot=u.Object(_recipePreview,"RootSize");
            u.Font(_recipePreview,"SetLabelSize",16);
        }
        var theme=_handle!.Definition.Theme??UiThemes.Current;
        bool changed=_previewText!=row.Element.RecipePreview;
        long now=Environment.TickCount64;
        // Accommodate window/DPI changes without rebuilding layout on every UI tick.
        if(changed||now-_previewViewportAt>=250||_previewViewportWidth<=0){
            var layout=u.DefaultObject("/Script/UMG.Default__WidgetLayoutLibrary");
            var size=u.Vector(u.Call(layout,"GetViewportSize",("WorldContextObject",_host)),"GetViewportSize");
            double dpi=u.ResultFloat(u.Call(layout,"GetViewportScale",("WorldContextObject",_host)),"GetViewportScale");if(dpi<=0||size.X<=0||size.Y<=0)return;
            double viewportWidth=size.X/dpi,viewportHeight=size.Y/dpi;
            changed|=viewportWidth!=_previewViewportWidth||viewportHeight!=_previewViewportHeight;
            _previewViewportWidth=viewportWidth;_previewViewportHeight=viewportHeight;_previewViewportAt=now;
        }
        if(!_previewVisible){u.Call(_recipePreview,"SetVisibility",("InVisibility",(byte)3));_previewVisible=true;}
        if(_previewTheme!=theme){u.Ink(_recipePreview,"SetLabelInk",theme.Ink);_previewTheme=theme;}
        if(!changed&&_previewBounds is not null)return;
        if(_previewText!=row.Element.RecipePreview){u.SetText(_recipePreview,"SetLabel",row.Element.RecipePreview);_previewText=row.Element.RecipePreview;}
        double vw=_previewViewportWidth,vh=_previewViewportHeight;
        bool side=_handle.Definition.Placement is BookPlacement.Left or BookPlacement.Right;
        double bookWidth=side?500:760,scale=Math.Min((vw-56)/bookWidth,(vh-56)/940);
        double width=Math.Min(360,Math.Max(180,vw-bookWidth*scale-84));
        double height=Math.Min(vh-56,64+row.Element.RecipePreview.Split('\n').Sum(line=>Math.Max(1,Math.Ceiling(line.Length/((width-44)/9))))*23);
        double x=_handle.Definition.Placement==BookPlacement.Right?vw-28-bookWidth*scale-width-18:28+bookWidth*scale+18;
        if(!side)x=(vw+bookWidth*scale)/2+18;
        var bounds=(Width:width,Height:height,X:Math.Clamp(x,0,Math.Max(0,vw-width)),Y:Math.Clamp(vh*.22,0,Math.Max(0,vh-height)));
        if(_previewBounds==bounds)return;
        u.Call(_previewRoot,"SetWidthOverride",("InWidthOverride",(float)width));
        u.Call(_previewRoot,"SetHeightOverride",("InHeightOverride",(float)height));
        u.Call(_recipePreview,"SetDesiredSizeInViewport",("Size",(width,height)));
        u.Call(_recipePreview,"SetPositionInViewport",("Position",(bounds.X,bounds.Y)),("bRemoveDPIScale",false));
        _previewBounds=bounds;
    }
}
