using ScanPhoneNetwork;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=== 업무망 무단 장비 점검 (CLI) ===\n");

string? csvPath = GetOpt("--csv");
string? ouiPath = GetOpt("--oui");

// 옵션 값으로 소비된 인덱스를 제외하고 남는 위치 인자 = 대상 대역(CIDR)
var consumed = new HashSet<int>();
MarkOpt("--csv"); MarkOpt("--oui");
string? cidr = args.Where((a, i) => !a.StartsWith("--") && !consumed.Contains(i)).FirstOrDefault();

if (ouiPath is not null)
{
    int n = OuiDatabase.LoadExternalCsv(ouiPath);
    Console.WriteLine($"OUI 외부 DB 로드: {n}건\n");
}

var progress = new Progress<ScanProgress>(p =>
    Console.WriteLine($"  [{p.Phase}] {p.Done}/{p.Total}"));

try
{
    var report = await new Scanner().RunAsync(cidr, progress);

    Console.WriteLine($"\n대상: {report.TargetRange}");
    Console.WriteLine($"발견 호스트: {report.Hosts.Count}대 / 의심 장비: {report.Suspicious.Count()}대\n");

    Console.WriteLine($"{"IP",-16}{"MAC",-20}{"종류",-14}{"신뢰도",-8}제조사");
    Console.WriteLine(new string('-', 90));
    foreach (var h in report.Hosts)
    {
        Console.WriteLine($"{h.Ip,-16}{h.Mac ?? "-",-20}{CsvExporter.CategoryKo(h.Category),-14}{h.Confidence + "%",-8}{h.Vendor ?? ""}");
        foreach (var ev in h.Evidence) Console.WriteLine($"    · {ev}");
    }

    if (csvPath is not null)
    {
        CsvExporter.Save(report, csvPath);
        Console.WriteLine($"\nCSV 저장: {csvPath}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"오류: {ex.Message}");
    return 1;
}
return 0;

string? GetOpt(string name)
{
    int i = Array.IndexOf(args, name);
    return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
}

void MarkOpt(string name)
{
    int i = Array.IndexOf(args, name);
    if (i >= 0) { consumed.Add(i); if (i + 1 < args.Length) consumed.Add(i + 1); }
}
