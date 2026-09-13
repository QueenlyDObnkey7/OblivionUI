# Crafting menus (OblivionUI 0.9.1)

`Crafting.Open(CraftingDefinition)` opens an inventory-style recipe browser with parchment, category icon tabs, compact recipe rows and ingredient/detail pages.

```csharp
Crafting.Open(new() {
    Title = "Forge",
    Categories = [new() {
        Name = "Weapons", Icon = "weapons",
        Recipes = [new() {
            Id = "iron-dagger", Name = "Iron Dagger",
            Description = "A simple iron blade.",
            ImagePath = "/Game/MyMod/UI/T_Dagger.T_Dagger",
            Ingredients = [new("Iron ingot", 1, 3)],
            RowStatus = "Available", CanCraft = true
        }]
    }],
    OnCraft = recipeId => SendCraftRequestToServer(recipeId)
});
```

Recipe IDs must be unique. Up to eight categories and 32 total category/detail pages are supported. `ImagePath` is optional; absent recipe art uses the category icon. Categories accept weapons, armour, settings or items. `Placement` defaults to Left.

Ingredients use `(name, required, available?)`. Omit available when no inventory count is known. A known insufficient count disables the action. `CanCraft` defaults to false. Set `UnavailableReason` to explain restrictions; set `ActionLabel` to customize Craft. A missing OnCraft handler also disables the action. Closing a detail page with its Back action returns to the category.

This API requests an action; it does not consume inventory or grant items. The receiving mod/server must validate the recipe, current materials, station distance and permissions, then consume and grant atomically. Do not trust displayed inventory counts or client availability as authorization.

The Waterfront Blacksmith integration opens this menu from Use forge. Its example weapon, armour and component recipes are previews. The menu appears as soon as the server-approved smithing animation and camera start. Browsing keeps smithing active; closing the menu restores control and releases the station. No materials are consumed and no inventory item is granted.

For custom layouts, `BookControl.RecipeRow` provides an icon, label, right-hand Description/status, and an OnChange callback. `BookDefinition.ShowPageNavigation=false` hides Previous/Next/page-count controls for tab-based browsers.

Recipe rows also display a parchment preview beside the book on mouse hover or keyboard focus. The preview includes description, material counts, availability and an action hint. Clicking still opens the explicit action page. BookElement.RecipePreview can provide the same preview for custom recipe rows (maximum 4,000 characters).
