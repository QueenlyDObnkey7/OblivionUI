using OblivionMp.Sdk;

namespace OblivionUI.Client;

internal static class HudPanelRuntime
{
    private static UnrealBookBridge? _ue;
    private static nint _host, _controller, _library, _layout, _panelClass;
    private sealed class Entry
    {
        public nint Widget, RootSize, Paper, Title, Description, ValueProgress, SolidProgress, OrnateFrame, ProgressText, ProgressSlot, SolidSlot;
        public HudPanelDefinition? Definition;
        public UiTheme? Theme;
        public float Progress, Opacity, DrawnProgress=float.NaN, DrawnOpacity=float.NaN;
        public byte Visibility=byte.MaxValue;
        public double Width=double.NaN, Height=double.NaN, Scale=double.NaN, X=double.NaN, Y=double.NaN;
        public string? ProgressLabel;
    }
    private static readonly Dictionary<HudPanelHandle,Entry> Widgets=[];
    private static readonly List<HudPanelHandle> Closed=[];
    private static double _last;

    internal static void Frame(double now, bool playable)
    {
        lock(Hud.Gate)
        {
            if(HudPanels.Active.Count==0 && Widgets.Count==0)return;
            _ue??=new(); var u=_ue;
            if(_host!=0 && u.FindWidget()!=_host)Abandon();
            if(!playable)
            {
                foreach(var e in Widgets.Values)SetVisibility(u,e,false);
                _last=now;
                return;
            }
            if(_host==0)
            {
                _host=u.FindWidget();
                if(_host==0){SDK.GameMessage.ShowGameMessage("","","");_host=u.FindWidget();}
                if(_host==0)return;
                _controller=u.Pointer(_host,"GetOwningPlayer");
                _library=u.Library();
                _layout=u.DefaultObject("/Script/UMG.Default__WidgetLayoutLibrary");
                _panelClass=u.Object(_host,"HudPanelClass");
            }
            Closed.Clear();
            foreach(var pair in Widgets)if(pair.Key.Closed)Closed.Add(pair.Key);
            foreach(var handle in Closed){u.Call(Widgets[handle].Widget,"RemoveFromParent");Widgets.Remove(handle);}
            if(HudPanels.Active.Count==0)return;
            double dt=Math.Clamp(now-_last,0,.1); _last=now;
            var theme=UiThemes.Current;
            // Hidden panels do not need viewport queries or layout work.
            bool anyVisible=false;
            foreach(var handle in HudPanels.Active)if(handle.Definition.Visible){anyVisible=true;break;}
            if(!anyVisible){foreach(var e in Widgets.Values)SetVisibility(u,e,false);return;}
            var size=u.Vector(u.Call(_layout,"GetViewportSize",("WorldContextObject",_host)),"GetViewportSize");
            double dpi=u.ResultFloat(u.Call(_layout,"GetViewportScale",("WorldContextObject",_host)),"GetViewportScale");
            if(!double.IsFinite(dpi)||dpi<=0)return;
            foreach(var handle in HudPanels.Active)
            {
                var d=handle.Definition;
                if(!Widgets.TryGetValue(handle,out var row))
                {
                    if(!d.Visible)continue;
                    row=Create(u); Widgets.Add(handle,row);
                }
                SetVisibility(u,row,d.Visible);
                if(!d.Visible)continue;
                ApplyAppearance(u,row,d,theme);
                ApplyLayout(u,row,d,theme,size.X/dpi,size.Y/dpi);
                float blend=(float)(1-Math.Exp(-dt*10*theme.AnimationSpeed));
                row.Progress=Approach(row.Progress,(float)d.Progress,blend);
                row.Opacity=Approach(row.Opacity,(float)d.Opacity,blend);
                if(row.DrawnOpacity!=row.Opacity)
                {
                    u.Call(row.Widget,"SetRenderOpacity",("InOpacity",row.Opacity));
                    row.DrawnOpacity=row.Opacity;
                }
                if(d.ShowProgress && row.DrawnProgress!=row.Progress)
                {
                    u.Call(d.ProgressOnly?row.SolidProgress:row.ValueProgress,"SetPercent",("InPercent",row.Progress));
                    row.DrawnProgress=row.Progress;
                }
                if(d.ShowProgress && !d.ProgressOnly)
                {
                    string label=$"{d.Progress:P0}";
                    if(row.ProgressLabel!=label){u.SetText(row.Widget,"SetSliderCurrent",label);row.ProgressLabel=label;}
                }
                row.Definition=d; row.Theme=theme;
            }
        }
    }

    private static Entry Create(UnrealBookBridge u)
    {
        var row=new Entry{Widget=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",_panelClass),("OwningPlayer",_controller))};
        // These widgets and slots are owned by the panel and live until the host changes.
        row.RootSize=u.Object(row.Widget,"RootSize"); row.Paper=u.Object(row.Widget,"BookBackground");
        row.Title=u.Object(row.Widget,"Title"); row.Description=u.Object(row.Widget,"Description");
        row.ValueProgress=u.Object(row.Widget,"ValueProgress"); row.SolidProgress=u.Object(row.Widget,"SolidProgress");
        row.OrnateFrame=u.Object(row.Widget,"OrnateFrame"); row.ProgressText=u.Object(row.Widget,"ProgressText");
        row.ProgressSlot=u.Object(row.ValueProgress,"Slot"); row.SolidSlot=u.Object(row.SolidProgress,"Slot");
        u.Call(row.Widget,"SetRenderTransformPivot",("Pivot",(0d,0d)));
        u.Call(row.Widget,"AddToViewport",("ZOrder",30));
        return row;
    }

    private static void ApplyAppearance(UnrealBookBridge u, Entry row, HudPanelDefinition d, UiTheme theme)
    {
        var previous=row.Definition; var oldTheme=row.Theme;
        // A progress update increments the public revision too. Compare the actual presentation
        // fields so an animated bar never reloads its paper or rebuilds text/fonts each tick.
        if(previous?.Title!=d.Title)u.SetText(row.Widget,"SetLabel",d.Title);
        if(previous?.Text!=d.Text)u.SetText(row.Widget,"SetDescription",d.Text);
        if(oldTheme?.FontScale!=theme.FontScale)
        {
            u.Font(row.Widget,"SetLabelSize",(int)(18*theme.FontScale));
            u.Font(row.Widget,"SetDescriptionSize",(int)(14*theme.FontScale));
        }
        if(oldTheme?.Ink!=theme.Ink){u.Ink(row.Widget,"SetLabelInk",theme.Ink);u.Ink(row.Widget,"SetDescriptionInk",theme.Ink);}
        if(oldTheme?.PaperTint!=theme.PaperTint)u.Call(row.Paper,"SetColorAndOpacity",("InColorAndOpacity",(theme.PaperTint.R,theme.PaperTint.G,theme.PaperTint.B,theme.PaperTint.A)));
        if(oldTheme?.PaperImagePath!=theme.PaperImagePath)u.SetText(row.Widget,"SetPaperArtwork",theme.PaperImagePath.Length>0?theme.PaperImagePath:"/Game/OblivionUI/Book/T_ImperialHudPaper.T_ImperialHudPaper");
        if(previous?.ShowProgress!=d.ShowProgress || previous?.ProgressOnly!=d.ProgressOnly)
        {
            u.Visible(row.ValueProgress,d.ShowProgress&&!d.ProgressOnly);
            u.Visible(row.OrnateFrame,d.ShowProgress&&d.ProgressOnly);
            u.Visible(row.SolidProgress,d.ShowProgress&&d.ProgressOnly);
            u.Visible(row.ProgressText,d.ShowProgress&&!d.ProgressOnly);
            u.Visible(row.Paper,!d.ProgressOnly);u.Visible(row.Title,!d.ProgressOnly);u.Visible(row.Description,!d.ProgressOnly);
            row.DrawnProgress=float.NaN;
        }
        if(previous?.ProgressOnly!=d.ProgressOnly)u.Call(row.ProgressSlot,"SetOffsets",("InOffset",d.ProgressOnly?(0f,0f,0f,22f):(22f,118f,22f,7f)));
        if(previous?.Width!=d.Width)u.Call(row.SolidSlot,"SetOffsets",("InOffset",((float)(d.Width*.104),13f,(float)(d.Width*.104),14f)));
        if(previous?.ProgressOnly!=d.ProgressOnly || oldTheme?.Accent!=theme.Accent)
            u.Call(d.ProgressOnly?row.SolidProgress:row.ValueProgress,"SetFillColorAndOpacity",("InColor",(d.ProgressOnly?.66f:theme.Accent.R,d.ProgressOnly?.38f:theme.Accent.G,d.ProgressOnly?.10f:theme.Accent.B,theme.Accent.A)));
    }

    private static void ApplyLayout(UnrealBookBridge u, Entry row, HudPanelDefinition d, UiTheme theme, double viewportWidth, double viewportHeight)
    {
        double width=Math.Min(d.Width*theme.Scale,viewportWidth),height=Math.Min((d.ProgressOnly?40:160)*theme.Scale,viewportHeight);
        double naturalWidth=width/theme.Scale,naturalHeight=height/theme.Scale;
        if(row.Height!=naturalHeight)u.Call(row.RootSize,"SetHeightOverride",("InHeightOverride",(float)naturalHeight));
        if(row.Width!=naturalWidth)u.Call(row.RootSize,"SetWidthOverride",("InWidthOverride",(float)naturalWidth));
        if(row.Scale!=theme.Scale)u.Call(row.Widget,"SetRenderScale",("Scale",(theme.Scale,theme.Scale)));
        if(row.Width!=naturalWidth || row.Height!=naturalHeight)u.Call(row.Widget,"SetDesiredSizeInViewport",("Size",(naturalWidth,naturalHeight)));
        double x=Math.Clamp(d.X*viewportWidth-(d.ProgressOnly?width/2:0),0,Math.Max(0,viewportWidth-width));
        double y=Math.Clamp(d.Y*viewportHeight-(d.ProgressOnly?height/2:0),0,Math.Max(0,viewportHeight-height));
        if(row.X!=x || row.Y!=y)u.Call(row.Widget,"SetPositionInViewport",("Position",(x,y)),("bRemoveDPIScale",false));
        row.Width=naturalWidth;row.Height=naturalHeight;row.Scale=theme.Scale;row.X=x;row.Y=y;
    }

    private static float Approach(float value,float target,float blend)
    {
        float result=value+(target-value)*blend;
        // End the otherwise endless invalidations once the remainder is visually insignificant.
        return Math.Abs(target-result)<.0001f?target:result;
    }
    private static void SetVisibility(UnrealBookBridge u,Entry row,bool visible)
    {
        byte value=visible?(byte)3:(byte)1;
        if(row.Visibility==value)return;
        u.Call(row.Widget,"SetVisibility",("InVisibility",value));row.Visibility=value;
    }
    private static void Abandon(){Widgets.Clear();Closed.Clear();_host=_controller=_library=_layout=_panelClass=0;_last=0;}
    internal static void Reset()
    {
        if(_ue is {} u && _host!=0 && u.FindWidget()==_host)foreach(var e in Widgets.Values)u.Call(e.Widget,"RemoveFromParent");
        Abandon();
    }
}
