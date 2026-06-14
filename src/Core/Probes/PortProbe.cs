using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace ScanPhoneNetwork.Probes;

/// <summary>
/// 호스트별로 관심 포트(TCP)를 열어보고, HTTP/SIP 배너를 수집한다.
///  - 5060/5061 SIP : 인터넷 전화기
///  - 80/443 HTTP   : 공유기 로그인 페이지 / 전화기 관리 페이지
/// </summary>
public static class PortProbe
{
    public static readonly int[] InterestingPorts =
        { 80, 443, 5060, 5061, 8080, 23, 9100, 515, 631 }; // 9100/515/631 = 프린터

    public sealed record Result(List<int> OpenPorts, List<string> Banners);

    public static async Task<Result> ProbeAsync(IPAddress ip, int connectTimeoutMs = 500)
    {
        var open = new List<int>();
        var banners = new List<string>();

        foreach (int port in InterestingPorts)
        {
            if (await IsOpenAsync(ip, port, connectTimeoutMs))
                open.Add(port);
        }

        if (open.Contains(80)) AddIfNotEmpty(banners, await HttpBannerAsync(ip, 80, false));
        else if (open.Contains(8080)) AddIfNotEmpty(banners, await HttpBannerAsync(ip, 8080, false));
        if (open.Contains(443)) AddIfNotEmpty(banners, await HttpBannerAsync(ip, 443, true));

        // SIP 는 UDP OPTIONS 로 별도 확인(포트 스캔에 안 잡혀도 응답할 수 있음)
        AddIfNotEmpty(banners, await SipOptionsAsync(ip));

        return new Result(open, banners);
    }

    private static async Task<bool> IsOpenAsync(IPAddress ip, int port, int timeoutMs)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(ip, port);
            var done = await Task.WhenAny(connect, Task.Delay(timeoutMs));
            return done == connect && client.Connected;
        }
        catch { return false; }
    }

    private static async Task<string?> HttpBannerAsync(IPAddress ip, int port, bool tls)
    {
        try
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
            string scheme = tls ? "https" : "http";
            using var resp = await http.GetAsync($"{scheme}://{ip}:{port}/");
            string body = await resp.Content.ReadAsStringAsync();

            string? server = resp.Headers.TryGetValues("Server", out var sv) ? string.Join(",", sv) : null;
            var m = Regex.Match(body, "<title>(.*?)</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            string? title = m.Success ? m.Groups[1].Value.Trim() : null;

            var parts = new[] { server is null ? null : $"Server={server}", title is null ? null : $"Title={title}" }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            string joined = string.Join(" | ", parts);
            return string.IsNullOrEmpty(joined) ? $"HTTP:{port} open" : $"HTTP:{port} {joined}";
        }
        catch { return null; }
    }

    private static async Task<string?> SipOptionsAsync(IPAddress ip)
    {
        try
        {
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 800;
            string branch = "z9hG4bK-scan-001";
            string msg =
                $"OPTIONS sip:{ip} SIP/2.0\r\n" +
                $"Via: SIP/2.0/UDP scan;branch={branch}\r\n" +
                "Max-Forwards: 70\r\n" +
                $"To: <sip:{ip}>\r\n" +
                "From: <sip:scan@scan>;tag=1\r\n" +
                "Call-ID: scan-call-id\r\n" +
                "CSeq: 1 OPTIONS\r\n" +
                "Content-Length: 0\r\n\r\n";
            byte[] payload = Encoding.ASCII.GetBytes(msg);
            await udp.SendAsync(payload, payload.Length, new IPEndPoint(ip, 5060));

            using var cts = new CancellationTokenSource(800);
            var recv = await udp.ReceiveAsync(cts.Token);
            string text = Encoding.ASCII.GetString(recv.Buffer);
            foreach (var line in text.Split('\n'))
            {
                if (line.StartsWith("Server:", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("User-Agent:", StringComparison.OrdinalIgnoreCase))
                    return "SIP " + line.Trim();
            }
            return "SIP 응답(5060)";
        }
        catch { return null; }
    }

    private static void AddIfNotEmpty(List<string> list, string? s)
    {
        if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
    }
}
