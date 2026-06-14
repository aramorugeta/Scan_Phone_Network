using Microsoft.Win32;

namespace ScanPhoneNetwork.Gui;

/// <summary>
/// 정보부장 PC 에서 로그인 시 자동 실행되도록 등록/해제.
/// 레지스트리 Run 키(HKCU)를 사용 — 관리자 권한 없이 동작.
/// </summary>
public static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "업무망점검기-감시";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public static void Enable()
    {
        string exe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exe)) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        // --monitor 로 시작하면 트레이로 최소화되어 자동 감시 시작
        key.SetValue(ValueName, $"\"{exe}\" --monitor");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
