using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ScanPhoneNetwork.Gui;

/// <summary>
/// 정보부장 PC 에서 로그인 시 자동 실행되도록 등록/해제.
/// 레지스트리 Run 키(HKCU) 사용 — 관리자 권한 불필요. Windows 전용.
/// </summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "업무망점검기-감시";

    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return false;
        return ReadEnabled();
    }

    public static void Enable()
    {
        if (!OperatingSystem.IsWindows()) return;
        WriteEnable();
    }

    public static void Disable()
    {
        if (!OperatingSystem.IsWindows()) return;
        WriteDisable();
    }

    [SupportedOSPlatform("windows")]
    private static bool ReadEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    [SupportedOSPlatform("windows")]
    private static void WriteEnable()
    {
        string exe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exe)) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        key.SetValue(ValueName, $"\"{exe}\" --monitor");
    }

    [SupportedOSPlatform("windows")]
    private static void WriteDisable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
