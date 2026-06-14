using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScanPhoneNetwork.Probes;

/// <summary>
/// SSDP(UPnP) M-SEARCH 브로드캐스트로 게이트웨이 장비를 찾는다.
/// 소비자용 공유기는 대부분 InternetGatewayDevice 를 광고하므로
/// 업무망에서 응답이 오면 무단 공유기일 가능성이 매우 높다.
/// </summary>
public static class SsdpProbe
{
    private static readonly IPAddress MulticastAddr = IPAddress.Parse("239.255.255.250");
    private const int SsdpPort = 1900;

    /// <summary>응답한 호스트 IP → SERVER/ST 배너. 게이트웨이 여부 판단에 사용.</summary>
    public static async Task<Dictionary<IPAddress, string>> DiscoverAsync(int waitMs = 2500)
    {
        var results = new Dictionary<IPAddress, string>();

        string msearch =
            "M-SEARCH * HTTP/1.1\r\n" +
            "HOST: 239.255.255.250:1900\r\n" +
            "MAN: \"ssdp:discover\"\r\n" +
            "MX: 2\r\n" +
            "ST: urn:schemas-upnp-org:device:InternetGatewayDevice:1\r\n" +
            "\r\n";
        byte[] payload = Encoding.ASCII.GetBytes(msearch);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        var dest = new IPEndPoint(MulticastAddr, SsdpPort);
        await udp.SendAsync(payload, payload.Length, dest);
        await udp.SendAsync(payload, payload.Length, dest); // 손실 대비 2회

        using var cts = new CancellationTokenSource(waitMs);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var recv = await udp.ReceiveAsync(cts.Token);
                string text = Encoding.ASCII.GetString(recv.Buffer);
                string banner = ExtractBanner(text);
                results[recv.RemoteEndPoint.Address] = banner;
            }
        }
        catch (OperationCanceledException) { /* 수신 시간 종료 */ }
        catch (SocketException) { /* 소켓 종료 */ }

        return results;
    }

    private static string ExtractBanner(string ssdpResponse)
    {
        string? server = null, st = null;
        foreach (var line in ssdpResponse.Split('\n'))
        {
            if (line.StartsWith("SERVER:", StringComparison.OrdinalIgnoreCase))
                server = line[7..].Trim();
            else if (line.StartsWith("ST:", StringComparison.OrdinalIgnoreCase))
                st = line[3..].Trim();
        }
        return string.Join(" | ", new[] { server, st }.Where(s => !string.IsNullOrEmpty(s)));
    }
}
