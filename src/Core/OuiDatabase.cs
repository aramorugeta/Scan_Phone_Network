namespace ScanPhoneNetwork;

/// <summary>
/// MAC 앞 3바이트(OUI)로 제조사·장비종류를 추정.
/// 내장 목록은 국내 학교망에서 자주 보이는 대표 벤더 위주.
/// 식별률을 높이려면 IEEE 공식 목록(oui.csv)을 exe 옆에 두면 자동 로드된다.
///   다운로드: https://standards-oui.ieee.org/oui/oui.csv
/// </summary>
public static class OuiDatabase
{
    public sealed record Entry(string Vendor, DeviceCategory Category);

    // 키: OUI 6자리 16진수(구분자 없음, 대문자)
    private static readonly Dictionary<string, Entry> _builtin = Normalize(new()
    {
        // --- 공유기/AP (소비자용) → 업무망에 있으면 무단 의심 ---
        ["00:26:66"] = new("EFM Networks (ipTIME)", DeviceCategory.Router),
        ["88:36:6C"] = new("EFM Networks (ipTIME)", DeviceCategory.Router),
        ["64:E5:99"] = new("EFM Networks (ipTIME)", DeviceCategory.Router),
        ["50:6F:9A"] = new("TP-Link", DeviceCategory.Router),
        ["C4:6E:1F"] = new("TP-Link", DeviceCategory.Router),
        ["AC:84:C6"] = new("TP-Link", DeviceCategory.Router),
        ["50:C7:BF"] = new("TP-Link", DeviceCategory.Router),
        ["C8:3A:35"] = new("Tenda", DeviceCategory.Router),
        ["50:64:2B"] = new("ASUS", DeviceCategory.Router),
        ["AC:9E:17"] = new("ASUS", DeviceCategory.Router),
        ["FC:34:97"] = new("Netis", DeviceCategory.Router),
        ["28:6C:07"] = new("Xiaomi", DeviceCategory.Router),
        ["B0:95:75"] = new("Netgear", DeviceCategory.Router),
        ["00:1F:33"] = new("Netgear", DeviceCategory.Router),
        ["14:CC:20"] = new("TP-Link", DeviceCategory.Router),
        ["90:9A:4A"] = new("TP-Link", DeviceCategory.Router),

        // --- VoIP 전화기 ---
        ["00:15:65"] = new("Yealink", DeviceCategory.VoipPhone),
        ["80:5E:C0"] = new("Yealink", DeviceCategory.VoipPhone),
        ["00:0B:82"] = new("Grandstream", DeviceCategory.VoipPhone),
        ["00:1A:A0"] = new("Cisco (IP Phone)", DeviceCategory.VoipPhone),
        ["00:1E:75"] = new("LG (VoIP)", DeviceCategory.VoipPhone),
        ["00:26:E1"] = new("Moimstone", DeviceCategory.VoipPhone),
        ["00:09:45"] = new("Samsung (VoIP)", DeviceCategory.VoipPhone),
    });

    // 외부 oui.csv 로 확장되는 부분(런타임 로드).
    private static Dictionary<string, Entry> _external = new();

    public static Entry? Lookup(string? mac)
    {
        if (string.IsNullOrWhiteSpace(mac)) return null;
        var clean = mac.Replace(":", "").Replace("-", "").ToUpperInvariant();
        if (clean.Length < 6) return null;
        var oui = clean[..6];
        return _builtin.GetValueOrDefault(oui) ?? _external.GetValueOrDefault(oui);
    }

    /// <summary>
    /// IEEE oui.csv 를 읽어 제조사 식별률을 높인다.
    /// CSV 형식: Registry,Assignment,Organization Name,Organization Address
    /// 종류(Category)는 알 수 없으므로 Unknown 으로 두되 제조사명은 표시된다.
    /// </summary>
    public static int LoadExternalCsv(string path)
    {
        if (!File.Exists(path)) return 0;
        var map = new Dictionary<string, Entry>();
        foreach (var line in File.ReadLines(path).Skip(1))
        {
            // 간단 CSV 파싱: 두 번째 필드 = OUI(예 AABBCC), 세 번째 = 회사명
            var cols = SplitCsv(line);
            if (cols.Count < 3) continue;
            var oui = cols[1].Replace("-", "").Replace(":", "").Trim().ToUpperInvariant();
            if (oui.Length != 6) continue;
            var vendor = cols[2].Trim();
            map[oui] = new Entry(vendor, GuessCategory(vendor));
        }
        _external = map;
        return map.Count;
    }

    // 회사명에 키워드가 있으면 종류를 추정(내장 목록에 없을 때 보조).
    private static DeviceCategory GuessCategory(string vendor)
    {
        var v = vendor.ToLowerInvariant();
        if (v.Contains("yealink") || v.Contains("grandstream") || v.Contains("polycom")
            || v.Contains("snom") || v.Contains("moimstone"))
            return DeviceCategory.VoipPhone;
        if (v.Contains("tp-link") || v.Contains("tenda") || v.Contains("netis")
            || v.Contains("d-link") || v.Contains("iptime") || v.Contains("efm")
            || v.Contains("netgear") || v.Contains("xiaomi"))
            return DeviceCategory.Router;
        return DeviceCategory.Unknown;
    }

    private static Dictionary<string, Entry> Normalize(Dictionary<string, Entry> src) =>
        src.ToDictionary(
            kv => kv.Key.Replace(" ", "").Replace(":", "").Replace("-", "").ToUpperInvariant(),
            kv => kv.Value);

    private static List<string> SplitCsv(string line)
    {
        var result = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes) { result.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        result.Add(cur.ToString());
        return result;
    }
}
