namespace OblivionUI;

public sealed record CraftingIngredient(string Name,int Required,int? Available=null);
public sealed record CraftingRecipe
{
    public required string Id {get;init;}
    public required string Name {get;init;}
    public string Description {get;init;}="";
    public string ImagePath {get;init;}="";
    public string RowStatus {get;init;}="";
    public CraftingIngredient[] Ingredients {get;init;}=[];
    public bool CanCraft {get;init;}
    public string UnavailableReason {get;init;}="Recipe unavailable.";
    public string ActionLabel {get;init;}="Craft";
}
public sealed record CraftingCategory
{
    public required string Name {get;init;}
    public string Icon {get;init;}="items";
    public CraftingRecipe[] Recipes {get;init;}=[];
}
public sealed record CraftingDefinition
{
    public string Title {get;init;}="Crafting";
    public string Subtitle {get;init;}="Select a recipe";
    public BookPlacement Placement {get;init;}=BookPlacement.Left;
    public CraftingCategory[] Categories {get;init;}=[];
    /// <summary>Request only. The receiving mod/server must recheck materials and grant items atomically.</summary>
    public Action<string>? OnCraft {get;init;}
}
public static class Crafting
{
    public static BookHandle Open(CraftingDefinition definition)=>Books.Open(Build(definition));
    internal static BookDefinition Build(CraftingDefinition d)
    {
        ArgumentNullException.ThrowIfNull(d);
        if(d.Categories is null||d.Categories.Length is <1 or >8||d.Categories.Any(c=>c is null||string.IsNullOrWhiteSpace(c.Name)||c.Recipes is null))throw new ArgumentException("Supply 1–8 crafting categories.");
        var recipes=d.Categories.SelectMany(c=>c.Recipes).ToArray();
        if(recipes.Any(r=>r is null||string.IsNullOrWhiteSpace(r.Id)||string.IsNullOrWhiteSpace(r.Name)||r.Ingredients is null||r.Ingredients.Any(i=>i is null||string.IsNullOrWhiteSpace(i.Name)||i.Required<=0||i.Available<0))||recipes.Select(r=>r.Id).Distinct().Count()!=recipes.Length||recipes.Length+d.Categories.Length>32)throw new ArgumentException("Invalid or duplicate crafting recipes (32 total pages maximum).");
        var pages=new List<BookPage>();int sequence=0;
        foreach(var category in d.Categories){
            int categoryPage=pages.Count;var rows=new List<BookElement>();
            pages.Add(new(){Title=category.Name,Subtitle=d.Subtitle,TabIcon=category.Icon,Gap=6});
            foreach(var recipe in category.Recipes){
                int detailPage=pages.Count;string prefix="recipe-"+(sequence++);
                string icon=recipe.ImagePath.Length>0?recipe.ImagePath:CategoryImage(category.Icon);
                string preview=recipe.Name+"\n\n"+recipe.Description+"\n\nMaterials\n"+(recipe.Ingredients.Length==0?"No materials required":string.Join("\n",recipe.Ingredients.Select(i=>i.Available is {} n?$"{i.Name}: {n} / {i.Required}":$"{i.Name} × {i.Required}")))+"\n\n"+(recipe.CanCraft&&d.OnCraft!=null&&recipe.Ingredients.All(i=>i.Available is null||i.Available>=i.Required)?"Select for "+recipe.ActionLabel.ToLowerInvariant():recipe.UnavailableReason);
                rows.Add(new(){Id=prefix,Kind=BookControl.RecipeRow,Label=recipe.Name,Description=recipe.RowStatus,RecipePreview=preview,ImagePath=icon,Height=52,OnChange=e=>e.Book.GoToPage(detailPage)});
                var elements=new List<BookElement>{new(){Id=prefix+"-back",Kind=BookControl.Button,ButtonLabel="Back to "+category.Name,Height=42,OnChange=e=>e.Book.GoToPage(categoryPage)},new(){Id=prefix+"-description",Kind=BookControl.Text,Description=recipe.Description,Height=90}};
                elements.Add(new(){Id=prefix+"-heading",Kind=BookControl.Heading,Label="Materials",Height=40});
                if(recipe.Ingredients.Length==0)elements.Add(new(){Id=prefix+"-none",Kind=BookControl.Text,Label="No materials required",Height=40});
                int ingredient=0;
                foreach(var item in recipe.Ingredients)elements.Add(new(){Id=prefix+"-material-"+(ingredient++),Kind=BookControl.RecipeRow,Label=item.Name,Description=item.Available is {} available?$"{available} / {item.Required}":$"× {item.Required}",Enabled=false,Height=48});
                bool enabled=recipe.CanCraft&&d.OnCraft!=null&&recipe.Ingredients.All(i=>i.Available is null||i.Available>=i.Required);
                if(!enabled)elements.Add(new(){Id=prefix+"-reason",Kind=BookControl.Text,Description=recipe.UnavailableReason,Height=78});
                elements.Add(new(){Id=prefix+"-craft",Kind=BookControl.Button,ButtonLabel=recipe.ActionLabel,Height=48,Enabled=enabled,OnChange=e=>{if(!enabled)return;e.Book.Dispose();d.OnCraft?.Invoke(recipe.Id);}});
                pages.Add(new(){Title=recipe.Name,Subtitle=category.Name,TabIcon=category.Icon,ShowInTabs=false,Gap=8,Elements=elements.ToArray()});
            }
            if(rows.Count==0)rows.Add(new(){Id="empty-"+categoryPage,Kind=BookControl.Text,Description="No recipes available.",Height=60});
            pages[categoryPage]=pages[categoryPage] with{Elements=rows.ToArray()};
        }
        return new(){Title=d.Title,Placement=d.Placement,ShowTabs=true,ShowPageNavigation=false,Pages=pages.ToArray()};
    }
    private static string CategoryImage(string icon)
    {
        string suffix=icon.ToLowerInvariant() switch {"weapons"=>"Weapons","armour" or "armor"=>"Armor","settings"=>"skills",_=>"All_items"};
        return $"/Game/Art/UI/Icons/Icon_Category/T_Category_{suffix}.T_Category_{suffix}";
    }
}
