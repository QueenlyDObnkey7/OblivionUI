#include "WidgetBlueprint.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintGeneratedClass.h"
#include "Blueprint/WidgetBlueprintLibrary.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/SizeBox.h"
#include "Components/ScaleBox.h"
#include "Components/Border.h"
#include "Components/Image.h"
#include "Components/TextBlock.h"
#include "Components/Button.h"
#include "Components/Slider.h"
#include "Components/CheckBox.h"
#include "Components/ComboBoxString.h"
#include "Components/EditableTextBox.h"
#include "Components/ScrollBox.h"
#include "Engine/Texture2D.h"
#include "Engine/Font.h"
#include "Kismet/KismetTextLibrary.h"
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
#include "GameFramework/PlayerController.h"
#include "Brushes/SlateRoundedBoxBrush.h"
#include "Brushes/SlateNoResource.h"

namespace BookBuild
{
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
    for(auto Brush : {&Style.Normal,&Style.Hovered,&Style.Pressed}) { Brush->DrawAs=ESlateBrushDrawType::RoundedBox; Brush->OutlineSettings.CornerRadii=FVector4(2,2,2,2); }
    Style.Normal.TintColor=FSlateColor(FLinearColor(.16f,.10f,.05f,.16f));
    Style.Hovered.TintColor=FSlateColor(FLinearColor(.28f,.16f,.07f,.32f)); Style.Pressed.TintColor=FSlateColor(FLinearColor(.25f,.12f,.04f,.48f));
    W->SetStyle(Style); auto L=Text(T,FName(*(Name.ToString()+TEXT("Caption"))),21,Label); L->SetAutoWrapText(false); L->SetJustification(ETextJustify::Center); W->AddChild(L); return W;
}
static void IntVar(UWidgetBlueprint* BP,FName Name) { FEdGraphPinType T; T.PinCategory=UEdGraphSchema_K2::PC_Int; FBlueprintEditorUtils::AddMemberVariable(BP,Name,T,TEXT("0")); }
static void Finalize(UWidgetBlueprint* BP) { FBlueprintEditorUtils::MarkBlueprintAsStructurallyModified(BP); FKismetEditorUtilities::CompileBlueprint(BP); check(BP->Status!=BS_Error); Save(BP); }
}

int32 BuildBookUI()
{
    using namespace BookBuild;
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
    auto Toggle=T->ConstructWidget<UCheckBox>(UCheckBox::StaticClass(),TEXT("Toggle")); Toggle->bIsVariable=true; Place(Root,Toggle,0,80,600,36);
    auto Combo=T->ConstructWidget<UComboBoxString>(UComboBoxString::StaticClass(),TEXT("Dropdown")); Combo->bIsVariable=true; Place(Root,Combo,0,80,600,38);
    auto Input=T->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass(),TEXT("TextInput")); Input->bIsVariable=true; Place(Root,Input,0,80,600,38);
    auto ComboStyle=Combo->GetWidgetStyle(); ComboStyle.ComboButtonStyle.ButtonStyle=Btn->GetStyle();
    ComboStyle.ComboButtonStyle.MenuBorderBrush=Btn->GetStyle().Normal; ComboStyle.ComboButtonStyle.MenuBorderBrush.TintColor=FSlateColor(FLinearColor(.65f,.57f,.43f,1)); Combo->SetWidgetStyle(ComboStyle);
    auto ItemStyle=Combo->GetItemStyle(); ItemStyle.ActiveBrush=Btn->GetStyle().Pressed; ItemStyle.ActiveHoveredBrush=Btn->GetStyle().Hovered;
    ItemStyle.InactiveBrush=Btn->GetStyle().Normal; ItemStyle.InactiveHoveredBrush=Btn->GetStyle().Hovered; ItemStyle.SelectorFocusedBrush=Btn->GetStyle().Hovered;
    ItemStyle.TextColor=FSlateColor(FLinearColor(.09f,.055f,.03f,1)); ItemStyle.SelectedTextColor=ItemStyle.TextColor; Combo->SetItemStyle(ItemStyle);
    auto CheckStyle=Toggle->GetWidgetStyle();
    for(auto Brush : {&CheckStyle.UncheckedImage,&CheckStyle.UncheckedHoveredImage,&CheckStyle.UncheckedPressedImage,&CheckStyle.CheckedImage,&CheckStyle.CheckedHoveredImage,&CheckStyle.CheckedPressedImage}) Brush->TintColor=FSlateColor(FLinearColor(.3f,.18f,.07f,1));
    Toggle->SetWidgetStyle(CheckStyle);
    Combo->Font=FSlateFontInfo(LoadObject<UFont>(nullptr,TEXT("/Game/OblivionUI/Fonts/Petrock.Petrock")),20); Combo->ForegroundColor=FSlateColor(FLinearColor(.09f,.055f,.03f,1));
    auto InputStyle=Input->WidgetStyle; InputStyle.TextStyle.SetFont(Combo->GetFont()); InputStyle.ForegroundColor=Combo->ForegroundColor;
    InputStyle.BackgroundImageNormal=Btn->GetStyle().Normal; InputStyle.BackgroundImageHovered=Btn->GetStyle().Hovered; InputStyle.BackgroundImageFocused=Btn->GetStyle().Pressed; Input->WidgetStyle=InputStyle;
    for(auto W : TArray<UWidget*>{Btn,Slider,Toggle,Combo,Input}) {
        W->SetVisibility(ESlateVisibility::Collapsed); auto S=CastChecked<UCanvasPanelSlot>(W->Slot); S->SetAnchors(FAnchors(0,0,1,0)); S->SetOffsets(FMargin(0,80,0,38));
    }
    IntVar(BP,TEXT("EventSerial")); FEdGraphPinType String; String.PinCategory=UEdGraphSchema_K2::PC_String; FBlueprintEditorUtils::AddMemberVariable(BP,TEXT("InputValue"),String);
    FKismetEditorUtilities::CompileBlueprint(BP);
    StringSetter(BP,TEXT("SetLabel"),TEXT("Label")); StringSetter(BP,TEXT("SetDescription"),TEXT("Description"));
    StringSetter(BP,TEXT("SetButtonLabel"),TEXT("ActionButtonCaption")); StringSetter(BP,TEXT("SetInput"),TEXT("TextInput"),UEditableTextBox::StaticClass());
    Event(BP,TEXT("ActionButton"),TEXT("OnClicked"),TEXT("EventSerial")); Event(BP,TEXT("ValueSlider"),TEXT("OnValueChanged"),TEXT("EventSerial"));
    Event(BP,TEXT("Toggle"),TEXT("OnCheckStateChanged"),TEXT("EventSerial")); Event(BP,TEXT("Dropdown"),TEXT("OnSelectionChanged"),TEXT("EventSerial")); Event(BP,TEXT("TextInput"),TEXT("OnTextChanged"),TEXT("EventSerial"));
    Finalize(BP);

    BP=NewBP(TEXT("WBP_Book")); T=BP->WidgetTree;
    Root=T->ConstructWidget<UCanvasPanel>(); T->RootWidget=Root;
    auto Dim=T->ConstructWidget<UBorder>(); Dim->SetBrushColor(FLinearColor(0,0,0,.55f)); auto DimSlot=Root->AddChildToCanvas(Dim); DimSlot->SetAnchors(FAnchors(0,0,1,1)); DimSlot->SetOffsets(FMargin(0));
    auto Scale=T->ConstructWidget<UScaleBox>(); Scale->SetStretch(EStretch::ScaleToFit); auto ScaleSlot=Root->AddChildToCanvas(Scale); ScaleSlot->SetAnchors(FAnchors(0,0,1,1)); ScaleSlot->SetOffsets(FMargin(28));
    auto Size=T->ConstructWidget<USizeBox>(USizeBox::StaticClass(),TEXT("BookSize")); Size->bIsVariable=true; Size->SetWidthOverride(760); Size->SetHeightOverride(940); Scale->AddChild(Size);
    auto Paper=T->ConstructWidget<UCanvasPanel>(); Size->AddChild(Paper);
    auto Image=T->ConstructWidget<UImage>(UImage::StaticClass(),TEXT("BookBackground")); Image->bIsVariable=true; Image->SetBrushFromTexture(Texture); Place(Paper,Image,0,0,760,940);
    Place(Paper,Text(T,TEXT("Title"),32,TEXT("The Imperial Ledger")),65,100,570,48);
    Place(Paper,Text(T,TEXT("Subtitle"),18,TEXT("A book for every purpose")),65,153,620,38);
    Place(Paper,Button(T,TEXT("CloseButton"),TEXT("Close")),610,100,84,38);
    auto Scroll=T->ConstructWidget<UScrollBox>(UScrollBox::StaticClass(),TEXT("BodyScroll")); Scroll->bIsVariable=true; Place(Paper,Scroll,65,212,630,610);
    // Slim leather channel with a brass thumb and brighter hover/drag edges.
    FScrollBarStyle Bar;
    Bar.SetThickness(8);
    Bar.VerticalBackgroundImage=FSlateRoundedBoxBrush(FLinearColor(.08f,.045f,.02f,.28f),3.f,FLinearColor(.28f,.18f,.07f,.55f),1.f,FVector2D(8,8));
    Bar.HorizontalBackgroundImage=Bar.VerticalBackgroundImage;
    Bar.VerticalTopSlotImage=Bar.VerticalBottomSlotImage=Bar.HorizontalTopSlotImage=Bar.HorizontalBottomSlotImage=FSlateNoResource();
    Bar.NormalThumbImage=FSlateRoundedBoxBrush(FLinearColor(.32f,.20f,.075f,1),3.f,FLinearColor(.62f,.44f,.19f,1),1.f,FVector2D(8,32));
    Bar.HoveredThumbImage=FSlateRoundedBoxBrush(FLinearColor(.46f,.30f,.11f,1),3.f,FLinearColor(.8f,.62f,.31f,1),1.f,FVector2D(8,32));
    Bar.DraggedThumbImage=FSlateRoundedBoxBrush(FLinearColor(.24f,.13f,.04f,1),3.f,FLinearColor(.9f,.7f,.35f,1),1.f,FVector2D(8,32));
    Scroll->SetWidgetBarStyle(Bar); Scroll->SetScrollbarThickness(FVector2D(8,8)); Scroll->SetScrollbarPadding(FMargin(5,2,1,2));
    Scroll->SetAlwaysShowScrollbar(false); Scroll->SetAlwaysShowScrollbarTrack(false);
    auto ScrollStyle=Scroll->GetWidgetStyle(); ScrollStyle.TopShadowBrush=ScrollStyle.BottomShadowBrush=FSlateNoResource(); Scroll->SetWidgetStyle(ScrollStyle);
    auto BodySize=T->ConstructWidget<USizeBox>(USizeBox::StaticClass(),TEXT("BodySize")); BodySize->bIsVariable=true; BodySize->SetHeightOverride(610); Scroll->AddChild(BodySize);
    auto Content=T->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("Content")); Content->bIsVariable=true; BodySize->AddChild(Content);
    Place(Paper,Button(T,TEXT("PreviousButton"),TEXT("Previous")),65,846,150,38);
    Place(Paper,Text(T,TEXT("PageLabel"),20,TEXT("1 / 1")),310,850,160,36);
    Place(Paper,Button(T,TEXT("NextButton"),TEXT("Next")),545,846,150,38);
    IntVar(BP,TEXT("CloseSerial")); IntVar(BP,TEXT("PreviousSerial")); IntVar(BP,TEXT("NextSerial"));
    FKismetEditorUtilities::CompileBlueprint(BP);
    StringSetter(BP,TEXT("SetTitle"),TEXT("Title")); StringSetter(BP,TEXT("SetSubtitle"),TEXT("Subtitle")); StringSetter(BP,TEXT("SetPageLabel"),TEXT("PageLabel"));
    Event(BP,TEXT("CloseButton"),TEXT("OnClicked"),TEXT("CloseSerial")); Event(BP,TEXT("PreviousButton"),TEXT("OnClicked"),TEXT("PreviousSerial")); Event(BP,TEXT("NextButton"),TEXT("OnClicked"),TEXT("NextSerial"));
    Finalize(BP); UE_LOG(LogTemp,Display,TEXT("BOOK_UI_BUILT: book and interactive control widgets.")); return 0;
}

int32 PreviewBookUI()
{
    using namespace BookBuild;
    if(!FSlateApplication::IsInitialized()) FSlateApplication::InitializeAsStandaloneApplication(FModuleManager::LoadModuleChecked<ISlateRHIRendererModule>(TEXT("SlateRHIRenderer")).CreateSlateRHIRenderer());
    auto Make=[](const TCHAR* Path) { auto C=LoadClass<UUserWidget>(nullptr,Path); check(C); auto W=NewObject<UUserWidget>(GetTransientPackage(),C); W->AddToRoot(); check(W->Initialize()); return W; };
    auto Set=[](UUserWidget* W,FName Fn,const TCHAR* Value) { struct { FString Text; } A{Value}; W->ProcessEvent(W->FindFunctionChecked(Fn),&A); };
    auto Book=Make(TEXT("/Game/OblivionUI/Book/WBP_Book.WBP_Book_C"));
    Set(Book,TEXT("SetTitle"),TEXT("The Arena Ledger")); Set(Book,TEXT("SetSubtitle"),TEXT("Prepare for your next challenge")); Set(Book,TEXT("SetPageLabel"),TEXT("1 / 3"));
    auto Content=CastChecked<UCanvasPanel>(Book->WidgetTree->FindWidget(TEXT("Content")));
    const TCHAR* Labels[]={TEXT("Enter the arena"),TEXT("Match difficulty"),TEXT("Wager amount"),TEXT("Match announcements")};
    const TCHAR* Details[]={TEXT("Speak to the gatekeeper when you are ready."),TEXT("Choose the challenge that suits your champion."),TEXT("Set how much gold you wish to wager."),TEXT("Receive a notice before each match begins.")};
    const TCHAR* Controls[]={TEXT("ActionButton"),TEXT("Dropdown"),TEXT("ValueSlider"),TEXT("Toggle")};
    TArray<UUserWidget*> Rows;
    for(int I=0;I<4;++I) {
        auto Row=Make(TEXT("/Game/OblivionUI/Book/WBP_BookControl.WBP_BookControl_C")); Rows.Add(Row);
        Set(Row,TEXT("SetLabel"),Labels[I]); Set(Row,TEXT("SetDescription"),Details[I]); Set(Row,TEXT("SetButtonLabel"),TEXT("Join the next match"));
        auto Control=Row->WidgetTree->FindWidget(Controls[I]); Control->SetVisibility(ESlateVisibility::Visible);
        if(auto C=Cast<UComboBoxString>(Control)) { C->AddOption(TEXT("Novice")); C->AddOption(TEXT("Gladiator")); C->AddOption(TEXT("Champion")); C->SetSelectedIndex(1); }
        if(auto S=Cast<USlider>(Control)) S->SetValue(.4f);
        if(auto C=Cast<UCheckBox>(Control)) C->SetIsChecked(true);
        Place(Content,Row,0,I*144,610,128);
        auto Serial=FindFProperty<FIntProperty>(Row->GetClass(),TEXT("EventSerial")); check(Serial);
        int Before=Serial->GetPropertyValue_InContainer(Row);
        CastChecked<UButton>(Row->WidgetTree->FindWidget(TEXT("ActionButton")))->OnClicked.Broadcast();
        check(Serial->GetPropertyValue_InContainer(Row)==Before+1);
    }
    auto Close=FindFProperty<FIntProperty>(Book->GetClass(),TEXT("CloseSerial")); check(Close);
    CastChecked<UButton>(Book->WidgetTree->FindWidget(TEXT("CloseButton")))->OnClicked.Broadcast(); check(Close->GetPropertyValue_InContainer(Book)==1);
    FTextureCompilingManager::Get().FinishAllCompilation(); FlushRenderingCommands();
    auto Background=CastChecked<UImage>(Book->WidgetTree->FindWidget(TEXT("BookBackground")));
    auto BT=CastChecked<UTexture2D>(Background->GetBrush().GetResourceObject());
    UE_LOG(LogTemp,Display,TEXT("Book texture %s %d x %d source=%d resource=%d"),*BT->GetPathName(),BT->GetSizeX(),BT->GetSizeY(),BT->Source.IsValid(),BT->GetResource()!=nullptr);
    BT->UpdateResource(); FlushRenderingCommands();
    FWidgetRenderer Renderer(false); auto RT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000)); check(RT);
    FBufferArchive PNG; check(FImageUtils::ExportRenderTarget2DAsPNG(RT,PNG)); check(FFileHelper::SaveArrayToFile(PNG,*(FPaths::ProjectDir()/TEXT("Output/BookPreview.png"))));
    // Exercise overflow as well as the short-page case; this is preview-only.
    CastChecked<USizeBox>(Book->WidgetTree->FindWidget(TEXT("BodySize")))->SetHeightOverride(1100);
    Book->ForceLayoutPrepass();
    auto PreviewScroll=CastChecked<UScrollBox>(Book->WidgetTree->FindWidget(TEXT("BodyScroll")));
    PreviewScroll->SetAlwaysShowScrollbar(true); PreviewScroll->SetAlwaysShowScrollbarTrack(true);
    auto ScrollRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000)); check(ScrollRT);
    FSlateApplication::Get().Tick();
    ScrollRT=Renderer.DrawWidget(Book->TakeWidget(),FVector2D(1600,1000));
    FBufferArchive ScrollPNG; check(FImageUtils::ExportRenderTarget2DAsPNG(ScrollRT,ScrollPNG));
    check(FFileHelper::SaveArrayToFile(ScrollPNG,*(FPaths::ProjectDir()/TEXT("Output/BookScrollPreview.png"))));
    for(auto Row:Rows) Row->RemoveFromRoot(); Book->RemoveFromRoot();
    UE_LOG(LogTemp,Display,TEXT("BOOK_PREVIEW_VALIDATED: real UMG book rendered; button and close events passed.")); return 0;
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
    Add(UUserWidget::StaticClass(),{TEXT("AddToViewport"),TEXT("RemoveFromParent"),TEXT("GetOwningPlayer"),TEXT("SetKeyboardFocus"),TEXT("SetVisibility"),TEXT("SetIsEnabled")});
    Add(UCanvasPanel::StaticClass(),{TEXT("ClearChildren"),TEXT("AddChildToCanvas")});
    Add(UCanvasPanelSlot::StaticClass(),{TEXT("SetPosition"),TEXT("SetSize")});
    Add(USizeBox::StaticClass(),{TEXT("SetHeightOverride")});
    Add(USlider::StaticClass(),{TEXT("SetMinValue"),TEXT("SetMaxValue"),TEXT("SetStepSize"),TEXT("SetValue"),TEXT("GetValue")});
    Add(UCheckBox::StaticClass(),{TEXT("SetIsChecked"),TEXT("IsChecked")});
    Add(UComboBoxString::StaticClass(),{TEXT("ClearOptions"),TEXT("AddOption"),TEXT("SetSelectedIndex"),TEXT("GetSelectedIndex")});
    Add(UScrollBox::StaticClass(),{TEXT("ScrollToStart")});
    check(FFileHelper::SaveStringToFile(Data,*(FPaths::ProjectDir()/TEXT("OblivionUI.Client/BookNative.tsv")),FFileHelper::EEncodingOptions::ForceUTF8WithoutBOM));
    return 0;
}



