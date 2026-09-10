#pragma once
#include "Commandlets/Commandlet.h"
#include "BuildInteractionUICommandlet.generated.h"

UCLASS()
class UBuildInteractionUICommandlet : public UCommandlet
{
    GENERATED_BODY()
public:
    UBuildInteractionUICommandlet();
    virtual int32 Main(const FString& Params) override;
};
