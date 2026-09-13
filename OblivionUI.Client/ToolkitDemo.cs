namespace OblivionUI.Client;
internal static class ToolkitDemo
{
    private static HudPanelHandle? _hud;private static double _start,_tick;
    private static readonly CanvasSession Drawing=new(610,280){DrawingEnabled=true,Markers=[new(){Id="camp",Label="Camp",Position=new(300,100)}],Regions=[new(){Id="map",Position=new(0,0),Width=610,Height=280}],OnClick=e=>Notifications.Show("Map",e.Id??"Canvas")};
    internal static void Tick(double now){if(_hud is null||now-_tick<.5)return;_tick=now;_hud.Update(d=>d with{Progress=((now-_start)%20)/20,Text="Waiting for another player\nDemo countdown: "+(20-(int)((now-_start)%20))+" seconds"});}
    private static BookElement Button(string id,string label,Action<BookEvent> callback)=>new(){Id=id,Kind=BookControl.Button,ButtonLabel=label,Height=48,OnChange=callback};
    internal static void Open()
    {
        var pending=UiThemes.Current;
        Books.Open(new(){Title="UI Workshop",ShowTabs=true,OnDrop=e=>{Notifications.Show("Demo slot move",e.Payload+" moved to "+e.TargetId);return true;},Pages=[
            new(){Title="Controls",Subtitle="Focus with Tab or a controller; hover for help",Elements=[
                new(){Id="bar",Kind=BookControl.ProgressBar,Label="Crafting progress",Description="A progress bar",Value=.65,Tooltip="Mods update Value to show work, timers or objective progress."},
                new(){Id="ring",Kind=BookControl.ProgressRing,Label="Capture progress",Value=.4,Height=180},
                new(){Id="advanced",Kind=BookControl.Section,Label="Advanced settings",Checked=true,Height=48},
                new(){Id="advice",Kind=BookControl.Toggle,SectionId="advanced",Label="Show advice",Checked=true,Tooltip="This option sits inside a collapsible section."},
                new(){Id="workshop-action",Kind=BookControl.Keybind,Label="Interaction key",Text=KeyBindings.Get("workshop-action","E"),DefaultKey="E",Tooltip="Click, then press a key. Escape cancels; Backspace restores the default."},
                new(){Id="workshop-menu",Kind=BookControl.Keybind,Label="Menu key",Text=KeyBindings.Get("workshop-menu","F6"),DefaultKey="F6"},
                Button("save-keys","Save demo keybinds",e=>KeyBindings.Save("workshop")),
                Button("choices","Try a choice dialog",e=>Dialogs.Choose("Choose your team","Which side will you fight for?",new(){Id="blue",Label="Blue team",OnChoose=()=>Open()},new(){Id="yellow",Label="Yellow team",OnChoose=()=>Open()}))
            ]},
            new(){Title="Items & slots",Subtitle="Drag between slots, or select a source then a target",Elements=[
                Button("shop","Open example shop",e=>ItemMenus.Open(new(){Title="Arena outfitter",Items=Enumerable.Range(0,18).Select(i=>new MenuItem{Id="demo-"+i,Name=new[]{"Iron sword","Woodcutter axe","Leather armour","Fire spell","Steel shield","Healing spell"}[i%6]+" "+(i/6+1),Category=new[]{"Weapons","Weapons","Armour","Spells","Armour","Spells"}[i%6],Price=25+i*17,Quantity=8,Description="Example item. Select it to view details and try the purchase callback.",ImagePath="/Game/OblivionUI/Book/T_Book.T_Book"}).ToArray(),OnBuy=(item,count)=>Notifications.Show("Demo purchase",$"Requested {count} x {item.Name}")})),
                new(){Id="bag",Kind=BookControl.Slot,Label="Backpack",ButtonLabel="Empty",DragGroup="equipment",DragPayload="Iron sword",Tooltip="Drag this to an empty slot, or select it and activate a target."},
                new(){Id="hand",Kind=BookControl.Slot,Label="Main hand",ButtonLabel="Empty",DragGroup="equipment"},
                new(){Id="storage",Kind=BookControl.Slot,Label="Storage",ButtonLabel="Empty",DragGroup="equipment"}
            ]},
            new(){Title="Drawing",Subtitle="Left: draw/drag marker. Right: pan. +/-: zoom",Elements=[
                new(){Id="sketch",Kind=BookControl.Canvas,Width=610,Height=280,Canvas=Drawing,Shapes=[new(){Kind=CanvasShapeKind.Rectangle,Points=[new(0,0),new(610,280)],Color=new(.4f,.25f,.1f,.08f)}]},
                Button("zoom-in","Zoom in",e=>Drawing.SetView(Math.Min(8,Drawing.Zoom*1.25),Drawing.Pan)),Button("zoom-out","Zoom out",e=>Drawing.SetView(Math.Max(.25,Drawing.Zoom/1.25),Drawing.Pan)),
                Button("pan-left","Pan left",e=>Drawing.SetView(Drawing.Zoom,new(Drawing.Pan.X-30,Drawing.Pan.Y))),Button("pan-right","Pan right",e=>Drawing.SetView(Drawing.Zoom,new(Drawing.Pan.X+30,Drawing.Pan.Y))),
                Button("undo","Undo stroke",e=>Drawing.Undo()),Button("redo","Redo stroke",e=>Drawing.Redo()),Button("clear","Clear drawing",e=>Drawing.Clear()),
                Button("save-art","Save drawing",e=>Drawing.Save("workshop")),Button("load-art","Load drawing",e=>{try{Drawing.Load("workshop");}catch(Exception ex){Notifications.Show("Drawing",ex.Message);}}),Button("reset-view","Reset view",e=>Drawing.SetView(1,new(0,0)))
            ]},
            new(){Title="HUD panels",Subtitle="These remain visible while you move",Elements=[
                Button("show-hud","Show arena status demo",e=>{_hud?.Dispose();_hud=HudPanels.Show(new(){Title="Arena queue (demo)",Text="Waiting for another player",X=.02,Y=.2,Width=320});_start=Environment.TickCount64/1000d;e.Book.Dispose();}),
                Button("right-hud","Move demo HUD right",e=>{_hud?.Update(d=>d with{X=.8});e.Book.Dispose();}),
                Button("hide-hud","Remove demo HUD",e=>{_hud?.Dispose();_hud=null;})
            ]},
            new(){Title="Theme",Subtitle="Adjust, then apply the shared theme",Elements=[
                new(){Id="theme-scale",Kind=BookControl.Slider,Label="UI scale",Minimum=.5,Maximum=1.5,Value=pending.Scale,Step=.05,OnChange=e=>pending=pending with{Scale=e.Value}},
                new(){Id="theme-font",Kind=BookControl.Slider,Label="Font size",Minimum=.75,Maximum=1.5,Value=pending.FontScale,Step=.05,OnChange=e=>pending=pending with{FontScale=e.Value}},
                new(){Id="theme-spacing",Kind=BookControl.Slider,Label="Spacing",Minimum=.5,Maximum=2,Value=pending.Spacing,Step=.1,OnChange=e=>pending=pending with{Spacing=e.Value}},
                new(){Id="theme-speed",Kind=BookControl.Slider,Label="Animation speed",Minimum=.1,Maximum=3,Value=pending.AnimationSpeed,Step=.1,OnChange=e=>pending=pending with{AnimationSpeed=e.Value}},
                new(){Id="theme-paper",Kind=BookControl.Dropdown,Label="Paper style",Options=["Book","Torn parchment"],SelectedIndex=pending.PaperImagePath.Contains("Notification")?1:0,OnChange=e=>pending=pending with{PaperImagePath=e.SelectedIndex==0?"":"/Game/OblivionUI/T_NotificationParchment.T_NotificationParchment"}},
                new(){Id="theme-ink",Kind=BookControl.Dropdown,Label="Ink and accent",Options=["Brown and brass","Blue ink","Dark red"],OnChange=e=>pending=pending with{Ink=e.SelectedIndex==1?new(.03f,.08f,.2f):e.SelectedIndex==2?new(.2f,.025f,.025f):new(.09f,.055f,.03f),Accent=e.SelectedIndex==1?new(.1f,.3f,.5f):e.SelectedIndex==2?new(.5f,.1f,.08f):new(.48f,.3f,.1f)}},
                Button("theme-apply","Apply theme",e=>UiThemes.Current=pending),Button("theme-reset","Restore default theme",e=>{UiThemes.Current=new();Open();})
            ]}
        ]});
    }
}
