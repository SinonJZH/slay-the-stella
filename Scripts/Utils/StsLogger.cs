using System.IO;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Logging;
using STS2RitsuLib;

namespace SlayTheStella.Scripts.Utils;

public static class StsLogger
{
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(StsConsts.ModId);

    public static void Info(
        string text,
        int skipFrames = 1,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Logger.Info($"[SlayTheStella - INFO] {text} ({FormatCaller(callerFile, callerLine)})", ++skipFrames);
    }

    public static void InfoDebug(
        string text,
        int skipFrames = 1,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        if (DataStoreUtils.GetSettings().DebugMode)
        {
            Logger.Info($"[SlayTheStella - INFO(DEBUG)] {text} ({FormatCaller(callerFile, callerLine)})", ++skipFrames);
        }
    }
    
    public static void Warn(
        string text,
        int skipFrames = 1,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Logger.Warn($"[SlayTheStella - WARN] {text} ({FormatCaller(callerFile, callerLine)})", ++skipFrames);
    }
    
    public static void Error(
        string text,
        int skipFrames = 1,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        Logger.Error($"[SlayTheStella - ERROR] {text} ({FormatCaller(callerFile, callerLine)})", ++skipFrames);
    }

    private static string FormatCaller(string? callerFile, int callerLine)
        => $"{Path.GetFileName(callerFile) ?? "?"}:{callerLine}";
}