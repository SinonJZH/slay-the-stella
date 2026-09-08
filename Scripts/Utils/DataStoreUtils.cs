using STS2RitsuLib;
using STS2RitsuLib.Data;

namespace SlayTheStella.Scripts.Utils;

public static class DataStoreUtils
{
    private static readonly ModDataStore DataStore = RitsuLibFramework.GetDataStore(StsConsts.ModId);

    public static StsSettings GetSettings()
    {
        return DataStore.Get<StsSettings>(StsConsts.SettingsDataKey);
    }
}