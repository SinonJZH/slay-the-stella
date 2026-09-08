using STS2RitsuLib.Audio;

namespace SlayTheStella.Scripts.Utils;

public static class AudioUtils
{

    public static void Init()
    {
        FmodStudioDeferredBankRegistration.RegisterBank($"{StsConsts.ResPath}/audios/desktop/SlayTheStella.bank");
        FmodStudioDeferredBankRegistration.RegisterStudioGuidMappings($"{StsConsts.ResPath}/audios/GUIDs.txt");
        
        StsLogger.InfoDebug("Mod audio loaded");
    }
    
}