namespace OblivionUI.Client;

internal static class BookDemo
{
    internal static void Open(BookPlacement placement=BookPlacement.Center)=>Books.Open(new() {
        Placement=placement,
        Title="The Arena Ledger", Subtitle="Prepare for your next challenge",
        Pages=[
            new() { Elements=[
                new() {Id="toolkit",Kind=BookControl.Button,Label="UI Workshop",ButtonLabel="Try all UI features",OnChange=e=>ToolkitDemo.Open()},
                new() {Id="presentation",Kind=BookControl.Dropdown,Label="Panel position",Description="Choose a full book or a slim side panel.",Options=["Center book","Left panel","Right panel"],SelectedIndex=(int)placement,OnChange=e=>Open((BookPlacement)e.SelectedIndex)},
                new() {Id="popup-demo",Kind=BookControl.Button,Label="Parchment popup",ButtonLabel="Show quest update",OnChange=e=>Popups.Show(new(){Title="Quest Updated",Heading="A Champion in the Making",Subtitle="The Imperial City Arena",Message="Your next challenge awaits. Return to the gatekeeper when you are ready.",ImagePath="/Game/OblivionUI/Book/T_Book.T_Book",ImageAspectRatio=760d/940,OnContinue=()=>Open(placement)})},
                new() { Id="join",Kind=BookControl.Button,Label="Enter the arena",Description="Speak to the gatekeeper when you are ready.",ButtonLabel="Join the next match",OnChange=e=>{Notifications.Show("Arena ledger","Book button works."); e.Book.Dispose();} },
                new() { Id="difficulty",Kind=BookControl.Dropdown,Label="Match difficulty",Description="Choose the challenge that suits your champion.",Options=["Novice","Gladiator","Champion"],SelectedIndex=1,OnChange=Log },
                new() { Id="wager",Kind=BookControl.Slider,Label="Wager amount",Description="Set how much gold you wish to wager.",Minimum=0,Maximum=500,Step=10,Value=200,OnChange=Log },
                new() { Id="announcements",Kind=BookControl.Toggle,Label="Match announcements",Description="Receive a notice before each match begins.",Checked=true,OnChange=Log }
            ] },
            new() { Title="Champion preferences",Subtitle="Two columns, editable fields and individual options",Layout=BookLayout.TwoColumns,Elements=[
                new() {Id="name",Kind=BookControl.TextInput,Label="Champion name",Description="Choose a name for your ledger.",Text="Champion",OnChange=Log},
                new() {Id="team",Kind=BookControl.Dropdown,Label="Arena team",Description="Select your colours.",Options=["Blue team","Yellow team"],OnChange=Log},
                new() {Id="volume",Kind=BookControl.Slider,Label="Announcement volume",Description="Adjust the match notices.",Value=.7,OnChange=Log},
                new() {Id="tips",Kind=BookControl.Toggle,Label="Training advice",Description="Show helpful reminders.",Checked=true,OnChange=Log},
                new() {Id="save",Kind=BookControl.Button,Label="Keep your preferences",Description="Demo controls only; no gold is spent.",ButtonLabel="Save preferences",OnChange=e=>{e.Book.Update("save",x=>x with {Description="Your demo preferences have been saved."});Log(e);}}
            ] },
            new() {Title="A ledger for any mod",Subtitle="Titles, text and freely positioned controls",Layout=BookLayout.Custom,Elements=[
                new() {Id="about",Kind=BookControl.Heading,Label="Your story starts here",Description="Use these pages for shops, quests, settings or character sheets.",Height=100},
                new() {Id="custom-left",Kind=BookControl.Button,Label="Return to the arena",ButtonLabel="First page",X=0,Y=180,Width=290,OnChange=e=>e.Book.GoToPage(0)},
                new() {Id="custom-right",Kind=BookControl.Button,Label="Finish reading",ButtonLabel="Close ledger",X=320,Y=300,Width=290,OnChange=e=>e.Book.Dispose()},
                new() {Id="artwork",Kind=BookControl.Image,ImagePath="/Game/OblivionUI/Book/T_Book.T_Book",ImageAspectRatio=760d/940,X=0,Y=470,Width=150,Height=190},
                new() {Id="drawing",Kind=BookControl.Canvas,X=180,Y=470,Width=420,Height=200,Shapes=[
                    new(){Kind=CanvasShapeKind.Rectangle,Points=[new(0,0),new(420,200)],Color=new(.3f,.18f,.07f,.12f)},
                    new(){Kind=CanvasShapeKind.Polyline,Points=[new(20,150),new(80,70),new(140,120),new(230,30),new(370,65)],Thickness=4},
                    new(){Kind=CanvasShapeKind.Line,Points=[new(20,175),new(395,175)],Thickness=2}
                ]},
                new() {Id="scroll-note",Kind=BookControl.Text,Label="More room for your story",Description="Long pages scroll within the book. Drag the brass marker or use the mouse wheel.",Y=850,Height=140}
            ] }
        ]
    });
    private static void Log(BookEvent e)=>Console.WriteLine($"[OblivionUI] Book event {e.Id}: value={e.Value} checked={e.Checked} text={e.Text} index={e.SelectedIndex}");
}
