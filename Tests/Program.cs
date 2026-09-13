using OblivionUI;
using System.Globalization;

int checks=0;
void Check(bool value) { if (!value) throw new Exception($"Check {checks+1} failed"); checks++; }
void Throws(Action action) { try { action(); } catch (ArgumentException) { checks++; return; } throw new Exception("Expected rejected input"); }
var q=new NotificationQueue();
q.Add("Left", "Body",NotificationSide.Left,1);
Check(q.Frame(0).Left=="@oui|-380.0|Left|Body");
Check(q.Frame(.16).Left=="@oui|-190.0|Left|Body");
Check(q.Frame(.32).Left=="@oui|-0.0|Left|Body" || q.Frame(.32).Left=="@oui|0.0|Left|Body");
Check(q.Frame(1).Right=="");
Check(q.Frame(1.48).Left=="@oui|-190.0|Left|Body");
Check(q.Frame(1.65)==("",""));
var cancel=q.Add("Cancelled","",NotificationSide.Right,2); cancel.Dispose();
q.Add("Right","",NotificationSide.Right,1);
Check(q.Frame(2).Right=="@oui|380.0|Right|");
q.Clear(); Check(q.Frame(3)==("",""));
for (int i=0;i<20;i++) q.Add(i.ToString(),"",NotificationSide.Left,1);
Check(q.Frame(4).Left.Contains("|4|"));
q.Clear(); var active=q.Add("Active","",NotificationSide.Right,1); q.Frame(5); active.Dispose(); Check(q.Frame(5.1)==("",""));
Throws(()=>Notifications.Show("x",duration:double.NaN));
Throws(()=>Notifications.Show("x",duration:.5));
Throws(()=>Notifications.Show("x",duration:31));
Throws(()=>Notifications.Show("x",side:(NotificationSide)99));
Hud.SetPrompt("[F2] Open store");
Notifications.Show("Title|injected", "body\nline",NotificationSide.Right);
var packet=Hud.Queue.Frame(0).Right;
Check(packet=="@oui|380.0|Title injected|body line");
Check(Hud.Prompt=="[F2] Open store");
Hud.SetPrompt(null); Check(Hud.Queue.Frame(.4).Right!="");
Notifications.Clear(); Check(Hud.Queue.Frame(.5)==("",""));
CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("de-DE");
Notifications.Show("Number","",NotificationSide.Right);
Check(Hud.Queue.Frame(1).Right.StartsWith("@oui|380.0|"));
Hud.Reset(); Notifications.Show(new string('x',500),new string('y',500));
var parts=Hud.Queue.Frame(2).Right.Split('|'); Check(parts[2].Length==64 && parts[3].Length==180);
Hud.Reset(); Check(Hud.Prompt=="" && Hud.Queue.Frame(3)==("",""));
Throws(()=>Books.Open(new()));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="same"},new(){Id="same"}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="s",Kind=BookControl.Slider,Minimum=5,Maximum=2}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="d",Kind=BookControl.Dropdown,Options=["A"],SelectedIndex=1}]}]}));
var options=new[]{"A","B"};
var book=Books.Open(new(){Pages=[new(){Elements=[new(){Id="d",Kind=BookControl.Dropdown,Options=options}]},new()]});
options[0]="Changed outside"; Check(book.Definition.Pages[0].Elements[0].Options[0]=="A");
book.GoToPage(1); Check(book.Page==1); Throws(()=>book.GoToPage(2));
book.Update("d",x=>x with {SelectedIndex=1}); Check(book.Definition.Pages[0].Elements[0].SelectedIndex==1);
Throws(()=>book.Update("d",x=>x with {Id="renamed"}));
Check(book.Definition.Pages[0].Elements[0].Id=="d");
var replacement=Books.Open(new(){Pages=[new()]}); Check(!book.IsOpen && replacement.IsOpen); replacement.Dispose(); Check(!replacement.IsOpen);
Throws(()=>Books.Open(new(){Title=null!,Pages=[new()]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="null-text",Text=null!}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="overflow",Kind=BookControl.Slider,Maximum=double.MaxValue}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="underflow",Kind=BookControl.Slider,Step=double.Epsilon}]}]}));
// Privacy rules are evaluated afresh, independently of speech activity/linger.
Check(SpeakerVisibility.Eligible(true,false,false,true,true));
Check(!SpeakerVisibility.Eligible(true,false,true,true,true));
Check(!SpeakerVisibility.Eligible(true,true,false,true,true));
Check(!SpeakerVisibility.Eligible(false,false,false,true,true));
Check(!SpeakerVisibility.Eligible(true,false,false,false,true));
Check(!SpeakerVisibility.Eligible(true,false,false,true,false));
Check(SpeakerVisibility.OnScreen(true,500,300,1920,1080));
Check(!SpeakerVisibility.OnScreen(false,500,300,1920,1080));
Check(!SpeakerVisibility.OnScreen(true,-100,300,1920,1080));
Check(!SpeakerVisibility.OnScreen(true,double.NaN,300,1920,1080));
SpeakerIndicators.Publish(true,new[]{"one","one","", "two"});
Check(SpeakerIndicators.Snapshot().Local && SpeakerIndicators.Snapshot().Players.SequenceEqual(new[]{"one","two"}));
SpeakerIndicators.Clear();Check(!SpeakerIndicators.Snapshot().Local && SpeakerIndicators.Snapshot().Players.Length==0);
SpeakerIndicators.Publish(false,Enumerable.Range(0,100).Select(i=>i.ToString()));
Check(SpeakerIndicators.Snapshot().Players.Length==64);
SpeakerIndicators.Clear();
// Side placement validation and narrow-layout bounds use the runtime's actual geometry rules.
Throws(()=>Books.Open(new(){Placement=(BookPlacement)99,Pages=[new()]}));
foreach(var placement in Enum.GetValues<BookPlacement>()) {
    var sideBook=Books.Open(new(){Placement=placement,Pages=[new()]});
    Check(sideBook.Definition.Placement==placement); sideBook.Dispose();
    foreach(var layout in Enum.GetValues<BookLayout>()) {
        var page=new BookPage {Layout=layout,Gap=16};
        var element=new BookElement {Id="bounds",X=300,Width=330};
        foreach(var index in new[]{0,1,2}) {
            var (x,width)=BookGeometry.Row(placement,page,element,index);
            Check(x>=0 && width>0 && x+width<=BookGeometry.ContentWidth(placement)+.0001);
        }
    }
}
Check(BookGeometry.Layout(BookPlacement.Center,BookLayout.TwoColumns)==BookLayout.TwoColumns);
Check(BookGeometry.Layout(BookPlacement.Left,BookLayout.TwoColumns)==BookLayout.List);
Check(BookGeometry.Layout(BookPlacement.Right,BookLayout.Custom)==BookLayout.Custom);
Check(BookGeometry.Row(BookPlacement.Left,new(),new(){Id="width"},0).Width==396);
Check(BookGeometry.Row(BookPlacement.Center,new(),new(){Id="width"},0).Width==610);
Throws(()=>Books.Open(new(){Placement=BookPlacement.Popup,Pages=[new(),new()]}));
Throws(()=>Popups.Show(new(){Message=null!}));
Throws(()=>Popups.Show(new(){Message=new string('x',4001)}));
Throws(()=>Popups.Show(new(){ButtonLabel=""}));
Throws(()=>Popups.Show(new(){ImagePath="https://example.com/image.png"}));
var popup=Popups.Show(new(){Title="Quest Updated",Heading="New task",Message="Line one\nLine two",ImagePath="/Game/OblivionUI/Book/T_Book.T_Book"});
Check(popup.Definition.Placement==BookPlacement.Popup && popup.Definition.CloseLabel=="Continue");
Check(popup.Definition.Pages[0].Elements[0].Kind==BookControl.Image);
popup.Dispose();
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="image",Kind=BookControl.Image}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="image",Kind=BookControl.Image,ImagePath="/Game/../Secret.Asset"}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="image",ImageAspectRatio=double.NaN}]}]}));
Throws(()=>Books.Open(new(){Pages=[new(){Elements=[new(){Id="color",Tint=new(float.NaN,0,0)}]}]}));
var points=new[]{new UiPoint(10,10),new UiPoint(90,90)};
var art=Books.Open(new(){Pages=[new(){Elements=[new(){Id="art",Kind=BookControl.Canvas,Width=100,Height=100,Shapes=[new(){Kind=CanvasShapeKind.Polyline,Points=points}]}]}]});
points[0]=new(999,999);
Check(art.Definition.Pages[0].Elements[0].Shapes[0].Points[0]==new UiPoint(10,10));
Throws(()=>art.Update("art",e=>{e.Shapes[0].Points[0]=new(-1,0);return e;}));
Check(art.Definition.Pages[0].Elements[0].Shapes[0].Points[0]==new UiPoint(10,10));
Throws(()=>art.Update("art",e=>e with {Shapes=[new(){Points=[new(0,0),new(101,50)]}]}));
Throws(()=>art.Update("art",e=>e with {Shapes=[new(){Thickness=0,Points=[new(0,0),new(50,50)]}]}));
Throws(()=>art.Update("art",e=>e with {Shapes=[new(){Kind=CanvasShapeKind.Polyline,Points=Enumerable.Repeat(new UiPoint(0,0),514).ToArray()}]}));
art.Dispose();
// Stateful toolkit checks, including the actual demo definitions.
Throws(()=>UiThemes.Current=new(){FontScale=double.NaN});Throws(()=>UiThemes.Current=new(){Scale=10});
UiThemes.Current=new(){Accent=new(.2f,.3f,.4f),FontScale=1.1};Check(UiThemes.Current.FontScale==1.1);UiThemes.Current=new();
using(var panel=HudPanels.Show(new(){Title="Queue",Progress=.3})){panel.Update(d=>d with{Progress=.8});Check(panel.Definition.Progress==.8);Throws(()=>panel.Update(d=>d with{Progress=2}));Check(panel.Definition.Progress==.8);}Check(HudPanels.Active.Count==0);
Throws(()=>Dialogs.Choose("Title","Body",new DialogChoice{Id="x",Label="X"},new DialogChoice{Id="x",Label="Duplicate"}));
var choice=Dialogs.Choose("Team","Choose",new DialogChoice{Id="blue",Label="Blue"},new DialogChoice{Id="yellow",Label="Yellow"});Check(choice.Definition.Pages[0].Elements.Length==3);choice.Dispose();
KeyBindings.Assign("test-action","F8");Check(KeyBindings.Conflict("test-other","f8")=="test-action");Throws(()=>KeyBindings.Assign("test-other","F8"));KeyBindings.Release("test-action");
var drawing=new CanvasSession(200,100){Markers=[new(){Id="m",Position=new(50,50)}]};drawing.AddStroke(new(0,0),new(50,50));Check(drawing.Strokes.Length==1);var drawCopy=drawing.Strokes;drawCopy[0].Points[0]=new(999,0);Check(drawing.Strokes[0].Points[0].X==0);drawing.Undo();Check(drawing.Strokes.Length==0);drawing.Redo();Check(drawing.Strokes.Length==1);drawing.SetView(2,new(10,5));Check(drawing.ToWorld(drawing.ToView(new(80,40)))==new UiPoint(80,40));drawing.Clear();Check(drawing.Strokes.Length==0);drawing.Undo();Check(drawing.Strokes.Length==1);Throws(()=>drawing.AddStroke(new(-1,0),new(1,1)));Throws(()=>drawing.SetView(0,new()));
OblivionUI.Client.ToolkitDemo.Open();Check(Books.Current!.Definition.ShowTabs&&Books.Current.Definition.Pages.Length==5);
var demo=Books.Current;var drop=new SlotDropEvent(demo,"bag","hand","Iron sword");Check(demo.Definition.OnDrop!(drop));demo.Dispose();
var menu=ItemMenus.Open(new(){Items=Enumerable.Range(0,61).Select(n=>new MenuItem{Id=n.ToString(),Name="Sword "+n,Price=n,Quantity=1}).ToArray()});Check(menu.Definition.Pages.Length==2);Check(menu.Definition.Pages[0].Elements.Count(e=>e.Kind==BookControl.ItemCard)==30);var next=menu.Definition.Pages[0].Elements.First(e=>e.Id=="shop-more");next.OnChange!(new(menu,next.Id,next.Kind,0,false,"",0));Check(menu.Definition.Pages[0].Elements.Any(e=>e.Id=="shop-back"&&e.Enabled));menu.Dispose();
var slots=Books.Open(new(){OnDrop=e=>true,Pages=[new(){Elements=[new(){Id="source",Kind=BookControl.Slot,DragGroup="items",DragPayload="axe"},new(){Id="target",Kind=BookControl.Slot,DragGroup="items"},new(){Id="other",Kind=BookControl.Slot,DragGroup="spells"}]}]});
Check(!SlotMoves.Request(slots,"source","other"));Check(SlotMoves.Request(slots,"source","target"));Check(slots.Definition.Pages[0].Elements[0].DragPayload==""&&slots.Definition.Pages[0].Elements[1].DragPayload=="axe");Check(!SlotMoves.Request(slots,"source","target"));slots.Dispose();
string profile="checks-"+Guid.NewGuid().ToString("N");drawing.Save(profile);drawing.Clear();drawing.Load(profile);Check(drawing.Strokes.Length==1);KeyBindings.Assign("check-profile","F10");KeyBindings.Save(profile);KeyBindings.Release("check-profile");KeyBindings.Load(profile);Check(KeyBindings.Get("check-profile")=="F10");KeyBindings.Release("check-profile");
var savedRoot=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OblivionUI","Profiles");System.IO.File.Delete(System.IO.Path.Combine(savedRoot,profile+"-drawing.json"));System.IO.File.Delete(System.IO.Path.Combine(savedRoot,profile+"-keys.json"));
Throws(()=>drawing.Save("../escape"));
int closedShop=0;
var xpMenu=ItemMenus.Open(new(){CurrencyLabel="XP",Subtitle="40 XP available",OnClose=()=>closedShop++,Items=[new(){Id="iron",Name="Iron sword",Price=20}]});
Check(xpMenu.Definition.Pages[0].Elements.Single(e=>e.Id=="item-iron").Description.Contains("20 XP"));
Check(xpMenu.Definition.Pages[1].Elements.Single(e=>e.Id=="detail-text").Description.Contains("20 XP"));
xpMenu.Definition.OnClose!();Check(closedShop==1);xpMenu.Dispose();
Throws(()=>ItemMenus.Open(new(){CurrencyLabel=""}));
var categoryShop=ItemMenus.Open(new(){Items=[new(){Id="sword",Name="Sword",Category="Weapons"},new(){Id="shield",Name="Shield",Category="Armour"}]});
Check(categoryShop.Definition.ShowTabs);
int weapons=Array.FindIndex(categoryShop.Definition.Pages,p=>p.Title=="Weapons");categoryShop.GoToPage(weapons);
Check(categoryShop.Definition.Pages[weapons].Elements.Count(e=>e.Kind==BookControl.ItemCard)==1);
var sword=categoryShop.Definition.Pages[weapons].Elements.Single(e=>e.Kind==BookControl.ItemCard);sword.OnChange!(new(categoryShop,sword.Id,sword.Kind,0,false,"",0));
Check(!categoryShop.Definition.Pages[categoryShop.Page].ShowInTabs);
var back=categoryShop.Definition.Pages[categoryShop.Page].Elements.Single(e=>e.Id=="detail-back");back.OnChange!(new(categoryShop,back.Id,back.Kind,0,false,"",0));Check(categoryShop.Page==weapons);categoryShop.Dispose();
// Tab groups must not create empty artwork slots, including the final group and hidden details.
foreach(var placement in new[]{BookPlacement.Center,BookPlacement.Left,BookPlacement.Right}) {
    foreach(int visibleCount in new[]{0,1,2,3,5,6,7,12,31}) {
        var definition=new BookDefinition {ShowTabs=true,Placement=placement,Pages=Enumerable.Range(0,visibleCount).Select(i=>new BookPage{Title="Category "+i}).Append(new BookPage {ShowInTabs=false,Title="Details"}).ToArray()};
        Check(BookGeometry.HasTabs(definition)==(visibleCount>1));
        foreach(int page in Enumerable.Range(0,definition.Pages.Length)) {
            var group=BookGeometry.Tabs(definition,page);
            Check(group.Count==Math.Min(5,visibleCount-group.Start));
            Check(group.Pages.All(i=>definition.Pages[i].ShowInTabs));
            if(visibleCount>0){Check(group.Start<=group.Selected && group.Selected<group.Start+group.Count);Check(group.Width>0 && group.X*2+group.Width*group.Count<=BookGeometry.ContentWidth(placement)+.001);Check(Math.Abs(group.Width/group.Height-BookGeometry.TabAspect)<.001);}
        }
        var five=BookGeometry.Tabs(definition with{Pages=Enumerable.Range(0,5).Select(_=>new BookPage()).ToArray()},0);
        var current=BookGeometry.Tabs(definition,0);Check(current.Width==five.Width && current.Height==five.Height && current.X==five.X);
    }
}
Check(!BookGeometry.HasTabs(new(){ShowTabs=false,Pages=[new(),new()]}));
Check(!BookGeometry.HasTabs(new(){ShowTabs=true,Placement=BookPlacement.Popup,Pages=[new(),new()]}));

string? craftedRecipe=null;
var crafting=Crafting.Open(new(){Categories=[new(){Name="Weapons",Icon="weapons",Recipes=[new(){Id="dagger",Name="Iron dagger",CanCraft=true,Ingredients=[new("Iron",2,1)]},new(){Id="practice",Name="Practice",CanCraft=true}]}],OnCraft=id=>craftedRecipe=id});
Check(crafting.Definition.Pages.Length==3);
Check(crafting.Definition.Pages[0].Elements[0].RecipePreview.Contains("Iron: 1 / 2"));
Check(crafting.Definition.Pages[0].Elements[0].RecipePreview.Contains("Recipe unavailable."));
Check(crafting.Definition.Pages[0].Elements[1].RecipePreview.Contains("No materials required"));
Check(crafting.Definition.Pages[0].Elements.All(e=>e.Kind==BookControl.RecipeRow));
Check(!crafting.Definition.Pages[1].ShowInTabs);
var blocked=crafting.Definition.Pages[1].Elements.Last();Check(!blocked.Enabled);
blocked.OnChange!(new(crafting,blocked.Id,blocked.Kind,0,false,"",0));Check(craftedRecipe==null&&crafting.IsOpen);
var ready=crafting.Definition.Pages[2].Elements.Last();Check(ready.Enabled);
ready.OnChange!(new(crafting,ready.Id,ready.Kind,0,false,"",0));Check(craftedRecipe=="practice"&&!crafting.IsOpen);
var noHandler=Crafting.Open(new(){Categories=[new(){Name="Items",Recipes=[new(){Id="item",Name="Item",CanCraft=true}]}]});
Check(!noHandler.Definition.Pages[1].Elements.Last().Enabled);noHandler.Dispose();
bool duplicateRejected=false;try{Crafting.Open(new(){Categories=[new(){Name="Items",Recipes=[new(){Id="same",Name="One"},new(){Id="same",Name="Two"}]}]});}catch(ArgumentException){duplicateRejected=true;}Check(duplicateRejected);
// Exercise the native parameter writer against explicit x64 ABI bytes, including field
// boundaries and rejection of an incorrect reflected width before native dispatch.
var fieldBytes=Enumerable.Repeat((byte)0xCC,32).ToArray();
Check(OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(4,16),(1d,-2d)));
Check(Convert.ToHexString(fieldBytes.AsSpan(4,16))=="000000000000F03F00000000000000C0");
Check(fieldBytes.Take(4).Concat(fieldBytes.Skip(20)).All(b=>b==0xCC));
Check(OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(0,16),(.5f,1f,0f,-1f)));
Check(Convert.ToHexString(fieldBytes.AsSpan(0,16))=="0000003F0000803F00000000000080BF");
Check(OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(0,8),(nint)0x12345678));
Check(Convert.ToHexString(fieldBytes.AsSpan(0,8))=="7856341200000000");
Check(OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(0,1),true)&&fieldBytes[0]==1);
Check(OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(0,1),false)&&fieldBytes[0]==0);
bool abiRejected=false;try{OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(0,8),1f);}catch(NotSupportedException){abiRejected=true;}Check(abiRejected);
Check(!OblivionUI.Client.NativeArguments.TryWrite(fieldBytes.AsSpan(0,16),"text"));
var steady=new NotificationQueue();steady.Add("Stable","Body",NotificationSide.Right,30);steady.Frame(0);
Check(ReferenceEquals(steady.Frame(1).Right,steady.Frame(2).Right));
Console.WriteLine($"All {checks} notification, book and native argument checks passed.");
var nativeLayouts=File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"BookNative.tsv"));
Check(nativeLayouts.Contains("SetBackgroundColor|16|InBackgroundColor:0:16"));
Console.WriteLine("Transparent button native-call metadata check passed.");
