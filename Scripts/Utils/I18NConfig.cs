using STS2RitsuLib;
using STS2RitsuLib.Utils;

namespace SlayTheStella.Scripts.Utils;

public static class I18NConfig
{
    public static readonly I18N I18NInstance = RitsuLibFramework.CreateModLocalizationWithFallback(
        modId: StsConsts.ModId,
        instanceName: "settings",
        pckFolders: [$"{StsConsts.ResPath}/localization/settings"],
        fallbackLanguage: "zhs"
    );
}