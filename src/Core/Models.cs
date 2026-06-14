using System.Net;

namespace ScanPhoneNetwork;

/// <summary>업무망에서 발견된 장비 한 대에 대한 수집 정보.</summary>
public sealed class DiscoveredHost
{
    public required IPAddress Ip { get; init; }

    /// <summary>SendARP 로 얻은 MAC. 같은 L2 세그먼트에 있어야 채워진다.</summary>
    public string? Mac { get; set; }

    /// <summary>OUI(MAC 앞 3바이트) 기반 제조사.</summary>
    public string? Vendor { get; set; }

    /// <summary>ICMP 응답 TTL. NAT 뒤 추가 홉을 추정하는 데 사용.</summary>
    public int? Ttl { get; set; }

    /// <summary>응답한 열린 포트들 (5060=SIP, 80/443=HTTP 등).</summary>
    public List<int> OpenPorts { get; } = new();

    /// <summary>SSDP M-SEARCH 응답 여부 (공유기/UPnP 게이트웨이 신호).</summary>
    public bool SsdpGateway { get; set; }

    /// <summary>DHCP OFFER 를 보냈는지 (무단 DHCP 서버 = 공유기 결정적 증거).</summary>
    public bool DhcpServer { get; set; }

    /// <summary>HTTP/SIP 배너에서 추출한 부가 단서.</summary>
    public List<string> Banners { get; } = new();

    public DeviceCategory Category { get; set; } = DeviceCategory.Unknown;

    /// <summary>분류 근거 (왜 그렇게 판단했는지).</summary>
    public List<string> Evidence { get; } = new();

    /// <summary>0~100 신뢰도.</summary>
    public int Confidence { get; set; }
}

public enum DeviceCategory
{
    Unknown,
    Router,      // 무단 공유기 (NAT)
    WirelessAp,  // 무선 AP
    VoipPhone,   // 인터넷 전화기
    Infrastructure, // 정상 스위치/방화벽/서버 등
}
