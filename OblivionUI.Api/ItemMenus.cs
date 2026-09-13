namespace OblivionUI;
public sealed record MenuItem
{
    public required string Id {get;init;}public required string Name {get;init;}
    public string Category {get;init;}="Items";public string ImagePath {get;init;}="";
    public string Description {get;init;}="";public double Price {get;init;}public int Quantity {get;init;}=1;
    public bool Enabled {get;init;}=true;
}
public sealed record ItemMenuDefinition
{
    public string Title {get;init;}="Shop";public BookPlacement Placement {get;init;}
    public string CurrencyLabel {get;init;}="gold";public string Subtitle {get;init;}="";public Action? OnClose {get;init;}
    public MenuItem[] Items {get;init;}=[];public Action<MenuItem,int>? OnBuy {get;init;}
}
public static class ItemMenus
{
    public static BookHandle Open(ItemMenuDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);if(string.IsNullOrWhiteSpace(definition.CurrencyLabel)||definition.CurrencyLabel.Length>24||definition.Subtitle is null||definition.Subtitle.Length>200)throw new ArgumentException("Invalid shop labels.");if(definition.Items is null||definition.Items.Length>512||definition.Items.Any(i=>i is null||string.IsNullOrWhiteSpace(i.Id)||i.Name is null||i.Category is null||i.Description is null||i.ImagePath is null||!double.IsFinite(i.Price)||i.Price<0||i.Quantity<0)||definition.Items.Select(i=>i.Id).Distinct().Count()!=definition.Items.Length)throw new ArgumentException("Invalid catalogue (up to 512 unique items).");
        var items=definition.Items.ToArray();string search="";int sort=0,quantity=1,backPage=0;var batches=new Dictionary<string,int>();MenuItem? selected=items.FirstOrDefault();BookHandle? handle=null;
        var categories=items.Select(i=>i.Category).Distinct().Order().Take(30).ToArray();if(categories.Length==0)categories=["All"];else if(categories.Length>1)categories=new[]{"All"}.Concat(categories.Where(c=>c!="All")).ToArray();
        BookPage[] Pages()=>categories.Select(Catalogue).Append(Details()).ToArray();
        void Refresh(){handle?.ReplacePages(Pages());}
        BookPage Catalogue(string category){
            int batch=batches.GetValueOrDefault(category);
            IEnumerable<MenuItem> results=items.Where(i=>(category=="All"||i.Category==category)&&i.Name.Contains(search,StringComparison.OrdinalIgnoreCase));
            results=sort switch{1=>results.OrderBy(i=>i.Price),2=>results.OrderByDescending(i=>i.Price),_=>results.OrderBy(i=>i.Name)};
            var matches=results.ToArray();batch=Math.Clamp(batch,0,Math.Max(0,(matches.Length-1)/30));
            var rows=new List<BookElement>{
                new(){Id="shop-sort",Kind=BookControl.Dropdown,Label="Sort",Options=["Name","Price: low first","Price: high first"],SelectedIndex=sort,X=0,Width=290,OnChange=e=>{sort=e.SelectedIndex;Refresh();}},
                new(){Id="shop-query",Kind=BookControl.TextInput,Label="Search items",Text=search,X=320,Y=0,Width=290,OnChange=e=>search=e.Text},
                new(){Id="shop-search",Kind=BookControl.Button,ButtonLabel="Search",X=440,Y=124,Width=170,OnChange=e=>{batches.Clear();Refresh();}}
            };
            int columns=definition.Placement is BookPlacement.Left or BookPlacement.Right?2:3;double width=(610d-(columns-1)*12)/columns;int n=0;
            foreach(var item in matches.Skip(batch*30).Take(30)){var captured=item;rows.Add(new(){Id="item-"+item.Id,Kind=BookControl.ItemCard,Label=item.Name,Description=$"{item.Price:0.##} {definition.CurrencyLabel} | Qty {item.Quantity}",ImagePath=item.ImagePath,Tooltip=item.Description,Enabled=item.Enabled,Checked=selected?.Id==item.Id,X=(n%columns)*(width+12),Y=180+(n/columns)*206,Width=width,Height=194,OnChange=e=>{selected=captured;quantity=1;backPage=Array.IndexOf(categories,category);Refresh();handle!.GoToPage(categories.Length);}});n++;}
            double bottom=180+Math.Ceiling(n/(double)columns)*206;
            if(n==0)rows.Add(new(){Id="shop-empty",Kind=BookControl.Text,Label="No matching items",Y=180,Height=64});
            rows.Add(new(){Id="shop-back",Kind=BookControl.Button,ButtonLabel="Previous items",Enabled=batch>0,Y=bottom+64,Width=290,OnChange=e=>{batches[category]=batch-1;Refresh();}});
            rows.Add(new(){Id="shop-more",Kind=BookControl.Button,ButtonLabel="Next items",Enabled=(batch+1)*30<matches.Length,X=320,Y=bottom+64,Width=290,OnChange=e=>{batches[category]=batch+1;Refresh();}});
            return new(){Title=categories.Length>1?category:definition.Title,TabIcon=category,Subtitle=$"{definition.Subtitle} | {matches.Length} items",Layout=BookLayout.Custom,Elements=rows.Select(e=>Array.IndexOf(categories,category)==0?e:e with{Id="cat"+Array.IndexOf(categories,category)+"-"+e.Id}).ToArray()};
        }
        BookPage Details(){var rows=new List<BookElement>();if(selected is {} item){if(item.ImagePath.Length>0)rows.Add(new(){Id="detail-image",Kind=BookControl.Image,ImagePath=item.ImagePath,Height=160});rows.Add(new(){Id="detail-text",Kind=BookControl.Text,Label=item.Name,Description=item.Description+ $"\nPrice: {item.Price:0.##} {definition.CurrencyLabel} | Available: {item.Quantity}",Height=220});rows.Add(new(){Id="detail-quantity",Kind=BookControl.Slider,Label="Quantity",Minimum=1,Maximum=Math.Max(2,Math.Min(99,item.Quantity)),Step=1,Value=1,Enabled=item.Quantity>1,OnChange=e=>quantity=Math.Min((int)e.Value,item.Quantity)});rows.Add(new(){Id="detail-buy",Kind=BookControl.Button,ButtonLabel="Buy",Enabled=item.Enabled&&item.Quantity>0&&definition.OnBuy is not null,OnChange=e=>definition.OnBuy?.Invoke(item,quantity)});}rows.Add(new(){Id="detail-back",Kind=BookControl.Button,ButtonLabel="Back to catalogue",OnChange=e=>e.Book.GoToPage(backPage)});return new(){Title="Item details",ShowInTabs=false,Elements=rows.ToArray()};}
        handle=Books.Open(new(){Title=definition.Title,Subtitle=definition.Subtitle,OnClose=definition.OnClose,Placement=definition.Placement,ShowTabs=true,Pages=Pages()});return handle;
    }
}
