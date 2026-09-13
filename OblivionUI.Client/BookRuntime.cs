using OblivionMp.Sdk;

namespace OblivionUI.Client;

internal static partial class BookRuntime
{
    private sealed record Row(BookElement Element,nint Widget,nint Control,int Serial)
    {
        internal RowFrameState Frame {get;}=new();
    }
    private static UnrealBookBridge? _ue;
    private static BookHandle? _handle;
    private static nint _book,_controller,_host,_controlClass,_library;
    private static bool _cursor,_input;
    private static bool _lookBlocked,_moveBlocked;
    private static int _revision=-1,_close,_previous,_next;
    private static readonly List<Row> Rows=[];
    internal static void Frame(bool inWorld)
    {
        lock(Hud.Gate)
        {
            if(!inWorld) { Reset(); return; }
            var wanted=Books.Current;
            if(wanted!=_handle || wanted?.Closed==true) { Close(); if(wanted?.Closed==true) { if(Books.Current==wanted) Books.Current=null; return; } }
            if(wanted is null) return;
            _ue ??= new UnrealBookBridge();
            if(_book==0) Open(wanted);
            // A replaced game-message widget indicates world travel. Do not use old UObject pointers.
            if(_ue.FindWidget()!=_host) { Abandon(); wanted.Dispose(); return; }
            if(_revision!=wanted.Revision || _themeRevision!=UiThemes.Revision) Render();
            FeatureFrame();
            PreviewFrame();
            if(wanted!=Books.Current || wanted.Closed)return;
            if(_ue.Serial(_book,"CloseSerial")!=_close) {
                try {wanted.Definition.OnConfirm?.Invoke();} catch(Exception ex) {Console.WriteLine($"[OblivionUI] Popup callback: {ex.Message}");}
                wanted.Dispose(); Close(); return;
            }
            int previous=_ue.Serial(_book,"PreviousSerial"), next=_ue.Serial(_book,"NextSerial");
            if(previous!=_previous || next!=_next) {
                wanted.GoToPage(Math.Clamp(wanted.Page+(next!=_next ? 1 : -1),0,wanted.Definition.Pages.Length-1));
                _previous=previous; _next=next; Render(); return;
            }
            for(int rowIndex=0;rowIndex<Rows.Count;rowIndex++) {
                var row=Rows[rowIndex];
                if(row.Control==0 || row.Element.Kind==BookControl.ProgressBar)continue;
                int serial=_ue.Serial(row.Widget,"EventSerial"); if(serial==row.Serial) continue;
                Rows[rowIndex]=row with {Serial=serial};
                var e=row.Element; if(e.Id.StartsWith("__tab",StringComparison.Ordinal)){wanted.GoToPage(int.Parse(e.Id[5..]));break;}
                if(e.Kind==BookControl.Slot){SlotClick(row);continue;}
                if(e.Kind==BookControl.Keybind){BeginKeyCapture(row);continue;}
                if(!e.Enabled || !e.Visible) continue;
                double value=e.Value; bool check=e.Checked; string text=e.Text; int index=e.SelectedIndex;
                switch(e.Kind) {
                    case BookControl.Slider: value=Math.Clamp(e.Minimum+Math.Round((_ue.Number(row.Control,"GetValue")-e.Minimum)/e.Step)*e.Step,e.Minimum,e.Maximum); _ue.SetText(row.Widget,"SetSliderCurrent",SliderNumber(value,e)); break;
                    case BookControl.Section: check=!e.Checked;wanted.Revision++;break;
                    case BookControl.Toggle: check=_ue.Boolean(row.Control,"IsChecked"); break;
                    case BookControl.Dropdown: index=_ue.Integer(row.Control,"GetSelectedIndex"); text=index>=0 && index<e.Options.Length ? e.Options[index] : ""; break;
                    case BookControl.TextInput: text=_ue.Text(row.Widget,"InputValue"); break;
                }
                var updated=e with {Value=value,Checked=check,Text=text,SelectedIndex=index};
                Rows[rowIndex]=row with {Serial=serial,Element=updated};
                var page=wanted.Definition.Pages[wanted.Page];
                wanted.Definition=wanted.Definition with {Pages=wanted.Definition.Pages.Select(p=>p==page ? p with {Elements=p.Elements.Select(x=>x.Id==e.Id ? updated : x).ToArray()} : p).ToArray()};
                try { e.OnChange?.Invoke(new(wanted,e.Id,e.Kind,value,check,text,index)); } catch(Exception ex) { Console.WriteLine($"[OblivionUI] Book callback {e.Id}: {ex.Message}"); }
                if(wanted.Closed || wanted!=Books.Current || wanted.Revision!=_revision) break;
            }
        }
    }
    private static void Open(BookHandle handle)
    {
        var u=_ue!;
        _host=u.FindWidget();
        if(_host==0) { SDK.GameMessage.ShowGameMessage("", "", ""); _host=u.FindWidget(); }
        if(_host==0) throw new InvalidOperationException("Game HUD has not been created yet.");
        var classField=handle.Definition.Placement switch { BookPlacement.Left=>"BookLeftClass",BookPlacement.Right=>"BookRightClass",BookPlacement.Popup=>"BookPopupClass",_=>"BookClass" };
        var cls=u.Object(_host,classField); _controlClass=u.Object(_host,"BookControlClass");
        if(cls==0 || _controlClass==0) throw new InvalidOperationException("Book assets are missing; install the updated OblivionUI package.");
        _library=u.Library(); _controller=u.Pointer(_host,"GetOwningPlayer");
        if(_controller==0) throw new InvalidOperationException("No local player controller.");
        _cursor=u.Bool(_controller,"bShowMouseCursor");
        _book=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",cls),("OwningPlayer",_controller));
        if(_book==0) throw new InvalidOperationException("Unreal could not create the book.");
        _handle=handle; _revision=-1;
        u.Call(_book,"AddToViewport",("ZOrder",80));
        _input=true; u.Bool(_controller,"bShowMouseCursor",true);
        u.Call(_controller,"SetIgnoreLookInput",("bNewLookInput",true)); _lookBlocked=true;
        u.Call(_controller,"SetIgnoreMoveInput",("bNewMoveInput",true)); _moveBlocked=true;
        u.Call(_library,"SetInputMode_UIOnlyEx",("PlayerController",_controller),("InWidgetToFocus",_book),("InMouseLockMode",(byte)0),("bFlushInput",true));
        _close=u.Serial(_book,"CloseSerial"); _previous=u.Serial(_book,"PreviousSerial"); _next=u.Serial(_book,"NextSerial");
        Books.IsAvailable=true; Console.WriteLine("[OblivionUI] Book opened.");
    }
    private static void Render()
    {
        var u=_ue!; var h=_handle!; var page=h.Definition.Pages[h.Page];
        u.SetText(_book,"SetTitle",page.Title.Length>0 ? page.Title : h.Definition.Title);
        u.SetText(_book,"SetSubtitle",page.Subtitle.Length>0 ? page.Subtitle : h.Definition.Subtitle);
        u.SetText(_book,"SetCloseLabel",h.Definition.CloseLabel);
        u.SetText(_book,"SetPageLabel",$"{h.Page+1} / {h.Definition.Pages.Length}");
        u.Call(u.Object(_book,"PreviousButton"),"SetIsEnabled",("bInIsEnabled",h.Page>0));
        u.Call(u.Object(_book,"NextButton"),"SetIsEnabled",("bInIsEnabled",h.Page+1<h.Definition.Pages.Length));
        foreach(var name in new[]{"PreviousButton","NextButton","PageLabel"})u.Visible(u.Object(_book,name),h.Definition.ShowPageNavigation && h.Definition.Placement!=BookPlacement.Popup && h.Definition.Pages.Length>1);
        var canvas=u.Object(_book,"Content"); u.Call(canvas,"ClearChildren"); Rows.Clear();
        var theme=h.Definition.Theme??UiThemes.Current;ApplyBookTheme(u,theme);
        ClearArtworkCache();ArtRows.Clear();
        int count=0; double y=0,rowHeight=0,extent=h.Definition.Placement==BookPlacement.Popup ? 165 : 610;
        var layout=BookGeometry.Layout(h.Definition.Placement,page.Layout);
        foreach(var e in page.Elements.Where(e=>e.Visible && (e.SectionId.Length==0 || page.Elements.Any(s=>s.Id==e.SectionId && s.Checked)))) {
            var (x,width)=BookGeometry.Row(h.Definition.Placement,page,e,count);
            double top=layout==BookLayout.Custom ? e.Y : y;
            var w=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",_controlClass),("OwningPlayer",_controller));
            var slot=u.Pointer(canvas,"AddChildToCanvas",("Content",w));
            u.Call(slot,"SetPosition",("InPosition",(x,top))); u.Call(slot,"SetSize",("InSize",(width,e.Height)));
            StyleRow(u,w,e,theme);
            if(e.Kind is BookControl.Image or BookControl.Canvas or BookControl.ProgressRing) {
                RenderArtwork(u,w,e.Kind==BookControl.ProgressRing?Ring(e,width):e,width);
                if(e.Kind==BookControl.ProgressRing){u.Visible(u.Object(w,"Label"),true);u.SetText(w,"SetLabel",e.Label+$" {(e.Value-e.Minimum)/(e.Maximum-e.Minimum):P0}");u.Call(u.Object(u.Object(w,"Label"),"Slot"),"SetOffsets",("InOffset",(0f,(float)e.Height-32,0f,32f)));}
                if(e.Canvas is not null)ArtRows.Add(new(e,w,width,e.Canvas.Revision));
                extent=Math.Max(extent,top+e.Height); rowHeight=Math.Max(rowHeight,e.Height); count++;
                if(layout==BookLayout.List || (layout==BookLayout.TwoColumns && count%2==0)) {y+=rowHeight+page.Gap*theme.Spacing;rowHeight=0;}
                continue;
            }
            u.SetText(w,"SetLabel",e.Label); u.SetText(w,"SetDescription",e.Description);
            if(e.Kind==BookControl.Text) {
                var description=u.Object(w,"Description");
                u.Call(u.Object(description,"Slot"),"SetOffsets",("InOffset",(0f,34f,0f,(float)Math.Max(0,e.Height-34))));
                if(e.Description.Length==0)u.Call(u.Object(u.Object(w,"Label"),"Slot"),"SetOffsets",("InOffset",(0f,0f,0f,(float)e.Height)));
            }
            string? name=e.Kind switch { BookControl.Button or BookControl.Section or BookControl.ItemCard or BookControl.RecipeRow or BookControl.Keybind or BookControl.Slot=>"ActionButton",BookControl.ProgressBar=>"ValueProgress",BookControl.Slider=>"ValueSlider",BookControl.Toggle=>"Toggle",BookControl.Dropdown=>"Dropdown",BookControl.TextInput=>"TextInput",_=>null };
            nint control=0;
            if(name is not null) {
                control=u.Object(w,name); u.Visible(control,true); u.Call(control,"SetIsEnabled",("bInIsEnabled",e.Enabled));
                switch(e.Kind) {
                    case BookControl.Section: u.Visible(u.Object(w,"Label"),false);u.Visible(u.Object(w,"Description"),false);u.Call(u.Object(control,"Slot"),"SetOffsets",("InOffset",(0f,0f,0f,38f)));u.SetText(w,"SetButtonLabel",(e.Checked?"- ":"+ ")+e.Label);break;
                    case BookControl.Keybind: u.SetText(w,"SetButtonLabel",e.Text.Length>0?e.Text:e.DefaultKey);break;
                    case BookControl.ItemCard: ConfigureCard(u,w,e);break;
                    case BookControl.RecipeRow: ConfigureRecipeRow(u,w,e,width);break;
                    case BookControl.Slot: u.SetText(w,"SetButtonLabel",e.DragPayload.Length>0?e.DragPayload:e.ButtonLabel);break;
                    case BookControl.ProgressBar: u.Call(control,"SetPercent",("InPercent",ProgressValues.GetValueOrDefault(e.Id)));u.Call(control,"SetFillColorAndOpacity",("InColor",(theme.Accent.R,theme.Accent.G,theme.Accent.B,theme.Accent.A)));u.SetText(w,"SetDescription",e.Description+ $" {(e.Value-e.Minimum)/(e.Maximum-e.Minimum):P0}");break;
                    case BookControl.Button: u.SetText(w,"SetButtonLabel",e.ButtonLabel);if(e.Label.Length==0&&e.Description.Length==0)u.Call(u.Object(control,"Slot"),"SetOffsets",("InOffset",(0f,0f,0f,38f))); break;
                    case BookControl.Slider:
                        u.Call(control,"SetMinValue",("InValue",(float)e.Minimum)); u.Call(control,"SetMaxValue",("InValue",(float)e.Maximum));
                        u.Call(control,"SetStepSize",("InValue",(float)e.Step)); u.Call(control,"SetValue",("InValue",(float)e.Value));
                        foreach(var label in new[]{"SliderMinimum","SliderMiddle","SliderMaximum","SliderCurrent"})u.Visible(u.Object(w,label),true);
                        u.SetText(w,"SetSliderMinimum",SliderNumber(e.Minimum,e));u.SetText(w,"SetSliderMiddle",SliderNumber((e.Minimum+e.Maximum)/2,e));
                        u.SetText(w,"SetSliderMaximum",SliderNumber(e.Maximum,e));u.SetText(w,"SetSliderCurrent",SliderNumber(e.Value,e));break;
                    case BookControl.Toggle: u.Call(control,"SetIsChecked",("InIsChecked",e.Checked)); break;
                    case BookControl.Dropdown:
                        foreach(var option in e.Options) u.Call(control,"AddOption",("Option",option)); u.Call(control,"SetSelectedIndex",("Index",e.SelectedIndex)); break;
                    case BookControl.TextInput: u.SetText(w,"SetInput",e.Text); break;
                }
            }
            Rows.Add(new(e,w,control,u.Serial(w,"EventSerial")));
            extent=Math.Max(extent,top+e.Height); rowHeight=Math.Max(rowHeight,e.Height); count++;
            if(layout==BookLayout.List || (layout==BookLayout.TwoColumns && count%2==0)) { y+=rowHeight+page.Gap*theme.Spacing; rowHeight=0; }
        }
        u.Call(u.Object(_book,"BodySize"),"SetHeightOverride",("InHeightOverride",(float)extent));
        u.Call(u.Object(_book,"BodyScroll"),"ScrollToStart"); BuildTabs(u);
        _hasSlotControls=Rows.Any(r=>r.Element.Kind==BookControl.Slot);
        var first=Rows.FirstOrDefault(r=>r.Control!=0&&r.Element.Enabled&&r.Element.Kind!=BookControl.ProgressBar);if(first is not null)u.Call(first.Control,"SetKeyboardFocus");
        _revision=h.Revision;_themeRevision=UiThemes.Revision;
    }
    private static string SliderNumber(double value,BookElement element)=>value.ToString(element.Minimum<0 ? "+0.######;-0.######;0" : "0.######",System.Globalization.CultureInfo.InvariantCulture);
    private static void Close()
    {
        var old=_handle;
        try {
            if(_ue is not null && _host!=0 && _ue.FindWidget()==_host) {
                try { if(_recipePreview!=0)_ue.Call(_recipePreview,"RemoveFromParent"); if(_book!=0) _ue.Call(_book,"RemoveFromParent"); }
                finally { if(_input) {
                    if(_lookBlocked) _ue.Call(_controller,"SetIgnoreLookInput",("bNewLookInput",false));
                    if(_moveBlocked) _ue.Call(_controller,"SetIgnoreMoveInput",("bNewMoveInput",false));
                    _ue.Call(_library,"SetInputMode_GameOnly",("PlayerController",_controller),("bFlushInput",true)); _ue.Bool(_controller,"bShowMouseCursor",_cursor);
                } }
            }
        } finally { Abandon(); }
        if(old is not null) { old.Dispose(); try {old.Definition.OnClose?.Invoke();} catch(Exception ex) {Console.WriteLine($"[OblivionUI] Book close callback: {ex.Message}");} }
    }
    private static void Abandon() { _book=0; _controller=0; _host=0; _handle=null; _input=false; _lookBlocked=false; _moveBlocked=false; Rows.Clear(); _revision=-1;ClearFeatures(); }
    internal static void Reset() { Close(); lock(Hud.Gate) {Books.Current?.Dispose(); Books.Current=null;} }
}
