using UnrealBuildTool;
public class OblivionUIEditor : ModuleRules
{
    public OblivionUIEditor(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine", "UnrealEd", "UMG", "UMGEditor", "Slate", "SlateCore", "SlateRHIRenderer", "RenderCore", "RHI", "Kismet", "BlueprintGraph", "AssetTools", "ImageCore", "Json", "JsonUtilities", "Niagara" });
    }
}
