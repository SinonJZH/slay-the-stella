using STS2RitsuLib;
using STS2RitsuLib.Data;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace SlayTheStella.Scripts.Utils;

public sealed class StsSettings
{
    public bool DebugMode { get; set; } = false;
}

public static class StsSettingsPage
{
    private static readonly ModSettingsValueBinding<StsSettings, bool> DebugModeBinding = new(
        StsConsts.ModId, StsConsts.SettingsDataKey, SaveScope.Profile,
        static s => s.DebugMode,
        static (s, v) => s.DebugMode = v);

    public static void Register()
    {
        ModDataStore.For(StsConsts.ModId).Register<StsSettings>(
            key: StsConsts.SettingsDataKey, // 持久化数据ID，需要和别人防撞
            fileName: "SlayTheStellaSettings.json", // 你的数据文件名
            scope: SaveScope.Global, // Profile 表示每个存档独立，可改成 Global 表示所有存档共享
            defaultFactory: () => new StsSettings(),
            autoCreateIfMissing: true);

        RitsuLibFramework.RegisterModSettings(StsConsts.ModId, page => page
            .WithTitle(ModSettingsText.I18N(I18NConfig.I18NInstance, "settings.title", "杀戮星塔模组配置"))
            .WithModDisplayName(ModSettingsText.I18N(I18NConfig.I18NInstance, "settings.displayName",
                "杀戮星塔 - SlayTheStella"))
            .WithVisibleOnHostSurfaces(
                ModSettingsHostSurface.MainMenu | ModSettingsHostSurface.RunPause)
            .AddSection("general", section => section
                .WithTitle(ModSettingsText.I18N(I18NConfig.I18NInstance, "settings.general", "通用"))
                .AddToggle("debug_mode",
                    ModSettingsText.I18N(I18NConfig.I18NInstance, "settings.debug_mode", "Debug模式"),
                    DebugModeBinding)));

        StsLogger.InfoDebug("Mod settings page loaded");
    }
}