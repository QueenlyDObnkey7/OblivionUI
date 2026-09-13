#include "WidgetBlueprint.h"
#include "Blueprint/WidgetTree.h"
#include "Blueprint/WidgetBlueprintGeneratedClass.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Image.h"
#include "Engine/Texture2D.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "UObject/SavePackage.h"
#include "Misc/PackageName.h"

// Original vector-style speaker: warm ivory enamel, bronze edge and dark relief shadow.
// Supersampled geometric rasterization keeps the tiny transparent icon crisp without emoji fonts.
static UTexture2D* SpeakerTexture()
{
    auto Package=CreatePackage(TEXT("/Game/OblivionUI/Voice/T_Speaker"));
    auto Texture=NewObject<UTexture2D>(Package,TEXT("T_Speaker"),RF_Public|RF_Standalone);
    constexpr int N=128, SS=4;
    TArray<FColor> Pixels; Pixels.SetNumZeroed(N*N);
    auto Shape=[](float X,float Y) {
        bool Cone=(X>=24 && X<=40 && Y>=48 && Y<=80) ||
            (X>=40 && X<=66 && Y>=48-(X-40)*.75f && Y<=80+(X-40)*.75f);
        float DX=X-61,DY=Y-64,R=FMath::Sqrt(DX*DX+DY*DY);
        bool Arc=DX>8 && FMath::Abs(DY)<DX*1.1f && (FMath::Abs(R-29)<2.6f || FMath::Abs(R-45)<2.6f);
        return Cone || Arc;
    };
    for(int Y=0;Y<N;Y++) for(int X=0;X<N;X++) {
        FVector4f Sum(0,0,0,0);
        for(int V=0;V<SS;V++) for(int U=0;U<SS;U++) {
            float PX=X+(U+.5f)/SS,PY=Y+(V+.5f)/SS;
            bool Fill=Shape(PX,PY),Outline=false,Shadow=false;
            for(int I=0;I<12;I++) { float A=I*PI/6;
                Outline |= Shape(PX+FMath::Cos(A)*1.7f,PY+FMath::Sin(A)*1.7f);
                Shadow |= Shape(PX-1.5f+FMath::Cos(A)*2.6f,PY-2+FMath::Sin(A)*2.6f);
            }
            if(Fill) {float Light=1-.14f*(PY/128.f); Sum+=FVector4f(239*Light,223*Light,180*Light,255);}
            else if(Outline) Sum+=FVector4f(123,91,45,255);
            else if(Shadow) Sum+=FVector4f(25,20,14,210);
        }
        if(Sum.W>0) {float A=Sum.W/(SS*SS); float Scale=255/Sum.W; Pixels[Y*N+X]=FColor(Sum.X*Scale,Sum.Y*Scale,Sum.Z*Scale,A);}
    }
    Texture->Source.Init(N,N,1,1,TSF_BGRA8,reinterpret_cast<const uint8*>(Pixels.GetData()));
    Texture->CompressionSettings=TC_EditorIcon; Texture->LODGroup=TEXTUREGROUP_UI;
    Texture->MipGenSettings=TMGS_NoMipmaps; Texture->SRGB=true; Texture->UpdateResource();
    FSavePackageArgs Args; Args.TopLevelFlags=RF_Public|RF_Standalone;
    check(UPackage::SavePackage(Package,Texture,*FPackageName::LongPackageNameToFilename(Package->GetName(),TEXT(".uasset")),Args));
    return Texture;
}
int32 BuildVoiceUI()
{
    auto Texture=SpeakerTexture();
    for(bool Overlay:{false,true}) {
        FString Name=Overlay ? TEXT("WBP_VoiceOverlay") : TEXT("WBP_SpeakerIcon");
        FString Path=TEXT("/Game/OblivionUI/Voice/")+Name;
        auto BP=CastChecked<UWidgetBlueprint>(FKismetEditorUtilities::CreateBlueprint(UUserWidget::StaticClass(),CreatePackage(*Path),FName(*Name),BPTYPE_Normal,UWidgetBlueprint::StaticClass(),UWidgetBlueprintGeneratedClass::StaticClass()));
        auto Tree=BP->WidgetTree;
        auto Root=Tree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(),TEXT("VoiceCanvas")); Root->bIsVariable=true; Tree->RootWidget=Root;
        auto Icon=Tree->ConstructWidget<UImage>(UImage::StaticClass(),Overlay ? TEXT("LocalSpeaker") : TEXT("Speaker")); Icon->bIsVariable=true;
        Icon->SetBrushFromTexture(Texture); Icon->SetVisibility(Overlay ? ESlateVisibility::Collapsed : ESlateVisibility::HitTestInvisible);
        auto Slot=Root->AddChildToCanvas(Icon);
        if(Overlay) {Slot->SetAnchors(FAnchors(1,1));Slot->SetAlignment({1,1});Slot->SetPosition({-28,-30});Slot->SetSize({48,48});}
        else {Slot->SetAnchors(FAnchors(0,0,1,1));Slot->SetOffsets(FMargin(0));}
        FKismetEditorUtilities::CompileBlueprint(BP); if(BP->Status==BS_Error)return 2;
        CastChecked<UUserWidget>(BP->GeneratedClass->GetDefaultObject())->SetVisibility(ESlateVisibility::HitTestInvisible);
        FSavePackageArgs Args; Args.TopLevelFlags=RF_Public|RF_Standalone;
        check(UPackage::SavePackage(BP->GetOutermost(),BP,*FPackageName::LongPackageNameToFilename(Path,TEXT(".uasset")),Args));
    }
    UE_LOG(LogTemp,Display,TEXT("VOICE_UI_BUILT: shared speaker icon and bottom-right overlay."));return 0;
}
#include "Engine/World.h"
#include "Components/BoxComponent.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Kismet/GameplayStatics.h"
#include "ImageUtils.h"
#include "Misc/FileHelper.h"
#include "Serialization/BufferArchive.h"

int32 VoiceChecks()
{
    // Run actual Unreal collision queries, rather than treating a mocked LOS boolean as proof.
    auto World=UWorld::CreateWorld(EWorldType::Game,false);

    auto Wall=World->SpawnActor<AActor>();
    auto Box=NewObject<UBoxComponent>(Wall);Wall->SetRootComponent(Box);
    Box->SetBoxExtent({15,160,160});Box->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
    Box->SetCollisionObjectType(ECC_WorldStatic);Box->SetCollisionResponseToAllChannels(ECR_Block);
    Box->RegisterComponent();Wall->SetActorLocation({200,0,100});
    World->UpdateWorldComponents(true,false);World->Tick(LEVELTICK_All,.016f);
    auto Blocked=[&](FVector Start,FVector End) {FHitResult Hit;TArray<AActor*> Ignore;
        return UKismetSystemLibrary::LineTraceSingle(World,Start,End,TraceTypeQuery1,true,Ignore,EDrawDebugTrace::None,Hit,true);
    };
    checkf(Blocked({0,0,100},{400,0,100}),TEXT("Solid wall must block a speaker"));
    checkf(!Blocked({0,300,100},{400,300,100}),TEXT("Unobstructed speaker must pass"));
    checkf(Blocked({0,250,100},{400,-100,100}),TEXT("Corner diagonal must block a speaker"));
    checkf(!Blocked({0,0,400},{400,0,400}),TEXT("Clear sight above the wall must pass"));
    auto BP=LoadObject<UWidgetBlueprint>(nullptr,TEXT("/Game/OblivionUI/Voice/WBP_VoiceOverlay.WBP_VoiceOverlay"));check(BP);
    auto Icon=CastChecked<UImage>(BP->WidgetTree->FindWidget(TEXT("LocalSpeaker")));
    auto Slot=CastChecked<UCanvasPanelSlot>(Icon->Slot);
    check(Slot->GetAnchors().Minimum==FVector2D(1,1) && Slot->GetSize()==FVector2D(48,48));
    check(Icon->GetVisibility()==ESlateVisibility::Collapsed);
    auto Texture=CastChecked<UTexture2D>(Icon->GetBrush().GetResourceObject());
    TArray64<uint8> Raw;check(Texture->Source.GetMipData(Raw,0));
    TArray<FColor> Pixels;Pixels.Append(reinterpret_cast<const FColor*>(Raw.GetData()),128*128);
    check(Pixels[0].A==0);
    TArray64<uint8> PNG;FImageUtils::PNGCompressImageArray(128,128,Pixels,PNG);
    check(FFileHelper::SaveArrayToFile(PNG,*(FPaths::ProjectDir()/TEXT("Output/SpeakerIcon.png"))));
    World->DestroyWorld(false);
    UE_LOG(LogTemp,Display,TEXT("VOICE_CHECKS_PASSED: actual UE wall/corner/open-sight traces, bottom-right anchor, hidden default and transparent icon."));return 0;
}
