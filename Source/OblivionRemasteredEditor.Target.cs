using UnrealBuildTool;
public class OblivionRemasteredEditorTarget : TargetRules
{
    public OblivionRemasteredEditorTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Editor;
        DefaultBuildSettings = BuildSettingsVersion.V4;
        IncludeOrderVersion = EngineIncludeOrderVersion.Unreal5_3;
        ExtraModuleNames.Add("OblivionUIEditor");
    }
}
