using System.Text.Json;

namespace ScanPhoneNetwork;

/// <summary>
/// 승인된(정상) 장비 MAC 목록. 감시 모드에서 이 목록에 없는 의심 장비가
/// 새로 나타나면 경고한다. JSON 파일로 저장/로드.
/// </summary>
public sealed class AllowList
{
    public HashSet<string> ApprovedMacs { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsApproved(string? mac) =>
        !string.IsNullOrWhiteSpace(mac) && ApprovedMacs.Contains(Normalize(mac));

    public void Approve(string mac) => ApprovedMacs.Add(Normalize(mac));

    public static string Normalize(string mac) =>
        mac.Replace("-", ":").ToUpperInvariant();

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));

    public static AllowList Load(string path)
    {
        if (!File.Exists(path)) return new AllowList();
        try
        {
            return JsonSerializer.Deserialize<AllowList>(File.ReadAllText(path)) ?? new AllowList();
        }
        catch { return new AllowList(); }
    }
}
