using System.Text;

namespace ScanPhoneNetwork;

/// <summary>스캔 결과를 엑셀 호환 CSV(UTF-8 BOM)로 저장. 교육청 보고·대장용.</summary>
public static class CsvExporter
{
    public static void Save(ScanReport report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 업무망 점검 결과");
        sb.AppendLine($"# 대상,{report.TargetRange}");
        sb.AppendLine($"# 시작,{report.StartedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# 종료,{report.FinishedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# 발견호스트,{report.Hosts.Count},의심장비,{report.Suspicious.Count()}");
        sb.AppendLine();
        sb.AppendLine("IP,MAC,종류,신뢰도(%),제조사,열린포트,근거");

        foreach (var h in report.Hosts)
        {
            sb.Append(Csv(h.Ip.ToString())).Append(',');
            sb.Append(Csv(h.Mac ?? "")).Append(',');
            sb.Append(Csv(CategoryKo(h.Category))).Append(',');
            sb.Append(h.Confidence).Append(',');
            sb.Append(Csv(h.Vendor ?? "")).Append(',');
            sb.Append(Csv(string.Join(" ", h.OpenPorts))).Append(',');
            sb.Append(Csv(string.Join(" / ", h.Evidence)));
            sb.AppendLine();
        }

        // 엑셀 한글 깨짐 방지: UTF-8 BOM
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static string CategoryKo(DeviceCategory c) => c switch
    {
        DeviceCategory.Router => "공유기(의심)",
        DeviceCategory.WirelessAp => "무선AP(의심)",
        DeviceCategory.VoipPhone => "인터넷전화기",
        DeviceCategory.Infrastructure => "인프라",
        _ => "미상",
    };

    private static string Csv(string s)
    {
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
