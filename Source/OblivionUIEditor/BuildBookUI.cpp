#include "WidgetBlueprint.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintGeneratedClass.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/SizeBox.h"
#include "Components/ScaleBox.h"
#include "Components/ScaleBoxSlot.h"
#include "Components/Border.h"
#include "Components/Image.h"
#include "Components/TextBlock.h"
#include "Components/Button.h"
#include "Components/Slider.h"
#include "Components/ProgressBar.h"
#include "K2Node_MakeStruct.h"
#include "Blueprint/SlateBlueprintLibrary.h"
#include "Components/CheckBox.h"
#include "Components/ComboBoxString.h"
#include "Components/EditableTextBox.h"
#include "Components/ScrollBox.h"
#include "Engine/Texture2D.h"
#include "Engine/Font.h"
#include "Kismet/KismetTextLibrary.h"
#include "Kismet/KismetStringLibrary.h"
#include "Components/SceneComponent.h"
#include "Kismet/KismetMathLibrary.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Kismet2/BlueprintEditorUtils.h"
#include "K2Node_FunctionEntry.h"
#include "K2Node_VariableGet.h"
#include "K2Node_VariableSet.h"
#include "K2Node_CallFunction.h"
#include "K2Node_ComponentBoundEvent.h"
#include "EdGraphSchema_K2.h"
#include "ImageUtils.h"
#include "UObject/SavePackage.h"
#include "Misc/PackageName.h"
#include "Misc/FileHelper.h"
#include "Slate/WidgetRenderer.h"
#include "Framework/Application/SlateApplication.h"
#include "Interfaces/ISlateRHIRendererModule.h"
#include "Serialization/BufferArchive.h"
#include "TextureCompiler.h"
#include "ShaderCompiler.h"
#include "GameFramework/PlayerController.h"
#include "Brushes/SlateRoundedBoxBrush.h"
#include "Brushes/SlateNoResource.h"
#include "Brushes/SlateColorBrush.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Blueprint/WidgetLayoutLibrary.h"
#include "Camera/PlayerCameraManager.h"

namespace BookBuild
{
static UTexture2D* Art(const TCHAR* Name){return LoadObject<UTexture2D>(nullptr,*(FString(TEXT("/Game/OblivionUI/Book/T_Imperial"))+Name+TEXT(".T_Imperial")+Name));}
static FSlateBrush ArtBrush(const TCHAR* Name,FVector2D Size=FVector2D(64,32),bool Frame=false){
    FSlateBrush B;B.SetResourceObject(Art(Name));check(B.GetResourceObject());B.ImageSize=Size;B.TintColor=FLinearColor::White;
    B.DrawAs=Frame?ESlateBrushDrawType::Box:ESlateBrushDrawType::Image;
    if(Frame){B.Margin=FMargin(.055f,.085f);B.SetUVRegion(FBox2f(FVector2f(.006f,.19f),FVector2f(.996f,.79f)));}
    if(FString(Name)==TEXT("HudPaper"))B.SetUVRegion(FBox2f(FVector2f(0,.085f),FVector2f(1,.94f)));
    return B;
}
static void ThemeProgress(UProgressBar* Bar) {
    auto Style=Bar->GetWidgetStyle();
    Style.BackgroundImage=ArtBrush(TEXT("EngravedFrame"),FVector2D(80,12),true);
    Style.BackgroundImage.TintColor=FLinearColor(.38f,.34f,.28f,1);
    Style.FillImage=ArtBrush(TEXT("PatinatedMetal"),FVector2D(128,12));
    Bar->SetWidgetStyle(Style);Bar->SetFillColorAndOpacity(FLinearColor(1,1,1,1));
}
static UEdGraphPin* P(UEdGraphNode* N,FName Name) { auto R=N->FindPin(Name); checkf(R,TEXT("Missing book pin %s"),*Name.ToString()); return R; }
static void Link(UEdGraphPin* A,UEdGraphPin* B) { check(GetDefault<UEdGraphSchema_K2>()->TryCreateConnection(A,B)); }
static UK2Node_CallFunction* Call(UEdGraph* G,UClass* C,FName Name) {
    auto Fn=C->FindFunctionByName(Name); checkf(Fn,TEXT("Missing function %s"),*Name.ToString());
    auto N=NewObject<UK2Node_CallFunction>(G); N->SetFromFunction(Fn); G->AddNode(N); N->CreateNewGuid(); N->AllocateDefaultPins(); return N;
}
static UK2Node_VariableGet* Get(UEdGraph* G,FName Name) {
    auto N=NewObject<UK2Node_VariableGet>(G); N->VariableReference.SetSelfMember(Name); G->AddNode(N); N->CreateNewGuid(); N->AllocateDefaultPins(); return N;
}
static void Save(UObject* Obj) {
    FSavePackageArgs A; A.TopLevelFlags=RF_Public|RF_Standalone;
    check(UPackage::SavePackage(Obj->GetOutermost(),Obj,*FPackageName::LongPackageNameToFilename(Obj->GetOutermost()->GetName(),TEXT(".uasset")),A));
}
static void StringSetter(UWidgetBlueprint* BP,FName Function,FName Target,UClass* Class=UTextBlock::StaticClass()) {
    auto G=FBlueprintEditorUtils::CreateNewGraph(BP,Function,UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());
    FBlueprintEditorUtils::AddFunctionGraph(BP,G,true,static_cast<UFunction*>(nullptr));
    UK2Node_FunctionEntry* E=nullptr; for(auto N:G->Nodes) if(auto Entry=Cast<UK2Node_FunctionEntry>(N)) E=Entry; check(E);
    FEdGraphPinType T; T.PinCategory=UEdGraphSchema_K2::PC_String; E->CreateUserDefinedPin(TEXT("Text"),T,EGPD_Output);
    auto Conv=Call(G,UKismetTextLibrary::StaticClass(),TEXT("Conv_StringToText")); Link(P(E,TEXT("Text")),P(Conv,TEXT("InString")));
    auto Set=Call(G,Class,TEXT("SetText")); Link(P(E,TEXT("then")),P(Set,TEXT("execute")));
    Link(P(Get(G,Target),Target),P(Set,TEXT("self"))); Link(P(Conv,TEXT("ReturnValue")),P(Set,TEXT("InText")));
}
static void InkSetter(UWidgetBlueprint* BP,FName Function,FName Target) {
    auto G=FBlueprintEditorUtils::CreateNewGraph(BP,Function,UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());FBlueprintEditorUtils::AddFunctionGraph(BP,G,true,static_cast<UFunction*>(nullptr));
    UK2Node_FunctionEntry* E=nullptr;for(auto N:G->Nodes)if(auto Entry=Cast<UK2Node_FunctionEntry>(N))E=Entry;check(E);
    FEdGraphPinType Type;Type.PinCategory=UEdGraphSchema_K2::PC_Struct;Type.PinSubCategoryObject=TBaseStructure<FLinearColor>::Get();E->CreateUserDefinedPin(TEXT("Color"),Type,EGPD_Output);
    auto Color=NewObject<UK2Node_MakeStruct>(G);Color->StructType=FSlateColor::StaticStruct();G->AddNode(Color);Color->CreateNewGuid();Color->AllocateDefaultPins();Link(P(E,TEXT("Color")),P(Color,TEXT("SpecifiedColor")));
    auto Set=Call(G,UTextBlock::StaticClass(),TEXT("SetColorAndOpacity"));Link(P(E,TEXT("then")),P(Set,TEXT("execute")));Link(P(Get(G,Target),Target),P(Set,TEXT("self")));Link(P(Color,TEXT("SlateColor")),P(Set,TEXT("InColorAndOpacity")));
}
static void FontSetter(UWidgetBlueprint* BP,FName Function,FName Target) {
    auto G=FBlueprintEditorUtils::CreateNewGraph(BP,Function,UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());FBlueprintEditorUtils::AddFunctionGraph(BP,G,true,static_cast<UFunction*>(nullptr));
    UK2Node_FunctionEntry* E=nullptr;for(auto N:G->Nodes)if(auto Entry=Cast<UK2Node_FunctionEntry>(N))E=Entry;check(E);
    FEdGraphPinType Type;Type.PinCategory=UEdGraphSchema_K2::PC_Int;E->CreateUserDefinedPin(TEXT("Size"),Type,EGPD_Output);
    auto Font=NewObject<UK2Node_MakeStruct>(G);Font->StructType=FSlateFontInfo::StaticStruct();G->AddNode(Font);Font->CreateNewGuid();Font->AllocateDefaultPins();
    Link(P(E,TEXT("Size")),P(Font,TEXT("Size")));GetDefault<UEdGraphSchema_K2>()->TrySetDefaultObject(*P(Font,TEXT("FontObject")),LoadObject<UFont>(nullptr,TEXT("/Game/OblivionUI/Fonts/Petrock.Petrock")));
    auto Set=Call(G,UTextBlock::StaticClass(),TEXT("SetFont"));Link(P(E,TEXT("then")),P(Set,TEXT("execute")));Link(P(Get(G,Target),Target),P(Set,TEXT("self")));Link(P(Font,TEXT("SlateFontInfo")),P(Set,TEXT("InFontInfo")));
}
static void ArtworkSetter(UWidgetBlueprint* BP,FName Function=TEXT("SetArtwork"),FName Target=TEXT("Artwork"),bool Slices=false,int SliceCount=17) {
    auto G=FBlueprintEditorUtils::CreateNewGraph(BP,Function,UEdGraph::StaticClass(),UEdGraphSchema_K2::StaticClass());
    FBlueprintEditorUtils::AddFunctionGraph(BP,G,true,static_cast<UFunction*>(nullptr));
    UK2Node_FunctionEntry* E=nullptr;for(auto N:G->Nodes)if(auto Entry=Cast<UK2Node_FunctionEntry>(N))E=Entry;check(E);
    FEdGraphPinType Type;Type.PinCategory=UEdGraphSchema_K2::PC_String;E->CreateUserDefinedPin(TEXT("Text"),Type,EGPD_Output);
    auto Path=Call(G,UKismetSystemLibrary::StaticClass(),TEXT("MakeSoftObjectPath"));Link(P(E,TEXT("Text")),P(Path,TEXT("PathString")));
    auto Ref=Call(G,UKismetSystemLibrary::StaticClass(),TEXT("Conv_SoftObjPathToSoftObjRef"));Link(P(Path,TEXT("ReturnValue")),P(Ref,TEXT("SoftObjectPath")));
    auto Load=Call(G,UKismetSystemLibrary::StaticClass(),TEXT("LoadAsset_Blocking"));Link(P(Ref,TEXT("ReturnValue")),P(Load,TEXT("Asset")));Link(P(E,TEXT("then")),P(Load,TEXT("execute")));
    auto Exec=P(Load,TEXT("then"));
    for(int I=0;I<(Slices?SliceCount:1);I++){
        FName ImageName=Slices?FName(*(Target.ToString()+FString::FromInt(I))):Target;
        auto Set=Call(G,UImage::StaticClass(),TEXT("SetBrushResourceObject"));Link(Exec,P(Set,TEXT("execute")));Link(P(Load,TEXT("ReturnValue")),P(Set,TEXT("ResourceObject")));Link(P(Get(G,ImageName),ImageName),P(Set,TEXT("self")));Exec=P(Set,TEXT("then"));
    }
}
static void Event(UWidgetBlueprint* BP,FName Component,FName Delegate,FName Serial) {
    auto G=BP->UbergraphPages[0];
    auto Prop=FindFProperty<FObjectProperty>(BP->SkeletonGeneratedClass,Component); check(Prop);
    auto D=FindFProperty<FMulticastDelegateProperty>(Prop->PropertyClass,Delegate); check(D);
    auto E=NewObject<UK2Node_ComponentBoundEvent>(G); G->AddNode(E); E->CreateNewGuid(); E->InitializeComponentBoundEventParams(Prop,D); E->AllocateDefaultPins();
    auto Add=Call(G,UKismetMathLibrary::StaticClass(),TEXT("Add_IntInt")); Link(P(Get(G,Serial),Serial),P(Add,TEXT("A")));
    GetDefault<UEdGraphSchema_K2>()->TrySetDefaultValue(*P(Add,TEXT("B")),TEXT("1"));
    auto Set=NewObject<UK2Node_VariableSet>(G); Set->VariableReference.SetSelfMember(Serial); G->AddNode(Set); Set->CreateNewGuid(); Set->AllocateDefaultPins();
    Link(P(E,TEXT("then")),P(Set,TEXT("execute"))); Link(P(Add,TEXT("ReturnValue")),P(Set,Serial));
    if (Component==TEXT("TextInput")) {
        auto Conv=Call(G,UKismetTextLibrary::StaticClass(),TEXT("Conv_TextToString")); Link(P(E,TEXT("Text")),P(Conv,TEXT("InText")));
        auto Value=NewObject<UK2Node_VariableSet>(G); Value->VariableReference.SetSelfMember(TEXT("InputValue")); G->AddNode(Value); Value->CreateNewGuid(); Value->AllocateDefaultPins();
        Link(P(Set,TEXT("then")),P(Value,TEXT("execute"))); Link(P(Conv,TEXT("ReturnValue")),P(Value,TEXT("InputValue")));
    }
}
static UWidgetBlueprint* NewBP(FString Name) {
    FString Path=TEXT("/Game/OblivionUI/Book/")+Name;
    checkf(!FPackageName::DoesPackageExist(Path),TEXT("Move existing book assets aside before regeneration"));
    return CastChecked<UWidgetBlueprint>(FKismetEditorUtilities::CreateBlueprint(UUserWidget::StaticClass(),CreatePackage(*Path),FName(*Name),BPTYPE_Normal,UWidgetBlueprint::StaticClass(),UWidgetBlueprintGeneratedClass::StaticClass()));
}
static UCanvasPanelSlot* Place(UCanvasPanel* Root,UWidget* W,float X,float Y,float Width,float Height) {
    auto S=Root->AddChildToCanvas(W); S->SetPosition({X,Y}); S->SetSize({Width,Height}); return S;
}
static UTextBlock* Text(UWidgetTree* T,FName Name,int Size,const TCHAR* Value) {
    auto W=T->ConstructWidget<UTextBlock>(UTextBlock::StaticClass(),Name); W->bIsVariable=true;
    W->SetText(FText::FromString(Value)); W->SetFont(FSlateFontInfo(LoadObject<UFont>(nullptr,TEXT("/Game/OblivionUI/Fonts/Petrock.Petrock")),Size));
    W->SetColorAndOpacity(FSlateColor(FLinearColor(.09f,.055f,.03f,1))); W->SetAutoWrapText(true); return W;
}
static UButton* Button(UWidgetTree* T,FName Name,const TCHAR* Label) {
    auto W=T->ConstructWidget<UButton>(UButton::StaticClass(),Name); W->bIsVariable=true;
    FButtonStyle Style=W->GetStyle();
    Style.Normal=ArtBrush(TEXT("EngravedFrame"),FVector2D(120,38),true);
    Style.Hovered=Style.Normal;Style.Hovered.TintColor=FSlateColor(FLinearColor(1.13f,1.10f,1.02f,1));
    Style.Pressed=Style.Normal;Style.Pressed.TintColor=FSlateColor(FLinearColor(.75f,.71f,.62f,1));
    Style.Disabled=Style.Normal;Style.Disabled.TintColor=FSlateColor(FLinearColor(.78f,.76f,.72f,.65f));
    if(Name==TEXT("CloseButton")||Name==TEXT("PreviousButton")||Name==TEXT("NextButton")){
        Style.Normal=FSlateNoResource();Style.Hovered=FSlateNoResource();
        Style.Pressed=FSlateNoResource();Style.Disabled=FSlateNoResource();
    }
    W->SetStyle(Style); auto L=Text(T,FName(*(Name.ToString()+TEXT("Caption"))),21,Label); L->SetAutoWrapText(false); L->SetJustification(ETextJustify::Center); W->AddChild(L); return W;
}
static void IntVar(UWidgetBlueprint* BP,FName Name) { FEdGraphPinType T; T.PinCategory=UEdGraphSchema_K2::PC_Int; FBlueprintEditorUtils::AddMemberVariable(BP,Name,T,TEXT("0")); }
static void Finalize(UWidgetBlueprint* BP) { FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP); FKismetEditorUtilities::CompileBlueprint(BP); check(BP->Status!=BS_Error); Save(BP); }
}

int32 BuildBookUI()
{
    using namespace BookBuild;
    for(auto Name:{TEXT("Vellum"),TEXT("EngravedFrame"),TEXT("HudPaper"),TEXT("PatinatedMetal"),TEXT("Chevron"),TEXT("Tick"),TEXT("TabAtlas")}){
        if(auto Existing=Art(Name)){if(FString(Name)==TEXT("EngravedFrame")&&Existing->MaxTextureSize!=256){Existing->MaxTextureSize=256;Existing->PostEditChange();Save(Existing);}continue;}
        auto Imported=FImageUtils::ImportFileAsTexture2D(FPaths::ProjectDir()/TEXT("Art/Imperial/")+Name+TEXT(".png"));check(Imported);
        FString Package=FString(TEXT("/Game/OblivionUI/Book/T_Imperial"))+Name;
        auto A=DuplicateObject<UTexture2D>(Imported,CreatePackage(*Package),*(FString(TEXT("T_Imperial"))+Name));
        auto& Mip=Imported->GetPlatformData()->Mips[0];auto Pixels=Mip.BulkData.LockReadOnly();
        A->Source.Init(Imported->GetSizeX(),Imported->GetSizeY(),1,1,TSF_BGRA8,static_cast<const uint8*>(Pixels));Mip.BulkData.Unlock();
        A->ClearFlags(RF_Transient);A->SetFlags(RF_Public|RF_Standalone);A->LODGroup=TEXTUREGROUP_UI;A->CompressionSettings=TC_EditorIcon;A->MipGenSettings=TMGS_NoMipmaps;if(FString(Name)==TEXT("EngravedFrame"))A->MaxTextureSize=256;else if(FString(Name)==TEXT("Chevron")||FString(Name)==TEXT("Tick"))A->MaxTextureSize=128;A->UpdateResource();Save(A);
    }
    auto Texture=LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/Book/T_Book.T_Book"));
    if(!Texture) {
        auto Imported=FImageUtils::ImportFileAsTexture2D(FPaths::ProjectDir()/TEXT("Art/BookBackground.png")); check(Imported);
        Texture=DuplicateObject<UTexture2D>(Imported,CreatePackage(TEXT("/Game/OblivionUI/Book/T_Book")),TEXT("T_Book"));
        auto& Mip=Imported->GetPlatformData()->Mips[0]; auto Pixels=Mip.BulkData.LockReadOnly();
        Texture->Source.Init(Imported->GetSizeX(),Imported->GetSizeY(),1,1,TSF_BGRA8,static_cast<const uint8*>(Pixels)); Mip.BulkData.Unlock();
        Texture->ClearFlags(RF_Transient); Texture->SetFlags(RF_Public|RF_Standalone); Texture->LODGroup=TEXTUREGROUP_UI; Texture->CompressionSettings=TC_EditorIcon; Texture->MipGenSettings=TMGS_NoMipmaps; Texture->UpdateResource(); Save(Texture);
    }
    auto BP=NewBP(TEXT("WBP_BookControl")); auto T=BP->WidgetTree;
    auto Root=T->ConstructWidget<UCanvasPanel>(); T->RootWidget=Root;
    Place(Root,Text(T,TEXT("Label"),22,TEXT("Control title")),0,0,600,32)->SetAnchors(FAnchors(0,0,1,0));
    auto LabelSlot=CastChecked<UCanvasPanelSlot>(T->FindWidget(TEXT("Label"))->Slot); LabelSlot->SetOffsets(FMargin(0,0,0,32));
    Place(Root,Text(T,TEXT("Description"),16,TEXT("Supporting description")),0,34,600,42)->SetAnchors(FAnchors(0,0,1,0));
    CastChecked<UCanvasPanelSlot>(T->FindWidget(TEXT("Description"))->Slot)->SetOffsets(FMargin(0,34,0,42));
    auto Btn=Button(T,TEXT("ActionButton"),TEXT("Choose")); Place(Root,Btn,0,80,600,38);
    auto Slider=T->ConstructWidget<USlider>(USlider::StaticClass(),TEXT("ValueSlider")); Slider->bIsVariable=true; Slider->SetSliderBarColor(FLinearColor(.22f,.13f,.055f,1)); Slider->SetSliderHandleColor(FLinearColor(.42f,.28f,.11f,1)); Place(Root,Slider,0,80,600,32);
    auto SliderStyle=Slider->GetWidgetStyle();SliderStyle.NormalBarImage=SliderStyle.HoveredBarImage=SliderStyle.DisabledBarImage=ArtBrush(TEXT("EngravedFrame"),FVector2D(80,8),true);
    SliderStyle.NormalThumbImage=SliderStyle.HoveredThumbImage=SliderStyle.DisabledThumbImage=ArtBrush(TEXT("EngravedFrame"),FVector2D(16,22),true);SliderStyle.BarThickness=8;Slider->SetWidgetStyle(SliderStyle);Slider->SetSliderBarColor(FLinearColor(.5f,.46f,.37f,1));Slider->SetSliderHandleColor(FLinearColor::White);
    auto Toggle=T->ConstructWidget<UCheckBox>(UCheckBox::StaticClass(),TEXT("Toggle")); Toggle->bIsVariable=true; Place(Root,Toggle,0,80,600,36);
    auto Combo=T->ConstructWidget<UComboBoxString>(UComboBoxString::StaticClass(),TEXT("Dropdown")); Combo->bIsVariable=true; Place(Root,Combo,0,80,600,38);
    auto Input=T->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass(),TEXT("TextInput")); Input->bIsVariable=true; Place(Root,Input,0,80,600,38);
    auto ComboStyle=Combo->GetWidgetStyle(); ComboStyle.ComboButtonStyle.ButtonStyle=Btn->GetStyle();
    ComboStyle.ComboButtonStyle.MenuBorderBrush=ArtBrush(TEXT("Vellum")); Combo->SetWidgetStyle(ComboStyle);
    ComboStyle.ComboButtonStyle.DownArrowImage=ArtBrush(TEXT("Chevron"),FVector2D(16,16));Combo->SetWidgetStyle(ComboStyle);
    auto ItemStyle=Combo->GetItemStyle(); ItemStyle.ActiveBrush=Btn->GetStyle().Pressed; ItemStyle.ActiveHoveredBrush=Btn->GetStyle().Hovered;
    ItemStyle.InactiveBrush=Btn->GetStyle().Normal; ItemStyle.InactiveHoveredBrush=Btn->GetStyle().Hovered; ItemStyle.SelectorFocusedBrush=Btn->GetStyle().Hovered;
    ItemStyle.TextColor=FSlateColor(FLinearColor(.09f,.055f,.03f,1)); ItemStyle.SelectedTextColor=ItemStyle.TextColor; Combo->SetItemStyle(ItemStyle);
    auto CheckStyle=Toggle->GetWidgetStyle();
    for(auto Brush : {&CheckStyle.UncheckedImage,&CheckStyle.UncheckedHoveredImage,&CheckStyle.UncheckedPressedImage,&CheckStyle.CheckedImage,&CheckStyle.CheckedHoveredImage,&CheckStyle.CheckedPressedImage}) Brush->TintColor=FSlateColor(FLinearColor(.3f,.18f,.07f,1));
    CheckStyle.UncheckedImage=CheckStyle.UncheckedHoveredImage=CheckStyle.UncheckedPressedImage=ArtBrush(TEXT("EngravedFrame"),FVector2D(22,22),true);
    CheckStyle.CheckedImage=CheckStyle.CheckedHoveredImage=CheckStyle.CheckedPressedImage=ArtBrush(TEXT("Tick"),FVector2D(22,22));
    Toggle->SetWidgetStyle(CheckStyle);
    Combo->Font=FSlateFontInfo(LoadObject<UFont>(nullptr,TEXT("/Game/OblivionUI/Fonts/Petrock.Petrock")),20); Combo->ForegroundColor=FSlateColor(FLinearColor(.09f,.055f,.03f,1));
    auto InputStyle=Input->WidgetStyle; InputStyle.TextStyle.SetFont(Combo->GetFont()); InputStyle.ForegroundColor=Combo->ForegroundColor;
    InputStyle.BackgroundImageNormal=Btn->GetStyle().Normal; InputStyle.BackgroundImageHovered=Btn->GetStyle().Hovered; InputStyle.BackgroundImageFocused=Btn->GetStyle().Pressed; Input->WidgetStyle=InputStyle;
    for(auto W : TArray<UWidget*>{Btn,Slider,Toggle,Combo,Input}) {
        W->SetVisibility(ESlateVisibility::Collapsed); auto S=CastChecked<UCanvasPanelSlot>(W->Slot); S->SetAnchors(FAnchors(0,0,1,0)); S->SetOffsets(FMargin(0,80,0,38));
    }
    auto SliderSlot=CastChecked<UCanvasPanelSlot>(Slider->Slot);SliderSlot->SetOffsets(FMargin(0,80,84,30));
    for(auto Name:{TEXT("SliderMinimum"),TEXT("SliderMiddle"),TEXT("SliderMaximum"),TEXT("SliderCurrent")}){
        auto Value=Text(T,Name,16,TEXT(""));Value->bIsVariable=true;Value->SetVisibility(ESlateVisibility::Collapsed);
        auto Slot=Place(Root,Value,0,111,76,17);
        if(FString(Name)==TEXT("SliderMiddle")){Value->SetJustification(ETextJustify::Center);Slot->SetAnchors(FAnchors(.5f,0));Slot->SetOffsets(FMargin(-80,111,76,17));}
        if(FString(Name)==TEXT("SliderMaximum")){Value->SetJustification(ETextJustify::Right);Slot->SetAnchors(FAnchors(1,0));Slot->SetOffsets(FMargin(-160,111,76,17));}
        if(FString(Name)==TEXT("SliderCurrent")){Value->SetJustification(ETextJustify::Right);Slot->SetAnchors(FAnchors(1,0));Slot->SetOffsets(FMargin(-78,83,76,26));}
    }
    IntVar(BP,TEXT("EventSerial")); FEdGraphPinType String; String.PinCategory=UEdGraphSchema_K2::PC_String; FBlueprintEditorUtils::AddMemberVariable(BP,TEXT("InputValue"),String);
    FKismetEditorUtilities::CompileBlueprint(BP);
    StringSetter(BP,TEXT("SetLabel"),TEXT("Label")); StringSetter(BP,TEXT("SetDescription"),TEXT("Description"));
    StringSetter(BP,TEXT("SetButtonLabel"),TEXT("ActionButtonCaption")); StringSetter(BP,TEXT("SetInput"),TEXT("TextInput"),UEditableTextBox::StaticClass());
    for(auto Name:{TEXT("SliderMinimum"),TEXT("SliderMiddle"),TEXT("SliderMaximum"),TEXT("SliderCurrent")})StringSetter(BP,*(FString(TEXT("Set"))+Name),Name);
    Event(BP,TEXT("ActionButton"),TEXT("OnClicked"),TEXT("EventSerial")); Event(BP,TEXT("ValueSlider"),TEXT("OnValueChanged"),TEXT("EventSerial"));
    Event(BP,TEXT("Toggle"),TEXT("OnCheckStateChanged"),TEXT("EventSerial")); Event(BP,TEXT("Dropdown"),TEXT("OnSelectionChanged"),TEXT("EventSerial")); Event(BP,TEXT("TextInput"),TEXT("OnTextChanged"),TEXT("EventSerial"));
    auto Fill=[&](UWidget* W) {auto Slot=Root->AddChildToCanvas(W);Slot->SetAnchors(FAnchors(0,0,1,1));Slot->SetOffsets(FMargin(0));W->SetVisibility(ESlateVisibility::Collapsed);};
    auto ArtworkScale=T->ConstructWidget<UScaleBox>(UScaleBox::StaticClass(),TEXT("ArtworkScale"));ArtworkScale->bIsVariable=true;ArtworkScale->SetStretch(EStretch::ScaleToFit);Fill(ArtworkScale);
    auto Artwork=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("Artwork"));Artwork->bIsVariable=true;Artwork->SetDesiredSizeOverride(FVector2D(128,128));ArtworkScale->AddChild(Artwork);
    auto Ink=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("Ink"));Ink->bIsVariable=true;Fill(Ink);
    auto ArtCanvas=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("ArtCanvas"));ArtCanvas->bIsVariable=true;ArtCanvas->SetClipping(EWidgetClipping::ClipToBoundsAlways);Fill(ArtCanvas);
    auto Progress=T->ConstructWidget<UProgressBar>(UProgressBar::StaticClass(),TEXT("ValueProgress"));Progress->bIsVariable=true;ThemeProgress(Progress);Fill(Progress);CastChecked<UCanvasPanelSlot>(Progress->Slot)->SetAnchors(FAnchors(0,0,1,0));CastChecked<UCanvasPanelSlot>(Progress->Slot)->SetOffsets(FMargin(0,80,0,24));
    auto CardPaper=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("CardPaper"));CardPaper->bIsVariable=true;Fill(CardPaper);CastChecked<UCanvasPanelSlot>(CardPaper->Slot)->SetZOrder(1);
    // Eight image slices retain the engraved border without drawing or stretching a centre fill.
    const float U[]={.006f,.045f,.957f,.996f},V[]={.20f,.30f,.69f,.79f};
    for(int Y=0;Y<3;Y++)for(int X=0;X<3;X++){
        if(X==1&&Y==1)continue;auto Edge=T->ConstructWidget<UImage>();auto Brush=ArtBrush(TEXT("EngravedFrame"));Brush.SetUVRegion(FBox2f(FVector2f(U[X],V[Y]),FVector2f(U[X+1],V[Y+1])));Edge->SetBrush(Brush);Edge->SetVisibility(ESlateVisibility::HitTestInvisible);
        auto Slot=CardPaper->AddChildToCanvas(Edge);Slot->SetAnchors(FAnchors(X==2?1:0,Y==2?1:0,X==0?0:1,Y==0?0:1));Slot->SetOffsets(FMargin(X==2?-10:X==1?10:0,Y==2?-10:Y==1?10:0,10,10));
    }
    auto Focus=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("FocusRing"));Focus->bIsVariable=true;Fill(Focus);auto FocusBrush=ArtBrush(TEXT("EngravedFrame"),FVector2D(120,38),true);FocusBrush.DrawAs=ESlateBrushDrawType::Border;Focus->SetBrush(FocusBrush);Focus->SetColorAndOpacity(FLinearColor(.5f,.3f,.1f,.12f));
    for(int I=0;I<9;I++){
        auto Icon=T->ConstructWidget<UImage>(UImage::StaticClass(),FName(*FString::Printf(TEXT("Tab%d"),I)));Icon->bIsVariable=true;Fill(Icon);
        auto Brush=ArtBrush(TEXT("TabAtlas"),FVector2D(64,64));Brush.SetUVRegion(FBox2f(FVector2f((I%3)/3.f,(I/3)/3.f),FVector2f((I%3+1)/3.f,(I/3+1)/3.f)));Icon->SetBrush(Brush);
        CastChecked<UCanvasPanelSlot>(Icon->Slot)->SetZOrder(I==7?0:2);
    }
    for(int I=0;I<2;I++){
        FString Name=I==0?TEXT("TabActivePaper"):TEXT("TabInactivePaper");auto Paper=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),FName(*Name));Paper->bIsVariable=true;Fill(Paper);CastChecked<UCanvasPanelSlot>(Paper->Slot)->SetZOrder(0);
        // One native image piece with a 16-unit feather at its lower edge. Thin
        // image slices retain the artwork and avoid requiring a custom shader.
        for(int Band=0;Band<17;Band++){
            float Y=Band==0?0:63+Band,H=Band==0?64:1,A=Band==0?1:1-FMath::SmoothStep(0.f,16.f,Band-.5f);
            auto Slice=T->ConstructWidget<UImage>(UImage::StaticClass(),FName(*(Name+FString::FromInt(Band))));Slice->bIsVariable=true;FSlateBrush Brush;Brush.DrawAs=ESlateBrushDrawType::Image;Brush.ImageSize=FVector2D(140,H);Brush.SetUVRegion(FBox2f(FVector2f(I==0?.084f:.253f,Y/80*.44f),FVector2f(I==0?.253f:.421f,(Y+H)/80*.44f)));Slice->SetBrush(Brush);Slice->SetColorAndOpacity(FLinearColor(1,1,1,A));Slice->SetVisibility(ESlateVisibility::HitTestInvisible);auto S=Place(Paper,Slice,0,Y,0,H);S->SetAnchors(FAnchors(0,0,1,0));
        }
    }
    auto BasePaper=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("TabBasePaper"));BasePaper->bIsVariable=true;Fill(BasePaper);
    for(auto Name:{TEXT("Label"),TEXT("Description"),TEXT("ArtworkScale")}){auto W=T->FindWidget(Name);W->SetVisibility(FString(Name)==TEXT("ArtworkScale")?ESlateVisibility::Collapsed:ESlateVisibility::HitTestInvisible);CastChecked<UCanvasPanelSlot>(W->Slot)->SetZOrder(2);}
    CastChecked<UCanvasPanelSlot>(Focus->Slot)->SetZOrder(3);
    FKismetEditorUtilities::CompileBlueprint(BP);ArtworkSetter(BP);ArtworkSetter(BP,TEXT("SetTabActivePaper"),TEXT("TabActivePaper"),true);ArtworkSetter(BP,TEXT("SetTabInactivePaper"),TEXT("TabInactivePaper"),true);ArtworkSetter(BP,TEXT("SetTabBasePaper"),TEXT("TabBasePaper"));
    InkSetter(BP,TEXT("SetLabelInk"),TEXT("Label"));InkSetter(BP,TEXT("SetDescriptionInk"),TEXT("Description"));InkSetter(BP,TEXT("SetButtonInk"),TEXT("ActionButtonCaption"));
    FontSetter(BP,TEXT("SetLabelSize"),TEXT("Label"));FontSetter(BP,TEXT("SetDescriptionSize"),TEXT("Description"));FontSetter(BP,TEXT("SetButtonSize"),TEXT("ActionButtonCaption"));
    Finalize(BP);

    for(int Placement=0;Placement<4;++Placement) {
    const bool Side=Placement==1 || Placement==2;
    const bool Popup=Placement==3;
    const float PaperWidth=Side ? 500.f : 760.f, Margin=Side ? 42.f : 65.f, BodyWidth=PaperWidth-2*Margin, PaperHeight=Popup ? 410.f : 940.f;
    BP=NewBP(Placement==1 ? TEXT("WBP_BookLeft") : Placement==2 ? TEXT("WBP_BookRight") : Placement==3 ? TEXT("WBP_BookPopup") : TEXT("WBP_Book")); T=BP->WidgetTree;
    Root=T->ConstructWidget<UCanvasPanel>(); T->RootWidget=Root;
    auto Dim=T->ConstructWidget<UBorder>(); Dim->SetBrushColor(FLinearColor(0,0,0,Side ? .18f : .55f)); auto DimSlot=Root->AddChildToCanvas(Dim); DimSlot->SetAnchors(FAnchors(0,0,1,1)); DimSlot->SetOffsets(FMargin(0));
    auto Scale=T->ConstructWidget<UScaleBox>(); Scale->SetStretch(EStretch::ScaleToFit); auto ScaleSlot=Root->AddChildToCanvas(Scale); ScaleSlot->SetAnchors(FAnchors(0,0,1,1)); ScaleSlot->SetOffsets(FMargin(28));
    if(Popup)Scale->SetStretchDirection(EStretchDirection::DownOnly);
    auto Size=T->ConstructWidget<USizeBox>(USizeBox::StaticClass(),TEXT("BookSize")); Size->bIsVariable=true; Size->SetWidthOverride(PaperWidth); Size->SetHeightOverride(PaperHeight);
    CastChecked<UScaleBoxSlot>(Scale->AddChild(Size))->SetHorizontalAlignment(Placement==1 ? HAlign_Left : Placement==2 ? HAlign_Right : HAlign_Center);
    auto Paper=T->ConstructWidget<UCanvasPanel>(); Size->AddChild(Paper);
    auto Image=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("BookBackground")); Image->bIsVariable=true; Image->SetBrushFromTexture(Popup ? LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/T_NotificationParchment.T_NotificationParchment")) : Texture); Place(Paper,Image,0,0,PaperWidth,PaperHeight);
    auto Surface=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("PageSurface"));Surface->bIsVariable=true;FSlateBrush SurfaceBrush;SurfaceBrush.DrawAs=ESlateBrushDrawType::Image;SurfaceBrush.SetUVRegion(FBox2f(FVector2f(20.f/1024,162.f/1024),FVector2f(1003.f/1024,1001.f/1024)));Surface->SetBrush(SurfaceBrush);Place(Paper,Surface,15,72,PaperWidth-30,847);Surface->SetVisibility(ESlateVisibility::Collapsed);
    // Reveal the original leather above the page after the final occupied tab.
    // Clip a full-size copy of the background, preserving its texture coordinates.
    auto Leather=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("TabLeather"));Leather->bIsVariable=true;Leather->SetClipping(EWidgetClipping::ClipToBoundsAlways);Place(Paper,Leather,65,72,PaperWidth-80,80);Leather->SetVisibility(ESlateVisibility::Collapsed);
    auto LeatherArtwork=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("TabLeatherArtwork"));LeatherArtwork->bIsVariable=true;LeatherArtwork->SetVisibility(ESlateVisibility::HitTestInvisible);Place(Leather,LeatherArtwork,-65,-72,PaperWidth,PaperHeight);
    Place(Paper,Text(T,TEXT("Title"),Side ? 28 : 32,TEXT("The Imperial Ledger")),Margin,Popup ? 60 : 100,Popup ? BodyWidth : BodyWidth-100,48);
    Place(Paper,Text(T,TEXT("Subtitle"),18,TEXT("A book for every purpose")),Margin,Popup ? 84 : 153,BodyWidth,48);
    Place(Paper,Button(T,TEXT("CloseButton"),TEXT("Close")),Popup ? 280 : PaperWidth-Margin-84,Popup ? 300 : 100,Popup ? 200 : 84,38);
    auto Scroll=T->ConstructWidget<UScrollBox>(UScrollBox::StaticClass(),TEXT("BodyScroll")); Scroll->bIsVariable=true; Place(Paper,Scroll,Margin,Popup ? 125 : 212,BodyWidth,Popup ? 165 : 610);
    // Slim leather channel with a brass thumb and brighter hover/drag edges.
    FScrollBarStyle Bar;
    Bar.SetThickness(8);
    Bar.VerticalBackgroundImage=ArtBrush(TEXT("PatinatedMetal"),FVector2D(8,64));Bar.VerticalBackgroundImage.TintColor=FLinearColor(.3f,.26f,.2f,.35f);
    Bar.HorizontalBackgroundImage=Bar.VerticalBackgroundImage;
    Bar.VerticalTopSlotImage=Bar.VerticalBottomSlotImage=Bar.HorizontalTopSlotImage=Bar.HorizontalBottomSlotImage=FSlateNoResource();
    Bar.NormalThumbImage=ArtBrush(TEXT("EngravedFrame"),FVector2D(8,32),true);
    Bar.HoveredThumbImage=Bar.NormalThumbImage;Bar.HoveredThumbImage.TintColor=FLinearColor(1.2f,1.15f,1.05f,1);
    Bar.DraggedThumbImage=Bar.NormalThumbImage;Bar.DraggedThumbImage.TintColor=FLinearColor(.7f,.65f,.55f,1);
    Scroll->SetWidgetBarStyle(Bar); Scroll->SetScrollbarThickness(FVector2D(8,8)); Scroll->SetScrollbarPadding(FMargin(5,2,1,2));
    Scroll->SetAlwaysShowScrollbar(false); Scroll->SetAlwaysShowScrollbarTrack(false);
    auto ScrollStyle=Scroll->GetWidgetStyle(); ScrollStyle.TopShadowBrush=ScrollStyle.BottomShadowBrush=FSlateNoResource(); Scroll->SetWidgetStyle(ScrollStyle);
    auto BodySize=T->ConstructWidget<USizeBox>(USizeBox::StaticClass(),TEXT("BodySize")); BodySize->bIsVariable=true; BodySize->SetHeightOverride(Popup ? 165 : 610); Scroll->AddChild(BodySize);
    auto Content=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("Content")); Content->bIsVariable=true; BodySize->AddChild(Content);
    Place(Paper,Button(T,TEXT("PreviousButton"),TEXT("Previous")),Margin,846,Side ? 105 : 150,38);
    Place(Paper,Text(T,TEXT("PageLabel"),20,TEXT("1 / 1")),Side ? 205 : 310,850,Side ? 90 : 160,36);
    Place(Paper,Button(T,TEXT("NextButton"),TEXT("Next")),PaperWidth-Margin-(Side ? 105 : 150),846,Side ? 105 : 150,38);
    if(Popup){
        CastChecked<UTextBlock>(T->FindWidget(TEXT("Title")))->SetJustification(ETextJustify::Center);
        CastChecked<UTextBlock>(T->FindWidget(TEXT("CloseButtonCaption")))->SetText(FText::FromString(TEXT("Continue")));
        for(auto Name:{TEXT("PreviousButton"),TEXT("NextButton"),TEXT("PageLabel"),TEXT("Subtitle")})T->FindWidget(Name)->SetVisibility(ESlateVisibility::Collapsed);
    }
    auto Tabs=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("PageTabs"));Tabs->bIsVariable=true;Place(Paper,Tabs,Margin,190,BodyWidth,38);Tabs->SetVisibility(ESlateVisibility::Collapsed);
    // Blend the two exposed tab edges into the same underlying page sample.
    // Clipped copies keep paper grain aligned instead of adding a different patch.
    for(bool RightEdge:{false,true}){
        FString EdgeName=RightEdge?TEXT("TabRightBlend"):TEXT("TabLeftBlend");auto Edge=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),FName(*EdgeName));Edge->bIsVariable=true;Place(Paper,Edge,65,72,8,80);Edge->SetVisibility(ESlateVisibility::Collapsed);
        for(int Band=0;Band<8;Band++){
            auto Clip=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),FName(*(EdgeName+FString::FromInt(Band))));Clip->bIsVariable=true;Clip->SetClipping(EWidgetClipping::ClipToBoundsAlways);Clip->SetVisibility(ESlateVisibility::HitTestInvisible);auto S=Place(Edge,Clip,Band,0,1,0);S->SetAnchors(FAnchors(0,0,0,1));
            auto Sample=T->ConstructWidget<UImage>(UImage::StaticClass(),FName(*(EdgeName+TEXT("Artwork")+FString::FromInt(Band))));Sample->bIsVariable=true;Sample->SetBrush(SurfaceBrush);float Fade=FMath::SmoothStep(0.f,8.f,Band+.5f);Sample->SetColorAndOpacity(FLinearColor(1,1,1,RightEdge?Fade:1-Fade));Sample->SetVisibility(ESlateVisibility::HitTestInvisible);Place(Clip,Sample,15-65-Band,0,PaperWidth-30,847);
        }
    }
    IntVar(BP,TEXT("CloseSerial")); IntVar(BP,TEXT("PreviousSerial")); IntVar(BP,TEXT("NextSerial"));
    FKismetEditorUtilities::CompileBlueprint(BP);
    StringSetter(BP,TEXT("SetCloseLabel"),TEXT("CloseButtonCaption")); StringSetter(BP,TEXT("SetTitle"),TEXT("Title")); StringSetter(BP,TEXT("SetSubtitle"),TEXT("Subtitle")); StringSetter(BP,TEXT("SetPageLabel"),TEXT("PageLabel"));
    Event(BP,TEXT("CloseButton"),TEXT("OnClicked"),TEXT("CloseSerial")); Event(BP,TEXT("PreviousButton"),TEXT("OnClicked"),TEXT("PreviousSerial")); Event(BP,TEXT("NextButton"),TEXT("OnClicked"),TEXT("NextSerial"));
    InkSetter(BP,TEXT("SetTitleInk"),TEXT("Title"));InkSetter(BP,TEXT("SetSubtitleInk"),TEXT("Subtitle"));
    FontSetter(BP,TEXT("SetTitleSize"),TEXT("Title"));FontSetter(BP,TEXT("SetSubtitleSize"),TEXT("Subtitle"));ArtworkSetter(BP,TEXT("SetPaperArtwork"),TEXT("BookBackground"));ArtworkSetter(BP,TEXT("SetPageArtwork"),TEXT("PageSurface"));ArtworkSetter(BP,TEXT("SetTabLeatherArtwork"),TEXT("TabLeatherArtwork"));
    ArtworkSetter(BP,TEXT("SetTabLeftBlendArtwork"),TEXT("TabLeftBlendArtwork"),true,8);ArtworkSetter(BP,TEXT("SetTabRightBlendArtwork"),TEXT("TabRightBlendArtwork"),true,8);
    Finalize(BP);
    }
    for(bool Tip:{true,false}) {
        BP=NewBP(Tip ? TEXT("WBP_Tooltip") : TEXT("WBP_HudPanel"));T=BP->WidgetTree;
        auto Box=T->ConstructWidget<USizeBox>(USizeBox::StaticClass(),TEXT("RootSize"));Box->bIsVariable=true;Box->SetWidthOverride(320);Box->SetHeightOverride(Tip ? 140 : 160);T->RootWidget=Box;
        Root=T->ConstructWidget<UCanvasPanel>();Box->AddChild(Root);
        auto BG=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("BookBackground"));BG->bIsVariable=true;BG->SetBrush(ArtBrush(TEXT("HudPaper"),FVector2D(320,160)));auto BGS=Root->AddChildToCanvas(BG);BGS->SetAnchors(FAnchors(0,0,1,1));BGS->SetOffsets(FMargin(0));
        Place(Root,Text(T,TEXT("Title"),Tip ? 16 : 18,TEXT("Status")),22,23,276,Tip ? 95 : 27);
        auto TitleSlot=CastChecked<UCanvasPanelSlot>(T->FindWidget(TEXT("Title"))->Slot);TitleSlot->SetAnchors(FAnchors(0,0,1,Tip ? 1 : 0));TitleSlot->SetOffsets(FMargin(22,23,22,Tip ? 25 : 27));
        if(!Tip){Place(Root,Text(T,TEXT("Description"),14,TEXT("Waiting for another player")),22,51,276,58);auto DS=CastChecked<UCanvasPanelSlot>(T->FindWidget(TEXT("Description"))->Slot);DS->SetAnchors(FAnchors(0,0,1,0));DS->SetOffsets(FMargin(22,51,22,58));
            auto HudProgress=T->ConstructWidget<UProgressBar>(UProgressBar::StaticClass(),TEXT("ValueProgress"));HudProgress->bIsVariable=true;ThemeProgress(HudProgress);Place(Root,HudProgress,22,118,276,7);auto PS=CastChecked<UCanvasPanelSlot>(HudProgress->Slot);PS->SetAnchors(FAnchors(0,0,1,0));PS->SetOffsets(FMargin(22,118,22,7));
            Place(Root,Text(T,TEXT("ProgressText"),12,TEXT("0%")),22,129,276,17);
        }
        FKismetEditorUtilities::CompileBlueprint(BP);StringSetter(BP,TEXT("SetLabel"),TEXT("Title"));InkSetter(BP,TEXT("SetLabelInk"),TEXT("Title"));FontSetter(BP,TEXT("SetLabelSize"),TEXT("Title"));ArtworkSetter(BP,TEXT("SetPaperArtwork"),TEXT("BookBackground"));
        if(!Tip){StringSetter(BP,TEXT("SetDescription"),TEXT("Description"));InkSetter(BP,TEXT("SetDescriptionInk"),TEXT("Description"));StringSetter(BP,TEXT("SetSliderCurrent"),TEXT("ProgressText"));FontSetter(BP,TEXT("SetDescriptionSize"),TEXT("Description"));}
        Finalize(BP);
    }
    UE_LOG(LogTemp,Display,TEXT("BOOK_UI_BUILT: center, left, right books and interactive control widgets.")); return 0;
}

int32 PreviewBookUI()
{
    using namespace BookBuild;
    if(!FSlateApplication::IsInitialized()) FSlateApplication::InitializeAsStandaloneApplication(FModuleManager::LoadModuleChecked<ISlateRHIRendererModule>(TEXT("SlateRHIRenderer")).CreateSlateRHIRenderer());
    auto Make=[](const TCHAR* Path) { auto C=LoadClass<UUserWidget>(nullptr,Path); check(C); auto W=NewObject<UUserWidget>(GetTransientPackage(),C); W->AddToRoot(); check(W->Initialize()); return W; };
    auto Set=[](UUserWidget* W,FName Fn,const TCHAR* Value) { struct { FString Text; } A{Value}; W->ProcessEvent(W->FindFunctionChecked(Fn),&A); };
    for(int Placement=0;Placement<4;++Placement) {
    const bool Side=Placement==1 || Placement==2;
    const bool Popup=Placement==3;
    FString Asset=Placement==1 ? TEXT("WBP_BookLeft") : Placement==2 ? TEXT("WBP_BookRight") : Placement==3 ? TEXT("WBP_BookPopup") : TEXT("WBP_Book");
    FString Suffix=Placement==1 ? TEXT("Left") : Placement==2 ? TEXT("Right") : Placement==3 ? TEXT("Popup") : TEXT("");
    auto Book=Make(*(TEXT("/Game/OblivionUI/Book/")+Asset+TEXT(".")+Asset+TEXT("_C")));
    Set(Book,TEXT("SetTitle"),TEXT("The Arena Ledger")); Set(Book,TEXT("SetSubtitle"),TEXT("Prepare for your next challenge")); Set(Book,TEXT("SetPageLabel"),TEXT("1 / 3"));
    auto Content=CastChecked<UCanvasPanel>(Book->WidgetTree->FindWidget(TEXT("Content")));
    const TCHAR* Labels[]={TEXT("Enter the arena"),TEXT("Match difficulty"),TEXT("Wager amount"),TEXT("Match announcements")};
    const TCHAR* Details[]={TEXT("Speak to the gatekeeper when you are ready."),TEXT("Choose the challenge that suits your champion."),TEXT("Set how much gold you wish to wager."),TEXT("Receive a notice before each match begins.")};
    const TCHAR* Controls[]={TEXT("ActionButton"),TEXT("Dropdown"),TEXT("ValueSlider"),TEXT("Toggle")};
    TArray<UUserWidget*> Rows;
    for(int I=0;I<(Popup ? 0 : 4);++I) {
        auto Row=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C")); Rows.Add(Row);
        Set(Row,TEXT("SetLabel"),Labels[I]); Set(Row,TEXT("SetDescription"),Details[I]); Set(Row,TEXT("SetButtonLabel"),TEXT("Join the next match"));
        auto Control=Row->WidgetTree->FindWidget(Controls[I]); Control->SetVisibility(ESlateVisibility::Visible);
        if(auto C=Cast<UComboBoxString>(Control)) { C->AddOption(TEXT("Novice")); C->AddOption(TEXT("Gladiator")); C->AddOption(TEXT("Champion")); C->SetSelectedIndex(1); }
        if(auto S=Cast<USlider>(Control)) {
            S->SetMinValue(-24); S->SetMaxValue(24); S->SetValue(6);
            Set(Row,TEXT("SetLabel"),TEXT("Microphone gain")); Set(Row,TEXT("SetDescription"),TEXT("Adjust your microphone level in dB."));
            const TCHAR* Names[]={TEXT("SliderMinimum"),TEXT("SliderMiddle"),TEXT("SliderMaximum"),TEXT("SliderCurrent")};
            const TCHAR* Values[]={TEXT("-24"),TEXT("0"),TEXT("+24"),TEXT("+6")};
            for(int J=0;J<4;++J) { Row->WidgetTree->FindWidget(Names[J])->SetVisibility(ESlateVisibility::HitTestInvisible); Set(Row,FName(*(FString(TEXT("Set"))+Names[J])),Values[J]);
                check(CastChecked<UTextBlock>(Row->WidgetTree->FindWidget(Names[J]))->GetText().ToString()==Values[J]); }
        }
        if(auto C=Cast<UCheckBox>(Control)) C->SetIsChecked(true);
        Place(Content,Row,0,I*144,Side ? 396 : 610,128);
        auto Serial=FindFProperty<FIntProperty>(Row->GetClass(),TEXT("EventSerial")); check(Serial);
        int Before=Serial->GetPropertyValue_InContainer(Row);
        CastChecked<UButton>(Row->WidgetTree->FindWidget(TEXT("ActionButton")))->OnClicked.Broadcast();
        check(Serial->GetPropertyValue_InContainer(Row)==Before+1);
    }
    // Exercise real image loading through the generated Blueprint function.
    auto Picture=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));Rows.Add(Picture);
    for(auto Name:{TEXT("Label"),TEXT("Description")})Picture->WidgetTree->FindWidget(Name)->SetVisibility(ESlateVisibility::Collapsed);
    Picture->WidgetTree->FindWidget(TEXT("ArtworkScale"))->SetVisibility(ESlateVisibility::Visible);
    Set(Picture,TEXT("SetArtwork"),TEXT("/Game/OblivionUI/Book/T_Book.T_Book"));
    auto PictureImage=CastChecked<UImage>(Picture->WidgetTree->FindWidget(TEXT("Artwork")));
    check(PictureImage->GetBrush().GetResourceObject()==LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/Book/T_Book.T_Book")));
    PictureImage->SetDesiredSizeOverride(FVector2D(128.f*760/940,128));
    Place(Content,Picture,0,Popup ? 0 : 620,Popup ? 80 : 120,Popup ? 80 : 160);
    UUserWidget* CanvasPreview=nullptr;
    if(Popup) {
        Set(Book,TEXT("SetTitle"),TEXT("Quest Updated"));Set(Book,TEXT("SetCloseLabel"),TEXT("Continue"));
        auto Heading=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));Rows.Add(Heading);
        Set(Heading,TEXT("SetLabel"),TEXT("A Champion in the Making"));Set(Heading,TEXT("SetDescription"),TEXT("The Imperial City Arena"));Place(Content,Heading,100,0,510,76);
        auto Message=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));Rows.Add(Message);
        Set(Message,TEXT("SetLabel"),TEXT(""));Set(Message,TEXT("SetDescription"),TEXT("Your next challenge awaits. Return to the gatekeeper when you are ready."));Place(Content,Message,0,72,610,82);
    } else {
        auto Canvas=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));CanvasPreview=Canvas;Rows.Add(Canvas);
        for(auto Name:{TEXT("Label"),TEXT("Description")})Canvas->WidgetTree->FindWidget(Name)->SetVisibility(ESlateVisibility::Collapsed);
        auto Art=CastChecked<UCanvasPanel>(Canvas->WidgetTree->FindWidget(TEXT("ArtCanvas")));Art->SetVisibility(ESlateVisibility::Visible);Place(Content,Canvas,140,620,Side ? 250 : 460,180);
        auto Stroke=[&](float X,float Y,float W,float H,float Angle,FLinearColor Color){auto R=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));Rows.Add(R);for(auto Name:{TEXT("Label"),TEXT("Description")})R->WidgetTree->FindWidget(Name)->SetVisibility(ESlateVisibility::Collapsed);auto Ink=CastChecked<UImage>(R->WidgetTree->FindWidget(TEXT("Ink")));Ink->SetVisibility(ESlateVisibility::Visible);Ink->SetColorAndOpacity(Color);Place(Art,R,X,Y,W,H);R->SetRenderTransformPivot(FVector2D(0,0));R->SetRenderTransformAngle(Angle);};
        Stroke(0,0,240,180,0,FLinearColor(.3f,.18f,.07f,.15f));
        Stroke(10,140,100,4,-45,FLinearColor(.18f,.1f,.04f,1));Stroke(80,70,90,4,30,FLinearColor(.18f,.1f,.04f,1));Stroke(158,115,95,4,-50,FLinearColor(.18f,.1f,.04f,1));
    }
    auto Close=FindFProperty<FIntProperty>(Book->GetClass(),TEXT("CloseSerial")); check(Close);
    CastChecked<UButton>(Book->WidgetTree->FindWidget(TEXT("CloseButton")))->OnClicked.Broadcast(); check(Close->GetPropertyValue_InContainer(Book)==1);
    FTextureCompilingManager::Get().FinishAllCompilation();GShaderCompilingManager->FinishAllCompilation();FlushRenderingCommands();
    auto Background=CastChecked<UImage>(Book->WidgetTree->FindWidget(TEXT("BookBackground")));
    auto BT=CastChecked<UTexture2D>(Background->GetBrush().GetResourceObject());
    UE_LOG(LogTemp,Display,TEXT("Book texture %s %d x %d source=%d resource=%d"),*BT->GetPathName(),BT->GetSizeX(),BT->GetSizeY(),BT->Source.IsValid(),BT->GetResource()!=nullptr);
    BT->UpdateResource(); FlushRenderingCommands();
    FWidgetRenderer Renderer(false); auto RT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000)); check(RT);
    FBufferArchive PNG; check(FImageUtils::ExportRenderTarget2DAsPNG(RT,PNG)); check(FFileHelper::SaveArrayToFile(PNG,*(FPaths::ProjectDir()/(TEXT("Output/Book")+Suffix+TEXT("Preview.png")))));
    // Exercise overflow as well as the short-page case; this is preview-only.
    CastChecked<USizeBox>(Book->WidgetTree->FindWidget(TEXT("BodySize")))->SetHeightOverride(Popup ? 165 : 820);
    Book->ForceLayoutPrepass();
    auto PreviewScroll=CastChecked<UScrollBox>(Book->WidgetTree->FindWidget(TEXT("BodyScroll")));
    PreviewScroll->SetAlwaysShowScrollbar(true); PreviewScroll->SetAlwaysShowScrollbarTrack(true);
    auto ScrollRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000)); check(ScrollRT);
    FSlateApplication::Get().Tick();
    ScrollRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));
    FBufferArchive ScrollPNG; check(FImageUtils::ExportRenderTarget2DAsPNG(ScrollRT,ScrollPNG));
    check(FFileHelper::SaveArrayToFile(ScrollPNG,*(FPaths::ProjectDir()/(TEXT("Output/Book")+Suffix+TEXT("ScrollPreview.png")))));
    if(!Popup){
        Content->ClearChildren();Place(Content,Picture,0,0,120,160);Place(Content,CanvasPreview,140,0,Side ? 250 : 460,180);
        Set(Book,TEXT("SetTitle"),TEXT("Artwork canvas"));Set(Book,TEXT("SetSubtitle"),TEXT("Images, filled shapes and freeform paths"));
        CastChecked<USizeBox>(Book->WidgetTree->FindWidget(TEXT("BodySize")))->SetHeightOverride(610);
        PreviewScroll->ScrollToStart();Book->ForceLayoutPrepass();FSlateApplication::Get().Tick();
        auto ArtRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));FSlateApplication::Get().Tick();ArtRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));
        FBufferArchive ArtPNG;check(FImageUtils::ExportRenderTarget2DAsPNG(ArtRT,ArtPNG));check(FFileHelper::SaveArrayToFile(ArtPNG,*(FPaths::ProjectDir()/(TEXT("Output/Book")+Suffix+TEXT("ArtworkPreview.png")))));
    }
    if(!Popup){
        const float Margin=Side?42.f:65.f,BodyWidth=Side?416.f:630.f,PaperWidth=BodyWidth+2*Margin;
        const float TabWidth=FMath::Min(80.f*(1024.f*.168f)/(256.f*.44f),(PaperWidth-130)/5),TabHeight=TabWidth*(256.f*.44f)/(1024.f*.168f),TabScale=TabHeight/80,TabX=65-Margin;
        Content->ClearChildren();Set(Book,TEXT("SetTitle"),TEXT("Arena outfitters"));Set(Book,TEXT("SetSubtitle"),TEXT("Armour / Weapons / Spells    |    240 XP available"));
        auto TabPanel=CastChecked<UCanvasPanel>(Book->WidgetTree->FindWidget(TEXT("PageTabs")));TabPanel->SetVisibility(ESlateVisibility::Visible);
        auto Relocate=[&](const TCHAR* Name,float X,float Y,float W,float H){auto Slot=CastChecked<UCanvasPanelSlot>(Book->WidgetTree->FindWidget(Name)->Slot);Slot->SetPosition(FVector2D(X,Y));Slot->SetSize(FVector2D(W,H));};
        Set(Book,TEXT("SetTitle"),TEXT("Weapons"));Set(Book,TEXT("SetSubtitle"),TEXT("Arena outfitters | 240 XP available"));
        auto NativeTexture=[&](const TCHAR* Name){auto Texture=FImageUtils::ImportFileAsTexture2D(FPaths::ProjectDir()/TEXT("Inspection/NativeTabs/Preview/")+Name+TEXT(".png"));check(Texture);return Texture;};
        auto NativeBackground=NativeTexture(TEXT("T_UI_PlayerMenu_Background"));auto NativeTab=NativeTexture(TEXT("T_Tab_1"));
        CastChecked<UImage>(Book->WidgetTree->FindWidget(TEXT("BookBackground")))->SetBrushFromTexture(NativeBackground);
        Set(Book,TEXT("SetPageArtwork"),TEXT("/Game/OblivionUI/Book/T_Book.T_Book"));auto Surface=CastChecked<UImage>(Book->WidgetTree->FindWidget(TEXT("PageSurface")));Surface->SetBrushResourceObject(NativeBackground);Surface->SetVisibility(ESlateVisibility::HitTestInvisible);
        Set(Book,TEXT("SetTabLeatherArtwork"),TEXT("/Game/OblivionUI/Book/T_Book.T_Book"));auto LeatherArt=CastChecked<UImage>(Book->WidgetTree->FindWidget(TEXT("TabLeatherArtwork")));check(LeatherArt->GetBrush().GetResourceObject()==LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/Book/T_Book.T_Book")));LeatherArt->SetBrushResourceObject(NativeBackground);
        const TCHAR* Icons[]={TEXT("Weapons"),TEXT("Armor"),TEXT("Alchemy"),TEXT("All_items"),TEXT("Misc")};
        auto PreviewTabs=[&](int Count,int Selected){
        TabPanel->ClearChildren();bool Show=Count>1;TabPanel->SetVisibility(Show?ESlateVisibility::Visible:ESlateVisibility::Collapsed);
        Relocate(TEXT("PageTabs"),Margin,72,BodyWidth,TabHeight);Relocate(TEXT("Title"),Margin,Show?100+TabHeight:100,BodyWidth-100,48);Relocate(TEXT("CloseButton"),Margin+BodyWidth-84,Show?100+TabHeight:100,84,38);Relocate(TEXT("Subtitle"),Margin,Show?146+TabHeight:153,BodyWidth,42);float BodyY=Show?198+TabHeight:212;Relocate(TEXT("BodyScroll"),Margin,BodyY,BodyWidth,822-BodyY);
        Book->WidgetTree->FindWidget(TEXT("TabLeather"))->SetVisibility(ESlateVisibility::Collapsed);
        for(bool RightEdge:{false,true}){
            FString EdgeName=RightEdge?TEXT("TabRightBlend"):TEXT("TabLeftBlend");auto Edge=CastChecked<UCanvasPanel>(Book->WidgetTree->FindWidget(FName(*EdgeName)));Edge->SetVisibility(Show?ESlateVisibility::HitTestInvisible:ESlateVisibility::Collapsed);check(Edge->GetChildrenCount()==8);if(!Show)continue;
            float EdgeX=Margin+TabX+(RightEdge?Count*TabWidth-8*TabScale:0);Relocate(*EdgeName,EdgeX,72,8*TabScale,TabHeight);Set(Book,FName(*(TEXT("Set")+EdgeName+TEXT("Artwork"))),TEXT("/Game/OblivionUI/Book/T_Book.T_Book"));
            for(int Band=0;Band<8;Band++){
                auto Clip=Book->WidgetTree->FindWidget(FName(*(EdgeName+FString::FromInt(Band))));check(Clip->GetClipping()==EWidgetClipping::ClipToBoundsAlways);CastChecked<UCanvasPanelSlot>(Clip->Slot)->SetOffsets(FMargin(Band*TabScale,0,TabScale,0));FString ArtName=EdgeName+TEXT("Artwork")+FString::FromInt(Band);auto Sample=CastChecked<UImage>(Book->WidgetTree->FindWidget(FName(*ArtName)));check(Sample->GetBrush().GetResourceObject()==LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/Book/T_Book.T_Book")));Sample->SetBrushResourceObject(NativeBackground);Relocate(*ArtName,15-EdgeX-Band*TabScale,0,PaperWidth-30,847);
            }
        }
        Set(Book,TEXT("SetTitle"),Selected==1?TEXT("Armour"):TEXT("Weapons"));
        for(auto Name:{TEXT("PreviousButton"),TEXT("NextButton"),TEXT("PageLabel")})Book->WidgetTree->FindWidget(Name)->SetVisibility(Show?ESlateVisibility::Visible:ESlateVisibility::Collapsed);
        Set(Book,TEXT("SetPageLabel"),*FString::Printf(TEXT("%d / %d"),Selected+1,Count));if(!Show){check(TabPanel->GetChildrenCount()==0);return;}
        for(int J=0;J<Count+2;J++){
            auto R=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));Rows.Add(R);
            bool Arrow=J==0||J==Count+1;float Width=Arrow?28:TabWidth/TabScale;float X=J==0?TabX-40*TabScale:J==Count+1?TabX+Count*TabWidth+12*TabScale:TabX+(J-1)*TabWidth;
            for(auto Name:{TEXT("Label"),TEXT("Description")})R->WidgetTree->FindWidget(Name)->SetVisibility(ESlateVisibility::Collapsed);
            auto A=CastChecked<UButton>(R->WidgetTree->FindWidget(TEXT("ActionButton")));A->SetVisibility(ESlateVisibility::Visible);A->SetBackgroundColor(FLinearColor(1,1,1,0));Set(R,TEXT("SetButtonLabel"),Arrow?(J==0?TEXT("A"):TEXT("D")):TEXT(""));CastChecked<UCanvasPanelSlot>(A->Slot)->SetOffsets(FMargin(0,0,0,80));
            if(!Arrow){
                Set(R,J-1==Selected?TEXT("SetTabActivePaper"):TEXT("SetTabInactivePaper"),TEXT("/Game/OblivionUI/Book/T_Book.T_Book"));
                auto Paper=CastChecked<UCanvasPanel>(R->WidgetTree->FindWidget(J-1==Selected?TEXT("TabActivePaper"):TEXT("TabInactivePaper")));check(Paper->GetChildrenCount()==17);for(auto Child:Paper->GetAllChildren()){auto Slice=CastChecked<UImage>(Child);check(Slice->GetBrush().GetResourceObject()==LoadObject<UTexture2D>(nullptr,TEXT("/Game/OblivionUI/Book/T_Book.T_Book")));Slice->SetBrushResourceObject(NativeTab);}Paper->SetVisibility(ESlateVisibility::HitTestInvisible);
                auto Scale=R->WidgetTree->FindWidget(TEXT("ArtworkScale"));Scale->SetVisibility(ESlateVisibility::HitTestInvisible);CastChecked<UCanvasPanelSlot>(Scale->Slot)->SetOffsets(FMargin((Width-54)/2,7,(Width-54)/2,18));
                CastChecked<UImage>(R->WidgetTree->FindWidget(TEXT("Artwork")))->SetBrushFromTexture(NativeTexture(*(FString(TEXT("T_Category_"))+Icons[J-1])));
            }
            Place(TabPanel,R,X,0,Width,80);R->SetRenderTransformPivot(FVector2D(0,0));R->SetRenderScale(FVector2D(TabScale,TabScale));
        }
        check(TabPanel->GetChildrenCount()==Count+2);
        };
        PreviewTabs(3,0);
        const TCHAR* Names[]={TEXT("Iron shortsword"),TEXT("Steel longsword"),TEXT("Silver dagger"),TEXT("Fur armour"),TEXT("Iron cuirass"),TEXT("Healing tome")};
        const TCHAR* Prices[]={TEXT("Free starter gear"),TEXT("120 XP"),TEXT("180 XP"),TEXT("Recovery available"),TEXT("160 XP"),TEXT("80 XP")};
        for(int I=0;I<6;I++){
            auto R=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C"));Rows.Add(R);Set(R,TEXT("SetLabel"),Names[I]);Set(R,TEXT("SetDescription"),Prices[I]);Set(R,TEXT("SetButtonLabel"),TEXT(""));R->WidgetTree->FindWidget(TEXT("CardPaper"))->SetVisibility(ESlateVisibility::HitTestInvisible);
            auto Action=R->WidgetTree->FindWidget(TEXT("ActionButton"));CastChecked<UButton>(Action)->SetBackgroundColor(FLinearColor(1,1,1,0));Action->SetVisibility(ESlateVisibility::Visible);CastChecked<UCanvasPanelSlot>(Action->Slot)->SetOffsets(FMargin(0,0,0,194));
            CastChecked<UCanvasPanelSlot>(R->WidgetTree->FindWidget(TEXT("Label"))->Slot)->SetOffsets(FMargin(16,20,16,90));
            CastChecked<UCanvasPanelSlot>(R->WidgetTree->FindWidget(TEXT("Description"))->Slot)->SetOffsets(FMargin(16,116,16,60));
            Place(Content,R,(I%(Side?2:3))*206,(I/(Side?2:3))*210,Side?190:194,194);
            check(CastChecked<UButton>(Action)->GetStyle().Normal.GetResourceObject()==Art(TEXT("EngravedFrame")));
        }
        PreviewScroll->ScrollToStart();Book->ForceLayoutPrepass();FSlateApplication::Get().Tick();auto ShopRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));FSlateApplication::Get().Tick();ShopRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));
        FBufferArchive ShopPNG;check(FImageUtils::ExportRenderTarget2DAsPNG(ShopRT,ShopPNG));check(FFileHelper::SaveArrayToFile(ShopPNG,*(FPaths::ProjectDir()/(TEXT("Output/ImperialShop")+Suffix+TEXT("Preview.png")))));
        for(int Count:{1,2,3,5}){
            PreviewTabs(Count,Count==2?1:0);Book->ForceLayoutPrepass();FSlateApplication::Get().Tick();auto TabsRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));FSlateApplication::Get().Tick();TabsRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));FBufferArchive TabsPNG;check(FImageUtils::ExportRenderTarget2DAsPNG(TabsRT,TabsPNG));check(FFileHelper::SaveArrayToFile(TabsPNG,*(FPaths::ProjectDir()/FString::Printf(TEXT("Output/BookTabs%d%sPreview.png"),Count,*Suffix))));
        }
    }
    for(auto Row:Rows) Row->RemoveFromRoot(); Book->RemoveFromRoot();
    }
    // Execute generated theme functions and render the reusable small parchment widgets.
    for(bool Tip:{true,false}) {
        auto W=Make(Tip ? TEXT("/Game/OblivionUI/Book/WBP_Tooltip.WBP_Tooltip_C") : TEXT("/Game/OblivionUI/Book/WBP_HudPanel.WBP_HudPanel_C"));
        Set(W,TEXT("SetLabel"),Tip ? TEXT("Iron sword\nA dependable arena weapon.") : TEXT("Arena match"));
        struct {int32 Size;} FontArgs{Tip?16:18}; W->ProcessEvent(W->FindFunctionChecked(TEXT("SetLabelSize")),&FontArgs);
        check(CastChecked<UTextBlock>(W->WidgetTree->FindWidget(TEXT("Title")))->GetFont().Size==(Tip?16:18));
        struct {FLinearColor Color;} InkArgs{FLinearColor(.16f,.10f,.055f,1.f)};
        W->ProcessEvent(W->FindFunctionChecked(TEXT("SetLabelInk")),&InkArgs);
        check(CastChecked<UTextBlock>(W->WidgetTree->FindWidget(TEXT("Title")))->GetColorAndOpacity().GetSpecifiedColor().Equals(InkArgs.Color));
        if(!Tip){Set(W,TEXT("SetDescription"),TEXT("Waiting for another player\n2 / 4 champions ready"));Set(W,TEXT("SetSliderCurrent"),TEXT("50%"));auto P=CastChecked<UProgressBar>(W->WidgetTree->FindWidget(TEXT("ValueProgress")));P->SetPercent(.5f);check(P->GetPercent()==.5f);}
        W->ForceLayoutPrepass();FSlateApplication::Get().Tick();FWidgetRenderer Renderer(false);
        auto RT=Renderer.DrawWidget(W->TakeWidget(),FVector2D(320,Tip ? 140 : 160));FSlateApplication::Get().Tick();RT=Renderer.DrawWidget(W->TakeWidget(),FVector2D(320,Tip ? 140 : 160));
        FBufferArchive PNG;check(FImageUtils::ExportRenderTarget2DAsPNG(RT,PNG));check(FFileHelper::SaveArrayToFile(PNG,*(FPaths::ProjectDir()/(Tip ? TEXT("Output/ToolkitTooltipPreview.png") : TEXT("Output/ToolkitHudPreview.png")))));W->RemoveFromRoot();
    }
    UE_LOG(LogTemp,Display,TEXT("BOOK_PREVIEW_VALIDATED: books and popup rendered; image loading, button and close events passed.")); return 0;
}

int32 BookMetadata()
{
    FString Data;
    auto Add=[&](UClass* C,std::initializer_list<const TCHAR*> Names) {
        for(auto Name:Names) {
            auto F=C->FindFunctionByName(Name); checkf(F,TEXT("Missing metadata function %s"),Name);
            Data+=FString::Printf(TEXT("%s|%d"),Name,F->ParmsSize);
            for(TFieldIterator<FProperty> P(F);P;++P) if(P->HasAnyPropertyFlags(CPF_Parm)) Data+=FString::Printf(TEXT("|%s:%d:%d"),*P->GetName(),P->GetOffset_ForInternal(),P->GetSize());
            Data+=TEXT("\n");
        }
    };
    Add(UWidgetBlueprintLibrary::StaticClass(),{TEXT("Create"),TEXT("SetInputMode_UIOnlyEx"),TEXT("SetInputMode_GameOnly")});
    Add(APlayerController::StaticClass(),{TEXT("SetIgnoreLookInput"),TEXT("SetIgnoreMoveInput")});
    Add(UUserWidget::StaticClass(),{TEXT("AddToViewport"),TEXT("RemoveFromParent"),TEXT("GetOwningPlayer"),TEXT("SetDesiredSizeInViewport"),TEXT("SetPositionInViewport"),TEXT("SetKeyboardFocus"),TEXT("SetVisibility"),TEXT("SetIsEnabled")});
    Add(UImage::StaticClass(),{TEXT("SetDesiredSizeOverride"),TEXT("SetColorAndOpacity")});
    Add(UButton::StaticClass(),{TEXT("SetBackgroundColor")});
    Add(UProgressBar::StaticClass(),{TEXT("SetPercent"),TEXT("SetFillColorAndOpacity")});
    Add(USlateBlueprintLibrary::StaticClass(),{TEXT("AbsoluteToLocal"),TEXT("GetLocalSize")});
    Add(UWidget::StaticClass(),{TEXT("SetRenderTransformAngle"),TEXT("SetRenderTransformPivot"),TEXT("GetCachedGeometry"),TEXT("HasKeyboardFocus"),TEXT("HasFocusedDescendants"),TEXT("IsHovered"),TEXT("SetToolTip"),TEXT("SetRenderOpacity"),TEXT("SetRenderScale")});
    Add(UCanvasPanel::StaticClass(),{TEXT("ClearChildren"),TEXT("AddChildToCanvas")});
    Add(UCanvasPanelSlot::StaticClass(),{TEXT("SetPosition"),TEXT("SetSize"),TEXT("SetOffsets")});
    Add(USizeBox::StaticClass(),{TEXT("SetHeightOverride"),TEXT("SetWidthOverride")});
    Add(USlider::StaticClass(),{TEXT("SetMinValue"),TEXT("SetMaxValue"),TEXT("SetStepSize"),TEXT("SetValue"),TEXT("GetValue")});
    Add(UCheckBox::StaticClass(),{TEXT("SetIsChecked"),TEXT("IsChecked")});
    Add(UComboBoxString::StaticClass(),{TEXT("ClearOptions"),TEXT("AddOption"),TEXT("SetSelectedIndex"),TEXT("GetSelectedIndex")});
    Add(UScrollBox::StaticClass(),{TEXT("ScrollToStart")});
    Add(UKismetStringLibrary::StaticClass(),{TEXT("Conv_StringToName")});
    Add(USceneComponent::StaticClass(),{TEXT("GetSocketLocation")});
    Add(AActor::StaticClass(),{TEXT("GetActorBounds")});
    Add(APlayerCameraManager::StaticClass(),{TEXT("GetCameraLocation")});
    Add(UKismetSystemLibrary::StaticClass(),{TEXT("LineTraceSingle"),TEXT("IsValid")});
    Add(UWidgetLayoutLibrary::StaticClass(),{TEXT("ProjectWorldLocationToWidgetPosition"),TEXT("GetViewportSize"),TEXT("GetViewportScale"),TEXT("GetMousePositionOnPlatform")});
    check(FFileHelper::SaveStringToFile(Data,*(FPaths::ProjectDir()/TEXT("OblivionUI.Client/BookNative.tsv")),FFileHelper::EEncodingOptions::ForceUTF8WithoutBOM));
    return 0;
}




int32 BuildOrnateBar()
{
    using namespace BookBuild;
    auto Imported=FImageUtils::ImportFileAsTexture2D(FPaths::ProjectDir()/TEXT("Art/Imperial/OrnateBrassBar.png"));check(Imported);
    auto A=DuplicateObject<UTexture2D>(Imported,CreatePackage(TEXT("/Game/OblivionUI/Book/T_OrnateBrassBar")),TEXT("T_OrnateBrassBar"));
    auto& Mip=Imported->GetPlatformData()->Mips[0];auto Pixels=Mip.BulkData.LockReadOnly();
    A->Source.Init(Imported->GetSizeX(),Imported->GetSizeY(),1,1,TSF_BGRA8,static_cast<const uint8*>(Pixels));Mip.BulkData.Unlock();
    A->ClearFlags(RF_Transient);A->SetFlags(RF_Public|RF_Standalone);A->LODGroup=TEXTUREGROUP_UI;A->CompressionSettings=TC_EditorIcon;A->MipGenSettings=TMGS_NoMipmaps;A->UpdateResource();Save(A);
    auto BP=LoadObject<UWidgetBlueprint>(nullptr,TEXT("/Game/OblivionUI/Book/WBP_HudPanel.WBP_HudPanel"));check(BP);
    auto T=BP->WidgetTree;auto Box=CastChecked<USizeBox>(T->RootWidget);auto Root=CastChecked<UCanvasPanel>(Box->GetChildAt(0));
    auto Bar=T->ConstructWidget<UProgressBar>(UProgressBar::StaticClass(),TEXT("SolidProgress"));Bar->bIsVariable=true;
    auto Style=Bar->GetWidgetStyle();Style.BackgroundImage=FSlateColorBrush(FLinearColor(.035f,.026f,.016f,1));Style.FillImage=FSlateColorBrush(FLinearColor::White);Bar->SetWidgetStyle(Style);Bar->SetFillColorAndOpacity(FLinearColor(.66f,.38f,.10f,1));
    auto BS=Root->AddChildToCanvas(Bar);BS->SetAnchors(FAnchors(0,0,1,0));BS->SetOffsets(FMargin(38,14,38,12));BS->SetZOrder(5);Bar->SetVisibility(ESlateVisibility::Collapsed);
    auto Frame=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("OrnateFrame"));Frame->bIsVariable=true;
    FSlateBrush Brush;Brush.SetResourceObject(A);Brush.DrawAs=ESlateBrushDrawType::Image;Brush.ImageSize=FVector2D(360,40);Brush.SetUVRegion(FBox2f(FVector2f(0,.34f),FVector2f(1,.64f)));Frame->SetBrush(Brush);
    auto FS=Root->AddChildToCanvas(Frame);FS->SetAnchors(FAnchors(0,0,1,1));FS->SetOffsets(FMargin(0));FS->SetZOrder(6);Frame->SetVisibility(ESlateVisibility::Collapsed);
    Finalize(BP);
    UE_LOG(LogTemp,Display,TEXT("ORNATE_BAR_BUILT"));return 0;
}
