#include "BuildInteractionUICommandlet.h"
#include "Modules/ModuleManager.h"
#include "WidgetBlueprint.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintGeneratedClass.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/Border.h"
#include "Components/SizeBox.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Kismet/KismetStringLibrary.h"
#include "Kismet/KismetTextLibrary.h"
#include "Kismet/KismetMathLibrary.h"
#include "Framework/Application/SlateApplication.h"
#include "Interfaces/ISlateRHIRendererModule.h"
#include "K2Node_IfThenElse.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "K2Node_FunctionEntry.h"
#include "K2Node_CallFunction.h"
#include "K2Node_VariableGet.h"
#include "EdGraphSchema_K2.h"
#include "UObject/SavePackage.h"
#include "Misc/PackageName.h"
#include "Slate/WidgetRenderer.h"
#include "Engine/TextureRenderTarget2D.h"
#include "ImageUtils.h"
#include "Misc/FileHelper.h"
#include "Serialization/BufferArchive.h"
#include "Engine/Font.h"
#include "Engine/FontFace.h"
#include "Engine/Texture2D.h"
#include "Components/VerticalBoxSlot.h"
#include "TextureCompiler.h"

IMPLEMENT_MODULE(FDefaultModuleImpl, OblivionUIEditor)

UBuildInteractionUICommandlet::UBuildInteractionUICommandlet()
{
    IsClient = false; IsServer = false; IsEditor = true; LogToConsole = true;
}

static UEdGraphPin* Pin(UEdGraphNode* Node, const FName Name)
{
    UEdGraphPin* Result = Node->FindPin(Name);
    if (!Result) for (auto P : Node->Pins) UE_LOG(LogTemp,Error,TEXT("Node %s (%s) available pin %s"),*Node->GetName(),*Node->GetNodeTitle(ENodeTitleType::FullTitle).ToString(),*P->PinName.ToString());
    checkf(Result, TEXT("Missing pin %s"), *Name.ToString());
    return Result;
}

static void SaveUIAsset(UObject* Asset)
{
    FSavePackageArgs Args; Args.TopLevelFlags = RF_Public | RF_Standalone;
    check(UPackage::SavePackage(Asset->GetOutermost(), Asset,
        *FPackageName::LongPackageNameToFilename(Asset->GetOutermost()->GetName(),TEXT(".uasset")), Args));
}

static UFont* MakeFont()
{
    const TCHAR* Path = TEXT("/Game/OblivionUI/Fonts/Petrock");
    if (auto Existing = LoadObject<UFont>(nullptr,TEXT("/Game/OblivionUI/Fonts/Petrock.Petrock"))) return Existing;
    auto Face = NewObject<UFontFace>(CreatePackage(TEXT("/Game/OblivionUI/Fonts/PetrockFace")),TEXT("PetrockFace"),RF_Public|RF_Standalone);
    TArray<uint8> Bytes;
    FString Filename = FPaths::ProjectDir()/TEXT("ThirdParty/Kingthings/Kingthings Petrock.ttf");
    check(FFileHelper::LoadFileToArray(Bytes,*Filename));
    Face->InitializeFromBulkData(Filename,EFontHinting::Default,Bytes.GetData(),Bytes.Num());
    Face->LoadingPolicy = EFontLoadingPolicy::Inline; SaveUIAsset(Face);
    auto Font = NewObject<UFont>(CreatePackage(Path),TEXT("Petrock"),RF_Public|RF_Standalone);
    Font->FontCacheType = EFontCacheType::Runtime;
    FTypefaceEntry Entry(TEXT("Regular")); Entry.Font = FFontData(Face);
    Font->CompositeFont.DefaultTypeface.Fonts.Add(Entry); SaveUIAsset(Font); return Font;
}

// Dedicated torn parchment preserves all four edges at notification scale.
static UTexture2D* MakePaper()
{
    if(auto Existing=LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/T_NotificationParchment.T_NotificationParchment"))) return Existing;
    auto Imported=FImageUtils::ImportFileAsTexture2D(FPaths::ProjectDir()/TEXT("Art/NotificationParchment.png")); check(Imported);
    auto Texture=DuplicateObject<UTexture2D>(Imported,CreatePackage(TEXT("/Game/OblivionUI/T_NotificationParchment")),TEXT("T_NotificationParchment"));
    auto& Mip=Imported->GetPlatformData()->Mips[0]; auto Pixels=Mip.BulkData.LockReadOnly();
    Texture->Source.Init(Imported->GetSizeX(),Imported->GetSizeY(),1,1,TSF_BGRA8,static_cast<const uint8*>(Pixels)); Mip.BulkData.Unlock();
    Texture->ClearFlags(RF_Transient); Texture->SetFlags(RF_Public|RF_Standalone);
    Texture->CompressionSettings=TC_EditorIcon; Texture->LODGroup=TEXTUREGROUP_UI;
    Texture->MipGenSettings=TMGS_NoMipmaps; Texture->SRGB=true; Texture->UpdateResource(); SaveUIAsset(Texture);
    return Texture;
}

static void MakeSetPrompt(UWidgetBlueprint* BP)
{
    UEdGraph* Graph = FBlueprintEditorUtils::CreateNewGraph(BP, TEXT("SetPrompt"), UEdGraph::StaticClass(), UEdGraphSchema_K2::StaticClass());
    FBlueprintEditorUtils::AddFunctionGraph(BP, Graph, true, static_cast<UFunction*>(nullptr));
    UK2Node_FunctionEntry* Entry = nullptr;
    for (UEdGraphNode* N : Graph->Nodes) if (auto E = Cast<UK2Node_FunctionEntry>(N)) Entry = E;
    check(Entry);
    FEdGraphPinType TextType; TextType.PinCategory = UEdGraphSchema_K2::PC_Text;
    Entry->CreateUserDefinedPin(TEXT("KeyText"), TextType, EGPD_Output);
    Entry->CreateUserDefinedPin(TEXT("ActionText"), TextType, EGPD_Output);
    const UEdGraphSchema_K2* Schema = GetDefault<UEdGraphSchema_K2>();
    UEdGraphPin* Exec = Pin(Entry, UEdGraphSchema_K2::PN_Then);
    int X = 300;
    for (const auto& Pair : { TPair<FName,FName>(TEXT("KeyLabel"),TEXT("KeyText")), TPair<FName,FName>(TEXT("ActionLabel"),TEXT("ActionText")) })
    {
        auto Get = NewObject<UK2Node_VariableGet>(Graph);
        Get->VariableReference.SetSelfMember(Pair.Key);
        Graph->AddNode(Get); Get->CreateNewGuid(); Get->AllocateDefaultPins(); Get->NodePosX = X; Get->NodePosY = 180;
        auto Call = NewObject<UK2Node_CallFunction>(Graph);
        Call->SetFromFunction(UTextBlock::StaticClass()->FindFunctionByName(TEXT("SetText")));
        Graph->AddNode(Call); Call->CreateNewGuid(); Call->AllocateDefaultPins(); Call->NodePosX = X;
        check(Schema->TryCreateConnection(Exec, Pin(Call, UEdGraphSchema_K2::PN_Execute)));
        check(Schema->TryCreateConnection(Pin(Get, Pair.Key), Pin(Call, UEdGraphSchema_K2::PN_Self)));
        check(Schema->TryCreateConnection(Pin(Entry, Pair.Value), Pin(Call, TEXT("InText"))));
        Exec = Pin(Call, UEdGraphSchema_K2::PN_Then); X += 350;
    }
}

// Match the four functions used by OBMP's native GameMessage binding. The
// original cooked Blueprint uses FString Text and bool Visible parameters.
static void MakeAdapterFunction(UWidgetBlueprint* BP, FName Name, FName Target, bool Visibility = false)
{
    auto Graph = FBlueprintEditorUtils::CreateNewGraph(BP, Name, UEdGraph::StaticClass(), UEdGraphSchema_K2::StaticClass());
    FBlueprintEditorUtils::AddFunctionGraph(BP, Graph, true, static_cast<UFunction*>(nullptr));
    UK2Node_FunctionEntry* Entry = nullptr;
    for (auto N : Graph->Nodes) if (auto E = Cast<UK2Node_FunctionEntry>(N)) Entry = E;
    check(Entry);
    const auto Schema = GetDefault<UEdGraphSchema_K2>();
    auto Link = [Schema](UEdGraphPin* A, UEdGraphPin* B) { check(Schema->TryCreateConnection(A,B)); };
    auto Call = [Graph](UClass* Class, FName Function) {
        auto N = NewObject<UK2Node_CallFunction>(Graph); N->SetFromFunction(Class->FindFunctionByName(Function));
        Graph->AddNode(N); N->CreateNewGuid(); N->AllocateDefaultPins(); return N;
    };
    auto Get = [Graph](FName Variable) {
        auto N = NewObject<UK2Node_VariableGet>(Graph); N->VariableReference.SetSelfMember(Variable);
        Graph->AddNode(N); N->CreateNewGuid(); N->AllocateDefaultPins(); return N;
    };
    auto SetVisibility = [&](UEdGraphPin* Exec, FName Variable, const TCHAR* Value) {
        auto N = Call(UWidget::StaticClass(), TEXT("SetVisibility"));
        Link(Exec, Pin(N, UEdGraphSchema_K2::PN_Execute));
        if (!Variable.IsNone()) Link(Pin(Get(Variable), Variable), Pin(N, UEdGraphSchema_K2::PN_Self));
        Schema->TrySetDefaultValue(*Pin(N,TEXT("InVisibility")), Value);
        return Pin(N, UEdGraphSchema_K2::PN_Then);
    };
    FEdGraphPinType Type; Type.PinCategory = Visibility ? UEdGraphSchema_K2::PC_Boolean : UEdGraphSchema_K2::PC_String;
    Entry->CreateUserDefinedPin(Visibility ? TEXT("Visible") : TEXT("Text"), Type, EGPD_Output);
    auto Exec = Pin(Entry, UEdGraphSchema_K2::PN_Then);
    if (Visibility)
    {
        auto Branch = NewObject<UK2Node_IfThenElse>(Graph); Graph->AddNode(Branch); Branch->CreateNewGuid(); Branch->AllocateDefaultPins();
        Link(Exec, Branch->GetExecPin()); Link(Pin(Entry,TEXT("Visible")), Branch->GetConditionPin());
        SetVisibility(Branch->GetThenPin(), NAME_None, TEXT("HitTestInvisible"));
        SetVisibility(Branch->GetElsePin(), NAME_None, TEXT("Collapsed"));
        return;
    }
    auto SetText = [&](UEdGraphPin* Execution, UEdGraphPin* String) {
        auto Conv = Call(UKismetTextLibrary::StaticClass(), TEXT("Conv_StringToText"));
        Link(String, Pin(Conv,TEXT("InString")));
        auto N = Call(UTextBlock::StaticClass(), TEXT("SetText"));
        Link(Execution, Pin(N,UEdGraphSchema_K2::PN_Execute));
        Link(Pin(Get(Target),Target), Pin(N,UEdGraphSchema_K2::PN_Self));
        Link(Pin(Conv,UEdGraphSchema_K2::PN_ReturnValue), Pin(N,TEXT("InText")));
        return Pin(N,UEdGraphSchema_K2::PN_Then);
    };
    if (Name == TEXT("SetSecondText") || Name == TEXT("SetThirdText"))
    {
        const FString Side = Name == TEXT("SetSecondText") ? TEXT("Left") : TEXT("Right");
        const FName Panel(*(Side+TEXT("Notice")));
        Exec = SetVisibility(Exec,Panel,TEXT("Collapsed"));
        auto Starts = Call(UKismetStringLibrary::StaticClass(),TEXT("StartsWith"));
        Link(Pin(Entry,TEXT("Text")),Pin(Starts,TEXT("SourceString")));
        Schema->TrySetDefaultValue(*Pin(Starts,TEXT("InPrefix")),TEXT("@oui|"));
        auto Branch = NewObject<UK2Node_IfThenElse>(Graph); Graph->AddNode(Branch); Branch->CreateNewGuid(); Branch->AllocateDefaultPins();
        Link(Exec,Branch->GetExecPin()); Link(Pin(Starts,UEdGraphSchema_K2::PN_ReturnValue),Branch->GetConditionPin());
        auto Split = [&](UEdGraphPin* Source) {
            auto N = Call(UKismetStringLibrary::StaticClass(),TEXT("Split"));
            Link(Source,Pin(N,TEXT("SourceString"))); Schema->TrySetDefaultValue(*Pin(N,TEXT("InStr")),TEXT("|")); return N;
        };
        auto Prefix = Split(Pin(Entry,TEXT("Text")));
        auto Offset = Split(Pin(Prefix,TEXT("RightS")));
        auto Content = Split(Pin(Offset,TEXT("RightS")));
        auto ToFloat = Call(UKismetStringLibrary::StaticClass(),TEXT("Conv_StringToDouble"));
        Link(Pin(Offset,TEXT("LeftS")),Pin(ToFloat,TEXT("InString")));
        auto Vector = Call(UKismetMathLibrary::StaticClass(),TEXT("MakeVector2D"));
        Link(Pin(ToFloat,UEdGraphSchema_K2::PN_ReturnValue),Pin(Vector,TEXT("X")));
        auto Translate = Call(UWidget::StaticClass(),TEXT("SetRenderTranslation"));
        Link(SetVisibility(SetVisibility(Branch->GetThenPin(),Target,TEXT("Collapsed")),Panel,TEXT("HitTestInvisible")),Pin(Translate,UEdGraphSchema_K2::PN_Execute));
        Link(Pin(Get(Panel),Panel),Pin(Translate,UEdGraphSchema_K2::PN_Self));
        Link(Pin(Vector,UEdGraphSchema_K2::PN_ReturnValue),Pin(Translate,TEXT("Translation")));
        FName Original = Target; Target = FName(*(Side+TEXT("Title")));
        auto TitleExec = SetText(Pin(Translate,UEdGraphSchema_K2::PN_Then),Pin(Content,TEXT("LeftS")));
        Target = FName(*(Side+TEXT("Body"))); SetText(TitleExec,Pin(Content,TEXT("RightS")));
        Target = Original; Exec = Branch->GetElsePin();
    }
    if (Name == TEXT("SetMainText"))
    {
        auto Starts = Call(UKismetStringLibrary::StaticClass(), TEXT("StartsWith"));
        Link(Pin(Entry,TEXT("Text")), Pin(Starts,TEXT("SourceString")));
        Schema->TrySetDefaultValue(*Pin(Starts,TEXT("InPrefix")), TEXT("["));
        auto Split = Call(UKismetStringLibrary::StaticClass(), TEXT("Split"));
        Link(Pin(Entry,TEXT("Text")), Pin(Split,TEXT("SourceString")));
        Schema->TrySetDefaultValue(*Pin(Split,TEXT("InStr")), TEXT("] "));
        auto Both = Call(UKismetMathLibrary::StaticClass(), TEXT("BooleanAND"));
        Link(Pin(Starts,UEdGraphSchema_K2::PN_ReturnValue), Pin(Both,TEXT("A")));
        Link(Pin(Split,UEdGraphSchema_K2::PN_ReturnValue), Pin(Both,TEXT("B")));
        auto Branch = NewObject<UK2Node_IfThenElse>(Graph); Graph->AddNode(Branch); Branch->CreateNewGuid(); Branch->AllocateDefaultPins();
        Link(Exec,Branch->GetExecPin()); Link(Pin(Both,UEdGraphSchema_K2::PN_ReturnValue),Branch->GetConditionPin());
        auto Chop = Call(UKismetStringLibrary::StaticClass(), TEXT("RightChop"));
        Link(Pin(Split,TEXT("LeftS")),Pin(Chop,TEXT("SourceString")));
        Schema->TrySetDefaultValue(*Pin(Chop,TEXT("Count")),TEXT("1"));
        auto KeyConv = Call(UKismetTextLibrary::StaticClass(),TEXT("Conv_StringToText"));
        Link(Pin(Chop,UEdGraphSchema_K2::PN_ReturnValue),Pin(KeyConv,TEXT("InString")));
        auto KeySet = Call(UTextBlock::StaticClass(),TEXT("SetText"));
        Link(SetVisibility(Branch->GetThenPin(),TEXT("KeySize"),TEXT("HitTestInvisible")),Pin(KeySet,UEdGraphSchema_K2::PN_Execute));
        Link(Pin(Get(TEXT("KeyLabel")),TEXT("KeyLabel")),Pin(KeySet,UEdGraphSchema_K2::PN_Self));
        Link(Pin(KeyConv,UEdGraphSchema_K2::PN_ReturnValue),Pin(KeySet,TEXT("InText")));
        SetText(Pin(KeySet,UEdGraphSchema_K2::PN_Then),Pin(Split,TEXT("RightS")));
        SetText(SetVisibility(Branch->GetElsePin(),TEXT("KeySize"),TEXT("Collapsed")),Pin(Entry,TEXT("Text")));
    }
    else
    {
        auto Empty = Call(UKismetStringLibrary::StaticClass(),TEXT("IsEmpty"));
        Link(Pin(Entry,TEXT("Text")),Pin(Empty,TEXT("InString")));
        auto Branch = NewObject<UK2Node_IfThenElse>(Graph); Graph->AddNode(Branch); Branch->CreateNewGuid(); Branch->AllocateDefaultPins();
        Link(Exec,Branch->GetExecPin()); Link(Pin(Empty,UEdGraphSchema_K2::PN_ReturnValue),Branch->GetConditionPin());
        SetText(SetVisibility(Branch->GetThenPin(),Target,TEXT("Collapsed")),Pin(Entry,TEXT("Text")));
        SetText(SetVisibility(Branch->GetElsePin(),Target,TEXT("HitTestInvisible")),Pin(Entry,TEXT("Text")));
    }
}

int32 UBuildInteractionUICommandlet::Main(const FString& Params)
{
    if(Params.Contains(TEXT("BookBuild"))) { extern int32 BuildBookUI(); return BuildBookUI(); }
    if(Params.Contains(TEXT("BookMetadata"))) { extern int32 BookMetadata(); return BookMetadata(); }
    if(Params.Contains(TEXT("BookPreview"))) { extern int32 PreviewBookUI(); return PreviewBookUI(); }
    if (Params.Contains(TEXT("Validate")))
    {
        if (!FSlateApplication::IsInitialized()) FSlateApplication::InitializeAsStandaloneApplication(FModuleManager::LoadModuleChecked<ISlateRHIRendererModule>(TEXT("SlateRHIRenderer")).CreateSlateRHIRenderer());
        auto Class = LoadClass<UUserWidget>(nullptr,TEXT("/Game/Mods/OblivionMp/WBP_GameMessage.WBP_GameMessage_C"));
        check(Class);
        auto Widget = NewObject<UUserWidget>(GetTransientPackage(),Class);
        Widget->AddToRoot(); check(Widget->Initialize());
        auto TextCall = [Widget](FName Name, const TCHAR* Text) {
            struct { FString Text; } Args { Text };
            auto Fn = Widget->FindFunction(Name); check(Fn); Widget->ProcessEvent(Fn,&Args);
        };
        auto Action = CastChecked<UTextBlock>(Widget->WidgetTree->FindWidget(TEXT("ActionLabel")));
        auto Key = Widget->WidgetTree->FindWidget(TEXT("KeySize"));
        TextCall(TEXT("SetMainText"),TEXT("[E] Open arena shop"));
        check(Action->GetText().ToString() == TEXT("Open arena shop"));
        check(Key->GetVisibility() == ESlateVisibility::HitTestInvisible);
        TextCall(TEXT("SetMainText"),TEXT("[F2] Open store"));
        check(CastChecked<UTextBlock>(Widget->WidgetTree->FindWidget(TEXT("KeyLabel")))->GetText().ToString() == TEXT("F2"));
        check(Action->GetText().ToString() == TEXT("Open store"));
        TextCall(TEXT("SetMainText"),TEXT("Arena starts soon"));
        check(Action->GetText().ToString() == TEXT("Arena starts soon"));
        check(Key->GetVisibility() == ESlateVisibility::Collapsed);
        TextCall(TEXT("SetSecondText"),TEXT("Second line"));
        TextCall(TEXT("SetThirdText"),TEXT("Third line"));
        check(CastChecked<UTextBlock>(Widget->WidgetTree->FindWidget(TEXT("SecondLabel")))->GetText().ToString() == TEXT("Second line"));
        check(CastChecked<UTextBlock>(Widget->WidgetTree->FindWidget(TEXT("ThirdLabel")))->GetText().ToString() == TEXT("Third line"));
        struct { bool Visible; } Vis { false };
        Widget->ProcessEvent(Widget->FindFunctionChecked(TEXT("SetWidgetVisibility")),&Vis);
        check(Widget->GetVisibility() == ESlateVisibility::Collapsed);
        Vis.Visible = true; Widget->ProcessEvent(Widget->FindFunctionChecked(TEXT("SetWidgetVisibility")),&Vis);
        check(Widget->GetVisibility() == ESlateVisibility::HitTestInvisible);
        TextCall(TEXT("SetMainText"),TEXT("[E] Open store")); TextCall(TEXT("SetSecondText"),TEXT("")); TextCall(TEXT("SetThirdText"),TEXT(""));
        auto Left = Widget->WidgetTree->FindWidget(TEXT("LeftNotice"));
        auto Right = Widget->WidgetTree->FindWidget(TEXT("RightNotice"));
        check(Left->GetVisibility()==ESlateVisibility::Collapsed && Right->GetVisibility()==ESlateVisibility::Collapsed);
        TextCall(TEXT("SetSecondText"),TEXT("@oui|-190.0|Arena notice|Your next challenge awaits."));
        check(Left->GetVisibility()==ESlateVisibility::HitTestInvisible && Left->GetRenderTransform().Translation.X==-190);
        check(CastChecked<UTextBlock>(Widget->WidgetTree->FindWidget(TEXT("LeftTitle")))->GetText().ToString()==TEXT("Arena notice"));
        TextCall(TEXT("SetThirdText"),TEXT("@oui|380.0|Journal updated|A new opportunity awaits in the Imperial City."));
        check(Right->GetRenderTransform().Translation.X==380);
        check(CastChecked<UTextBlock>(Widget->WidgetTree->FindWidget(TEXT("RightBody")))->GetText().ToString()==TEXT("A new opportunity awaits in the Imperial City."));
        check(Action->GetText().ToString()==TEXT("Open store"));
        TextCall(TEXT("SetSecondText"),TEXT("")); TextCall(TEXT("SetThirdText"),TEXT(""));
        check(Left->GetVisibility()==ESlateVisibility::Collapsed && Right->GetVisibility()==ESlateVisibility::Collapsed);
        if (Params.Contains(TEXT("Preview")))
        {
            TextCall(TEXT("SetSecondText"),TEXT("@oui|0|Arena notice|Your next challenge awaits."));
            TextCall(TEXT("SetThirdText"),TEXT("@oui|0|Journal updated|A new opportunity awaits in the Imperial City."));
            FTextureCompilingManager::Get().FinishAllCompilation(); FlushRenderingCommands();
            auto PaperBorder=CastChecked<UBorder>(Widget->WidgetTree->FindWidget(TEXT("LeftPaper")));
            check(PaperBorder->Background.GetResourceObject()==MakePaper());
            FWidgetRenderer Renderer(true);
            auto RT = Renderer.DrawWidget(Widget->TakeWidget(),FVector2D(1280,720));
            check(RT); FBufferArchive PNG; check(FImageUtils::ExportRenderTarget2DAsPNG(RT,PNG));
            check(FFileHelper::SaveArrayToFile(PNG,*(FPaths::ProjectDir()/TEXT("Output/PromptPreview.png"))));
        }
        Widget->RemoveFromRoot();
        UE_LOG(LogTemp,Display,TEXT("OBLIVION_UI_VALIDATED: prefix, plain message, secondary lines, hide/show, left/right notice text, slide offsets, dismissal and prompt coexistence passed."));
        return 0;
    }
    const bool Adapter = Params.Contains(TEXT("Adapter"));
    const FName AssetName = Adapter ? TEXT("WBP_GameMessage") : TEXT("WBP_InteractionPrompt");
    const FString PackageName = Adapter ? TEXT("/Game/Mods/OblivionMp/WBP_GameMessage") : TEXT("/Game/OblivionUI/WBP_InteractionPrompt");
    if (FPackageName::DoesPackageExist(PackageName))
    {
        UE_LOG(LogTemp, Error, TEXT("Asset already exists. Move it aside before regenerating to preserve designer edits."));
        return 1;
    }
    UPackage* Package = CreatePackage(*PackageName);
    auto BP = CastChecked<UWidgetBlueprint>(FKismetEditorUtilities::CreateBlueprint(UUserWidget::StaticClass(), Package,
        AssetName, BPTYPE_Normal, UWidgetBlueprint::StaticClass(), UWidgetBlueprintGeneratedClass::StaticClass()));
    UWidgetTree* Tree = BP->WidgetTree;
    auto Root = Tree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(), TEXT("Root"));
    Tree->RootWidget = Root;
    Root->SetVisibility(ESlateVisibility::HitTestInvisible);
    auto Row = Tree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass(), TEXT("PromptRow"));
    auto Stack = Tree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass(), TEXT("PromptStack"));
    Stack->AddChild(Row);
    auto RowSlot = Root->AddChildToCanvas(Stack);
    RowSlot->SetAnchors(FAnchors(.5f, .70f)); RowSlot->SetAlignment(FVector2D(.5f,.5f));
    RowSlot->SetAutoSize(true); RowSlot->SetPosition(FVector2D::ZeroVector);
    auto Size = Tree->ConstructWidget<USizeBox>(USizeBox::StaticClass(), TEXT("KeySize"));
    Size->bIsVariable = true;
    Size->SetMinDesiredWidth(30); Size->SetHeightOverride(30);
    Row->AddChildToHorizontalBox(Size)->SetVerticalAlignment(VAlign_Center);
    auto Frame = Tree->ConstructWidget<UBorder>(UBorder::StaticClass(), TEXT("KeyFrame"));
    Frame->SetBrushColor(FLinearColor(.48f,.40f,.27f,1)); Frame->SetPadding(FMargin(1)); Size->AddChild(Frame);
    auto Inset = Tree->ConstructWidget<UBorder>(UBorder::StaticClass(), TEXT("KeyInset"));
    Inset->SetBrushColor(FLinearColor(.025f,.021f,.016f,.87f)); Inset->SetPadding(FMargin(5,0));
    Inset->SetHorizontalAlignment(HAlign_Center); Inset->SetVerticalAlignment(VAlign_Center); Frame->AddChild(Inset);
    auto Key = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass(), TEXT("KeyLabel"));
    auto Label = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass(), TEXT("ActionLabel"));
    Key->bIsVariable = true; Label->bIsVariable = true;
    Key->SetText(FText::FromString(TEXT("E"))); Label->SetText(FText::FromString(TEXT("Open store")));
    UObject* Font = MakeFont();
    Key->SetFont(FSlateFontInfo(LoadObject<UObject>(nullptr,TEXT("/Engine/EngineFonts/Roboto.Roboto")), 16, TEXT("Bold"))); Label->SetFont(FSlateFontInfo(Font, 20));
    for (UTextBlock* Text : { Key, Label })
    {
        Text->SetColorAndOpacity(FSlateColor(FLinearColor(.96f,.93f,.84f,1)));
        Text->SetShadowOffset(FVector2D(1,1)); Text->SetShadowColorAndOpacity(FLinearColor(0,0,0,.85f));
    }
    Inset->AddChild(Key);
    auto LabelSlot = Row->AddChildToHorizontalBox(Label);
    LabelSlot->SetPadding(FMargin(9,0,0,0)); LabelSlot->SetVerticalAlignment(VAlign_Center);
    if (Adapter)
    {
        for(const FString Name : {FString(TEXT("Book")),FString(TEXT("BookControl"))}) {
            FString ClassPath=TEXT("/Game/OblivionUI/Book/WBP_")+Name+TEXT(".WBP_")+Name+TEXT("_C");
            if(auto BookClass=LoadClass<UUserWidget>(nullptr,*ClassPath)) {
                FEdGraphPinType ClassType; ClassType.PinCategory=UEdGraphSchema_K2::PC_Class; ClassType.PinSubCategoryObject=UUserWidget::StaticClass();
                FBlueprintEditorUtils::AddMemberVariable(BP,FName(*(Name+TEXT("Class"))),ClassType,BookClass->GetPathName());
            }
        }
        auto Paper = MakePaper();
        for (const FString Side : { FString(TEXT("Left")), FString(TEXT("Right")) })
        {
            const bool Right = Side == TEXT("Right");
            auto Notice = Tree->ConstructWidget<USizeBox>(USizeBox::StaticClass(),FName(*(Side+TEXT("Notice"))));
            Notice->bIsVariable=true; Notice->SetWidthOverride(340); Notice->SetVisibility(ESlateVisibility::Collapsed);
            auto Slot = Root->AddChildToCanvas(Notice); Slot->SetAnchors(FAnchors(Right?1.f:0.f,.20f));
            Slot->SetAlignment(FVector2D(Right?1.f:0.f,0)); Slot->SetAutoSize(true); Slot->SetPosition(FVector2D(Right?-28.f:28.f,0));
            auto Background = Tree->ConstructWidget<UBorder>(UBorder::StaticClass(),FName(*(Side+TEXT("Paper")))); Background->SetBrushFromTexture(Paper);
            auto PaperBrush=Background->Background;
            PaperBrush.DrawAs=ESlateBrushDrawType::Image;
            Background->SetBrush(PaperBrush);
            Background->SetPadding(FMargin(24,18,24,20)); Notice->AddChild(Background);
            auto Lines = Tree->ConstructWidget<UVerticalBox>(); Background->AddChild(Lines);
            auto Title = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass(),FName(*(Side+TEXT("Title"))));
            auto Body = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass(),FName(*(Side+TEXT("Body"))));
            Title->bIsVariable=true; Body->bIsVariable=true;
            Title->SetFont(FSlateFontInfo(Font,22)); Body->SetFont(FSlateFontInfo(Font,17));
            for (auto Text : {Title,Body}) { Text->SetColorAndOpacity(FSlateColor(FLinearColor(.075f,.048f,.027f,1))); FindFProperty<FFloatProperty>(UTextBlock::StaticClass(),TEXT("WrapTextAt"))->SetPropertyValue_InContainer(Text,292.f); }
            Lines->AddChildToVerticalBox(Title)->SetPadding(FMargin(0,0,0,6));
            auto Rule = Tree->ConstructWidget<UBorder>(); Rule->SetBrushColor(FLinearColor(.24f,.16f,.075f,.5f)); Rule->SetPadding(FMargin(0));
            auto RuleSize = Tree->ConstructWidget<USizeBox>(); RuleSize->SetHeightOverride(1); RuleSize->AddChild(Rule);
            Lines->AddChildToVerticalBox(RuleSize)->SetPadding(FMargin(0,0,0,6)); Lines->AddChild(Body);
        }
        for (FName Name : { FName(TEXT("SecondLabel")), FName(TEXT("ThirdLabel")) })
        {
            auto Extra = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass(), Name);
            Extra->bIsVariable = true; Extra->SetFont(FSlateFontInfo(Font,18));
            Extra->SetVisibility(ESlateVisibility::Collapsed);
            Extra->SetColorAndOpacity(FSlateColor(FLinearColor(.96f,.93f,.84f,1)));
            Extra->SetShadowOffset(FVector2D(1,2)); Stack->AddChild(Extra);
        }
    }
    FKismetEditorUtilities::CompileBlueprint(BP);
    MakeSetPrompt(BP);
    if (Adapter)
    {
        MakeAdapterFunction(BP,TEXT("SetMainText"),TEXT("ActionLabel"));
        MakeAdapterFunction(BP,TEXT("SetSecondText"),TEXT("SecondLabel"));
        MakeAdapterFunction(BP,TEXT("SetThirdText"),TEXT("ThirdLabel"));
        MakeAdapterFunction(BP,TEXT("SetWidgetVisibility"),NAME_None,true);
    }
    FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP);
    FKismetEditorUtilities::CompileBlueprint(BP);
    if (BP->Status == BS_Error) return 2;
    auto CDO = CastChecked<UUserWidget>(BP->GeneratedClass->GetDefaultObject());
    CDO->SetVisibility(ESlateVisibility::HitTestInvisible);
    const FString Filename = FPackageName::LongPackageNameToFilename(PackageName, FPackageName::GetAssetPackageExtension());
    FSavePackageArgs Args; Args.TopLevelFlags = RF_Public | RF_Standalone;
    if (!UPackage::SavePackage(Package, BP, *Filename, Args)) return 3;
    UE_LOG(LogTemp, Display, TEXT("OBLIVION_UI_BUILT %s; SetPrompt(KeyText,ActionText) compiled."), *Filename);
    return 0;
}



