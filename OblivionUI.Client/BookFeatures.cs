using System.Runtime.InteropServices;
namespace OblivionUI.Client;
internal static partial class BookRuntime
{
    private static int _themeRevision=-1;private static string? _captureId,_dragId,_slotSelected;private static UiPoint _press;
    private static bool _lastLeft,_lastRight;private static readonly bool[] Keys=new bool[256];
    private sealed record ArtRow(BookElement Element,nint Widget,double Width,int Revision);
    private static readonly List<ArtRow> ArtRows=[];
    private static CanvasSession? _gesture;private static string? _marker;private static readonly List<UiPoint> Stroke=[];private static UiPoint _lastPointer;
    private static double _lastFrame,_lastArt;private static float _fade;private static readonly Dictionary<string,float> ProgressValues=[];
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    private static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    private static void ClearFeatures(){ClearPreview();ClearArtworkCache();ArtRows.Clear();_captureId=_dragId=_slotSelected=null;_gesture=null;Stroke.Clear();_themeRevision=-1;_fade=0;_hasSlotControls=false;PressedKeys.Clear();ProgressValues.Clear();for(int i=0;i<256;i++)Keys[i]=false;}
    private static void StyleRow(UnrealBookBridge u,nint row,BookElement e,UiTheme theme)
    {
        u.Font(row,"SetLabelSize",(int)(22*theme.FontScale));u.Font(row,"SetDescriptionSize",(int)(16*theme.FontScale));u.Font(row,"SetButtonSize",(int)(21*theme.FontScale));
        u.Ink(row,"SetLabelInk",theme.Ink);u.Ink(row,"SetDescriptionInk",theme.Ink);u.Ink(row,"SetButtonInk",theme.Ink);
        Tint(u,u.Object(row,"FocusRing"),theme.Accent with{A=.16f});
        if(e.Tooltip.Length>0){var cls=u.Object(_host,"TooltipClass");var tip=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",cls),("OwningPlayer",_controller));u.SetText(tip,"SetLabel",e.Tooltip);u.Font(tip,"SetLabelSize",(int)(16*theme.FontScale));u.Ink(tip,"SetLabelInk",theme.Ink);Tint(u,u.Object(tip,"BookBackground"),theme.PaperTint);u.Call(u.Object(tip,"RootSize"),"SetHeightOverride",("InHeightOverride",(float)Math.Clamp(60+Math.Ceiling(e.Tooltip.Length/35d)*22*theme.FontScale,110,550)));u.Call(row,"SetToolTip",("Widget",tip));}
    }
    private static void ApplyBookTheme(UnrealBookBridge u,UiTheme theme)
    {
        u.Font(_book,"SetTitleSize",(int)(30*theme.FontScale));u.Font(_book,"SetSubtitleSize",(int)(18*theme.FontScale));u.Ink(_book,"SetTitleInk",theme.Ink);u.Ink(_book,"SetSubtitleInk",theme.Ink);
        Tint(u,u.Object(_book,"BookBackground"),theme.PaperTint);
        bool nativePaper=theme.PaperImagePath.Length==0 && _handle!.Definition.Placement!=BookPlacement.Popup && _handle.Definition.ShowTabs;
        const string inventoryPaper="/Game/Art/UI/Common/Backgrounds/T_UI_PlayerMenu_Background.T_UI_PlayerMenu_Background";
        u.SetText(_book,"SetPaperArtwork",theme.PaperImagePath.Length>0?theme.PaperImagePath:_handle!.Definition.Placement==BookPlacement.Popup?"/Game/OblivionUI/T_NotificationParchment.T_NotificationParchment":nativePaper?inventoryPaper:"/Game/OblivionUI/Book/T_Book.T_Book");
        var surface=u.Object(_book,"PageSurface");u.Call(surface,"SetVisibility",("InVisibility",nativePaper?(byte)3:(byte)1));
        u.Visible(u.Object(_book,"TabLeather"),false);
        if(nativePaper){u.SetText(_book,"SetPageArtwork",inventoryPaper);Tint(u,surface,theme.PaperTint);}
        double scale=Math.Min(1,theme.Scale);u.Call(u.Object(_book,"BookSize"),"SetRenderScale",("Scale",(scale,scale)));
    }
    private static void ConfigureCard(UnrealBookBridge u,nint row,BookElement e)
    {
        u.Call(u.Object(row,"CardPaper"),"SetVisibility",("InVisibility",(byte)3));
        u.Call(u.Object(row,"ActionButton"),"SetBackgroundColor",("InBackgroundColor",(1f,1f,1f,0f)));
        foreach(var name in new[]{"Label","Description"})u.Call(u.Object(row,name),"SetVisibility",("InVisibility",(byte)3));
        void Offset(string name,float left,float top,float right,float bottom)=>u.Call(u.Object(u.Object(row,name),"Slot"),"SetOffsets",("InOffset",(left,top,right,bottom)));
        Offset("Label",16,e.ImagePath.Length>0?86:20,16,e.ImagePath.Length>0?60:90);Offset("Description",16,e.ImagePath.Length>0?148:116,16,(float)Math.Max(24,e.Height-(e.ImagePath.Length>0?148:116)-8));Offset("ActionButton",0,0,0,(float)e.Height);u.SetText(row,"SetButtonLabel","");
        if(e.ImagePath.Length>0){u.Visible(u.Object(row,"ArtworkScale"),true);u.Call(u.Object(row,"ArtworkScale"),"SetVisibility",("InVisibility",(byte)3));Offset("ArtworkScale",12,8,12,(float)e.Height-80);u.SetText(row,"SetArtwork",e.ImagePath);}
    }
    private static int TabIcon(BookPage page)
    {
        var name=(page.TabIcon.Length>0?page.TabIcon:page.Title).ToLowerInvariant();
        if(name.Contains("weapon"))return 0;if(name.Contains("armour")||name.Contains("armor"))return 1;
        if(name.Contains("spell")||name.Contains("magic"))return 2;if(name.Contains("voice")||name.Contains("speaking")||name.Contains("microphone"))return 4;
        if(name.Contains("setting")||name.Contains("apply")||name.Contains("option"))return 5;if(name.Contains("item")||name=="all")return 6;return 3;
    }
    private static void BuildTabs(UnrealBookBridge u)
    {
        var h=_handle!;var tabs=u.Object(_book,"PageTabs");u.Call(tabs,"ClearChildren");bool show=BookGeometry.HasTabs(h.Definition);u.Visible(tabs,show);
        foreach(var name in new[]{"TabLeftBlend","TabRightBlend"})u.Visible(u.Object(_book,name),false);
        if(h.Definition.Placement==BookPlacement.Popup)return;
        double bodyWidth=BookGeometry.ContentWidth(h.Definition.Placement),margin=h.Definition.Placement is BookPlacement.Left or BookPlacement.Right?42:65;
        var (pages,selected,start,count,tabX,width,tabHeight)=BookGeometry.Tabs(h.Definition,h.Page);double scale=tabHeight/80;
        void PlaceWidget(string name,double x,double y,double width,double height){var slot=u.Object(u.Object(_book,name),"Slot");u.Call(slot,"SetPosition",("InPosition",(x,y)));u.Call(slot,"SetSize",("InSize",(width,height)));}
        PlaceWidget("Title",margin,show?100+tabHeight:100,bodyWidth-100,48);PlaceWidget("CloseButton",margin+bodyWidth-84,show?100+tabHeight:100,84,38);
        PlaceWidget("Subtitle",margin,show?146+tabHeight:153,bodyWidth,42);double bodyY=show?198+tabHeight:212;PlaceWidget("BodyScroll",margin,bodyY,bodyWidth,822-bodyY);
        PlaceWidget("PageTabs",margin,72,bodyWidth,tabHeight);if(!show)return;
        var theme=h.Definition.Theme??UiThemes.Current;
        if(theme.PaperImagePath.Length==0){
            foreach(bool rightEdge in new[]{false,true}){
                string edge=rightEdge?"TabRightBlend":"TabLeftBlend";double edgeX=margin+tabX+(rightEdge?count*width-8*scale:0);
                PlaceWidget(edge,edgeX,72,8*scale,tabHeight);u.Call(u.Object(_book,edge),"SetVisibility",("InVisibility",(byte)3));u.SetText(_book,"Set"+edge+"Artwork","/Game/Art/UI/Common/Backgrounds/T_UI_PlayerMenu_Background.T_UI_PlayerMenu_Background");
                for(int band=0;band<8;band++){
                    u.Call(u.Object(u.Object(_book,edge+band),"Slot"),"SetOffsets",("InOffset",((float)(band*scale),0f,(float)scale,0f)));
                    string art=edge+"Artwork"+band;PlaceWidget(art,15-edgeX-band*scale,0,bodyWidth+2*margin-30,847);float f=(band+.5f)/8;float fade=f*f*(3-2*f);Tint(u,u.Object(_book,art),theme.PaperTint with{A=theme.PaperTint.A*(rightEdge?fade:1-fade)});
                }
            }
        }
        void Add(int target,double x,double w,int icon,bool active,string title,string image="",bool right=false,bool enabled=true)
        {
            var row=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",_controlClass),("OwningPlayer",_controller));var slot=u.Pointer(tabs,"AddChildToCanvas",("Content",row));u.Call(slot,"SetPosition",("InPosition",(x,0d)));w/=scale;u.Call(slot,"SetSize",("InSize",(w,80d)));u.Call(row,"SetRenderTransformPivot",("Pivot",(0d,0d)));u.Call(row,"SetRenderScale",("Scale",(scale,scale)));
            u.Visible(u.Object(row,"Label"),false);u.Visible(u.Object(row,"Description"),false);
            var button=u.Object(row,"ActionButton");u.Visible(button,true);u.Call(button,"SetIsEnabled",("bInIsEnabled",enabled));u.Call(button,"SetBackgroundColor",("InBackgroundColor",(1f,1f,1f,0f)));u.Call(u.Object(button,"Slot"),"SetOffsets",("InOffset",(0f,0f,0f,80f)));u.SetText(row,"SetButtonLabel","");
            if(icon!=8){string paper=active?"TabActivePaper":"TabInactivePaper";u.SetText(row,"Set"+paper,"/Game/Art/UI/Common/Tabs/T_Tab_1.T_Tab_1");u.Call(u.Object(row,paper),"SetVisibility",("InVisibility",(byte)3));var tint=(h.Definition.Theme??UiThemes.Current).PaperTint;for(int band=0;band<17;band++){float f=band==0?0:(band-.5f)/16;Tint(u,u.Object(row,paper+band),tint with{A=tint.A*(1-f*f*(3-2*f))});}}
            void Icon(string name,float left,float top,float rightInset,float bottom){var obj=u.Object(row,name);u.Call(obj,"SetVisibility",("InVisibility",(byte)3));u.Call(u.Object(obj,"Slot"),"SetOffsets",("InOffset",(left,top,rightInset,bottom)));}
            if(image.Length>0){Icon("ArtworkScale",(float)Math.Max(0,(w-60)/2),8,(float)Math.Max(0,(w-60)/2),10);u.SetText(row,"SetArtwork",image);}
                        else if(icon==8){u.SetText(row,"SetButtonLabel",right?"D":"A");}
            else {var suffix=icon switch{0=>"Weapons",1=>"Armor",2=>"ActiveEffects",4=>"OnSelf",5=>"skills",6=>"All_items",_=>"details"};Icon("ArtworkScale",(float)Math.Max(0,(w-54)/2),7,(float)Math.Max(0,(w-54)/2),18);u.SetText(row,"SetArtwork","/Game/Art/UI/Icons/Icon_Category/T_Category_"+suffix+".T_Category_"+suffix);}
            var element=new BookElement{Id="__tab"+target,Kind=BookControl.Button,Checked=active,Tooltip=title,Enabled=enabled};StyleRow(u,row,element,h.Definition.Theme??UiThemes.Current);Rows.Add(new(element,row,button,u.Serial(row,"EventSerial")));
        }
        Add(pages[Math.Max(0,selected-1)],tabX-40*scale,28*scale,8,false,"Previous categories",enabled:selected>0);
        for(int i=0;i<count;i++){int index=pages[start+i];var p=h.Definition.Pages[index];Add(index,tabX+i*width,width,TabIcon(p),index==h.Page,p.Title,p.TabImagePath);}
        Add(pages[Math.Min(pages.Length-1,selected+1)],tabX+count*width+12*scale,28*scale,8,false,"Next categories",right:true,enabled:selected+1<pages.Length);
    }
    private static BookElement Ring(BookElement e,double width)
    {
        var shapes=new List<CanvasShape>();double fraction=(e.Value-e.Minimum)/(e.Maximum-e.Minimum),cx=e.Width/2,cy=e.Height/2-12,r=Math.Min(48,e.Height/2-26);
        for(int i=0;i<48;i++){double a=(i/48d*2-.5)*Math.PI,b=((i+1)/48d*2-.5)*Math.PI;shapes.Add(new(){Kind=CanvasShapeKind.Line,Points=[new(cx+r*Math.Cos(a),cy+r*Math.Sin(a)),new(cx+r*Math.Cos(b),cy+r*Math.Sin(b))],Thickness=5,Color=i/48d<fraction?(_handle!.Definition.Theme??UiThemes.Current).Accent:new(.3f,.2f,.1f,.18f)});}
        return e with{Kind=BookControl.Canvas,Shapes=shapes.ToArray()};
    }
    private static void BeginKeyCapture(Row row){_captureId=row.Element.Id;for(int k=0;k<256;k++)Keys[k]=Down(k);_ue!.SetText(row.Widget,"SetButtonLabel","Press key (Esc cancels)");}
    private static string? KeyName(int k)=>k switch{>=65 and <=90=>((char)k).ToString(),>=48 and <=57=>((char)k).ToString(),>=112 and <=135=>"F"+(k-111),32=>"Space",9=>"Tab",13=>"Enter",37=>"Left",38=>"Up",39=>"Right",40=>"Down",45=>"Insert",46=>"Delete",36=>"Home",35=>"End",33=>"PageUp",34=>"PageDown",_=>null};
    private static (UiPoint Local,double Width,double Height) Locate(nint widget,UiPoint screen)
    {
        var u=_ue!;var g=u.ResultBytes(u.Call(widget,"GetCachedGeometry"),"GetCachedGeometry");var slate=u.DefaultObject("/Script/UMG.Default__SlateBlueprintLibrary");var p=u.Vector(u.Call(slate,"AbsoluteToLocal",("Geometry",g),("AbsoluteCoordinate",(screen.X,screen.Y))),"AbsoluteToLocal");var size=u.Vector(u.Call(slate,"GetLocalSize",("Geometry",g)),"GetLocalSize");return(new(p.X,p.Y),size.X,size.Y);
    }
    private static bool Hit(nint widget,UiPoint point){var p=Locate(widget,point);return p.Width>0&&p.Height>0&&p.Local.X>=0&&p.Local.Y>=0&&p.Local.X<p.Width&&p.Local.Y<p.Height;}
    private static void Drop(string from,string to)
    {
        try{SlotMoves.Request(_handle!,from,to);}catch(Exception ex){Console.WriteLine($"[OblivionUI] Drop rejected: {ex.Message}");}
    }
    private static void SlotClick(Row row){if(_slotSelected is {} source){Drop(source,row.Element.Id);_slotSelected=null;}else if(row.Element.DragPayload.Length>0)_slotSelected=row.Element.Id;}
    private static void FeatureFrame()
    {
        var u=_ue!;var h=_handle!;var theme=h.Definition.Theme??UiThemes.Current;double now=Environment.TickCount64/1000d,dt=Math.Clamp(now-_lastFrame,0,.1);_lastFrame=now;
        if(_fade<1){_fade=Math.Min(1,_fade+(float)(dt*5*theme.AnimationSpeed));u.Call(_book,"SetRenderOpacity",("InOpacity",_fade));}
        UpdateRowHighlights(u);
        foreach(var row in Rows){if(row.Element.Kind!=BookControl.ProgressBar)continue;var e=row.Element;float target=(float)((e.Value-e.Minimum)/(e.Maximum-e.Minimum));float current=ProgressValues.GetValueOrDefault(e.Id);current+=(target-current)*(float)(1-Math.Exp(-dt*10*theme.AnimationSpeed));if(Math.Abs(current-target)<.0001f)current=target;ProgressValues[e.Id]=current;if(row.Frame.Percent!=current){u.Call(row.Control,"SetPercent",("InPercent",current));row.Frame.Percent=current;}}
        if(_captureId is null&&!BookGeometry.HasTabs(h.Definition)&&!_hasSlotControls&&ArtRows.Count==0){_lastLeft=Down(1);_lastRight=Down(2);return;}
        GetWindowThreadProcessId(GetForegroundWindow(),out uint process);if(process!=(uint)Environment.ProcessId){_gesture=null;Stroke.Clear();_dragId=null;_lastLeft=Down(1);_lastRight=Down(2);return;}
        bool left=Down(1),right=Down(2);var pressed=PressedKeys;pressed.Clear();
        if(_captureId is not null){for(int k=8;k<256;k++)ReadPressedKey(k);}
        else {if(BookGeometry.HasTabs(h.Definition))foreach(int k in NavigationKeys)ReadPressedKey(k);if(ArtRows.Count>0)foreach(int k in CanvasKeys)ReadPressedKey(k);}
        if(_captureId is {} id){var row=Rows.FirstOrDefault(r=>r.Element.Id==id);if(row is null)_captureId=null;else if(pressed.Contains(27)){_captureId=null;h.Revision++;}else{string? key=pressed.Contains(8)?row.Element.DefaultKey:pressed.Select(KeyName).FirstOrDefault(k=>k is not null);if(key is not null){string? conflict=KeyBindings.Conflict(id,key)??h.Definition.Pages.SelectMany(p=>p.Elements).FirstOrDefault(e=>e.Kind==BookControl.Keybind&&e.Id!=id&&(e.Text.Length>0?e.Text:e.DefaultKey)==key)?.Label;if(conflict is not null)u.SetText(row.Widget,"SetButtonLabel","Used by "+conflict);else{KeyBindings.Assign(id,key);h.Update(id,e=>e with{Text=key});_captureId=null;row.Element.OnChange?.Invoke(new(h,id,BookControl.Keybind,0,false,key,0));}}}_lastLeft=left;_lastRight=right;return;}
        if(h.Definition.ShowTabs&&(pressed.Contains(65)||pressed.Contains(68))&&!Rows.Any(r=>r.Element.Kind==BookControl.TextInput&&r.Control!=0&&(r.Frame.Focused||u.Boolean(r.Control,"HasFocusedDescendants")))){
            var nav=Rows.FirstOrDefault(r=>r.Element.Enabled&&r.Element.Tooltip==(pressed.Contains(68)?"Next categories":"Previous categories"));if(nav is not null){h.GoToPage(int.Parse(nav.Element.Id[5..]));return;}
        }
        if(!_hasSlotControls&&ArtRows.Count==0){_lastLeft=left;_lastRight=right;return;}
        var layout=u.DefaultObject("/Script/UMG.Default__WidgetLayoutLibrary");var mouse=u.Vector(u.Call(layout,"GetMousePositionOnPlatform"),"GetMousePositionOnPlatform");var point=new UiPoint(mouse.X,mouse.Y);
        if(left&&!_lastLeft){var slot=Rows.LastOrDefault(r=>r.Element.Kind==BookControl.Slot&&r.Element.Enabled&&Hit(r.Widget,point));_dragId=slot?.Element.Id;_press=point;}
        if(!left&&_lastLeft&&_dragId is {} sourceId){if(Math.Abs(point.X-_press.X)+Math.Abs(point.Y-_press.Y)>8){var target=Rows.LastOrDefault(r=>r.Element.Kind==BookControl.Slot&&Hit(r.Widget,point));if(target is not null)Drop(sourceId,target.Element.Id);foreach(var row in Rows.Where(r=>r.Element.Kind==BookControl.Slot).ToArray())Rows[Rows.IndexOf(row)]=row with{Serial=u.Serial(row.Widget,"EventSerial")};}_dragId=null;}
        var art=ArtRows.LastOrDefault(a=>Hit(a.Widget,point));
        if(art is not null&&art.Element.Canvas is {} canvas){var hit=Locate(art.Widget,point);var local=new UiPoint(hit.Local.X*canvas.Width/art.Width,hit.Local.Y);var world=canvas.ToWorld(local);
            if(left&&!_lastLeft){_gesture=canvas;_marker=canvas.MarkerAt(local);Stroke.Clear();if(_marker is null&&canvas.DrawingEnabled&&canvas.Inside(world))Stroke.Add(world);_lastPointer=local;}
            if(right&&!_lastRight){_gesture=canvas;_lastPointer=local;}
            if(right&&_gesture==canvas){canvas.SetView(canvas.Zoom,new(canvas.Pan.X- (local.X-_lastPointer.X)/canvas.Zoom,canvas.Pan.Y-(local.Y-_lastPointer.Y)/canvas.Zoom));_lastPointer=local;}
            if(left&&_gesture==canvas){if(_marker is not null)canvas.MoveMarker(_marker,world);else if(Stroke.Count>0&&Stroke.Count<256&&canvas.Inside(world)&&Math.Abs(world.X-Stroke[^1].X)+Math.Abs(world.Y-Stroke[^1].Y)>2)Stroke.Add(world);}
            if(!left&&_lastLeft&&_gesture==canvas){if(Stroke.Count>=2){try{canvas.AddStroke(Stroke.ToArray());}catch(Exception ex){Notifications.Show("Drawing",ex.Message);}}else if(_marker is {} marker)canvas.OnClick?.Invoke(new(marker,world));else canvas.Click(world);Stroke.Clear();_gesture=null;_marker=null;for(int i=0;i<ArtRows.Count;i++)ArtRows[i]=ArtRows[i] with{Revision=-1};}
            bool typing=Rows.Any(r=>r.Element.Kind==BookControl.TextInput&&r.Control!=0&&r.Frame.Focused);if(!typing){if(pressed.Contains(187)||pressed.Contains(107))canvas.SetView(Math.Min(8,canvas.Zoom*1.2),canvas.Pan);if(pressed.Contains(189)||pressed.Contains(109))canvas.SetView(Math.Max(.25,canvas.Zoom/1.2),canvas.Pan);}
        }else if(!left&&_lastLeft){Stroke.Clear();_gesture=null;_marker=null;for(int i=0;i<ArtRows.Count;i++)ArtRows[i]=ArtRows[i] with{Revision=-1};}
        if(!right&&_lastRight&&_gesture is not null&&Stroke.Count==0)_gesture=null;
        if(now-_lastArt>.066){for(int i=0;i<ArtRows.Count;i++){var a=ArtRows[i];if(a.Element.Canvas is {} session&&(session.Revision!=a.Revision||session==_gesture)){RenderArtwork(u,a.Widget,a.Element,a.Width);ArtRows[i]=a with{Revision=session.Revision};}}_lastArt=now;}
        _lastLeft=left;_lastRight=right;
    }
}
