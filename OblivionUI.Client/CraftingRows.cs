namespace OblivionUI.Client;
internal static partial class BookRuntime
{
    private static void ConfigureRecipeRow(UnrealBookBridge u,nint row,BookElement e,double width)
    {
        void Offset(string name,float left,float top,float right,float bottom)=>u.Call(u.Object(u.Object(row,name),"Slot"),"SetOffsets",("InOffset",(left,top,right,bottom)));
        u.Call(u.Object(row,"ActionButton"),"SetBackgroundColor",("InBackgroundColor",(1f,1f,1f,0f)));
        Offset("ActionButton",0,0,0,(float)e.Height);u.SetText(row,"SetButtonLabel","");
        u.Call(u.Object(row,"Label"),"SetVisibility",("InVisibility",(byte)3));
        u.Call(u.Object(row,"Description"),"SetVisibility",("InVisibility",(byte)3));
        u.Font(row,"SetLabelSize",20);u.Font(row,"SetDescriptionSize",16);
        Offset("Label",e.ImagePath.Length>0?50:12,10,90,32);
        Offset("Description",(float)width-90,13,8,28);
        u.Call(u.Object(row,"Description"),"SetJustification",("InJustification",(byte)2));
        // A tinted parchment image provides the soft inventory-row band.
        // It remains behind the click target, icon and text.
        var paper=u.DefaultObject("/Game/OblivionUI/Book/T_ImperialHudPaper.T_ImperialHudPaper");
        if(paper!=0){
            var ink=u.Object(row,"Ink");u.Call(ink,"SetBrushFromTexture",("Texture",paper),("bMatchSize",false));
            u.Call(ink,"SetColorAndOpacity",("InColorAndOpacity",(.32f,.25f,.17f,.22f)));
            u.Call(ink,"SetVisibility",("InVisibility",(byte)3));Offset("Ink",2,3,2,3);
            u.Call(u.Object(ink,"Slot"),"SetZOrder",("InZOrder",-1));
        }
        if(e.ImagePath.Length>0){
            u.Call(u.Object(row,"ArtworkScale"),"SetVisibility",("InVisibility",(byte)3));
            Offset("ArtworkScale",4,4,(float)width-44,4);u.SetText(row,"SetArtwork",e.ImagePath);
        }
    }
}
