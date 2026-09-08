using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using SlayTheStella.Scripts.Shared.SecondaryRes;
using SlayTheStella.Scripts.Utils;
using STS2RitsuLib;
using STS2RitsuLib.Interop;

namespace SlayTheStella.Scripts;

// 必须要加的属性，用于注册Mod。字符串和初始化函数命名一致。
[ModInitializer(nameof(Init))]
public class Entry
{
    public static void Init()
    {
        
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, StsLogger.Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(StsConsts.ModId, assembly);

        StsSettingsPage.Register();
        AudioUtils.Init();

        ResNotes.Register();

        StsLogger.Info("Mod Initialized");
    }
}