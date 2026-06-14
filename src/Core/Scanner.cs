using System.Net;
using System.Runtime.InteropServices;
using ScanPhoneNetwork.Probes;

namespace ScanPhoneNetwork;

/// <summary>스캔 진행 상황 알림.</summary>
public sealed record ScanProgress(string Phase, int Done, int Total);

/// <summary>한 번의 스캔 결과 묶음.</summary>
public sealed class ScanReport
{
    public DateTime StartedAt { get; init; } = DateTime.Now;
    public DateTime FinishedAt { get; set; }
    public string TargetRange { get; init; } = "";
    public List<DiscoveredHost> Hosts { get; init; } = new();

    /// <summary>업무망에 있으면 안 되는(=의심) 장비만 추림.</summary>
    public IEnumerable<DiscoveredHost> Suspicious =>
        Hosts.Where(h => h.Category is DeviceCategory.Router
                                    or DeviceCategory.WirelessAp
                                    or DeviceCategory.VoipPhone);
}

/// <summary>전체 스캔 파이프라인을 묶는 오케스트레이터. UI 와 무관하게 재사용.</summary>
public sealed class Scanner
{
    /// <param name="cidr">"10.20.30.0/24" 형식. null 이면 실행 PC 대역 자동 탐지.</param>
    public async Task<ScanReport> RunAsync(
        string? cidr,
        IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
    {
        IPAddress network, mask;
        string label;

        if (!string.IsNullOrWhiteSpace(cidr))
        {
            if (!NetworkInfo.TryParseCidr(cidr, out network, out mask))
                throw new ArgumentException($"대역 형식 오류: '{cidr}' (예: 10.20.30.0/24)");
            label = cidr;
        }
        else
        {
            var sub = NetworkInfo.GetActiveSubnet()
                ?? throw new InvalidOperationException("활성 인터페이스를 찾지 못했습니다. 대역을 직접 지정하세요.");
            network = sub.LocalIp;
            mask = sub.Mask;
            label = $"{sub.LocalIp}/{MaskToPrefix(sub.Mask)} ({sub.InterfaceName})";
        }

        var report = new ScanReport { TargetRange = label };
        var targets = NetworkInfo.EnumerateHosts(network, mask).ToList();

        // 1) DHCP/SSDP 는 브로드캐스트라 호스트 목록과 별개로 1회씩
        progress?.Report(new ScanProgress("DHCP 서버 탐지", 0, 1));
        var dhcpServers = await DhcpProbe.FindDhcpServersAsync();
        ct.ThrowIfCancellationRequested();

        progress?.Report(new ScanProgress("SSDP(공유기) 탐지", 0, 1));
        var ssdp = await SsdpProbe.DiscoverAsync();
        ct.ThrowIfCancellationRequested();

        // 2) 핑 스윕
        progress?.Report(new ScanProgress("핑 스윕", 0, targets.Count));
        var hosts = await HostDiscovery.PingSweepAsync(targets);

        // SSDP/DHCP 로만 보인 호스트도 합류(핑에 응답 안 해도 잡기)
        MergeExtraHosts(hosts, ssdp.Keys);
        if (dhcpServers is not null) MergeExtraHosts(hosts, dhcpServers);

        // 3) MAC 해석 (Windows)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            HostDiscovery.ResolveMacs(hosts);

        // 4) 호스트별 포트/배너 프로브
        int done = 0;
        foreach (var h in hosts)
        {
            ct.ThrowIfCancellationRequested();
            var pr = await PortProbe.ProbeAsync(h.Ip);
            h.OpenPorts.AddRange(pr.OpenPorts);
            h.Banners.AddRange(pr.Banners);
            if (ssdp.TryGetValue(h.Ip, out var banner))
            {
                h.SsdpGateway = true;
                if (!string.IsNullOrEmpty(banner)) h.Banners.Add("SSDP " + banner);
            }
            progress?.Report(new ScanProgress("포트/배너 프로브", ++done, hosts.Count));
        }

        // 5) 분류
        int? baseline = hosts.Where(h => h.Ttl is not null)
            .GroupBy(h => h.Ttl).OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;
        foreach (var h in hosts)
            Classifier.Classify(h, baseline, dhcpServers);

        report.Hosts.AddRange(hosts.OrderByDescending(h => h.Confidence));
        report.FinishedAt = DateTime.Now;
        return report;
    }

    private static void MergeExtraHosts(List<DiscoveredHost> hosts, IEnumerable<IPAddress> extra)
    {
        var have = hosts.Select(h => h.Ip.ToString()).ToHashSet();
        foreach (var ip in extra)
            if (have.Add(ip.ToString()))
                hosts.Add(new DiscoveredHost { Ip = ip });
    }

    private static int MaskToPrefix(IPAddress mask)
    {
        return mask.GetAddressBytes()
            .Sum(b => System.Numerics.BitOperations.PopCount(b));
    }
}
