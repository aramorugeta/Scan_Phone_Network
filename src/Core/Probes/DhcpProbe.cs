using System.Net;
using System.Net.Sockets;

namespace ScanPhoneNetwork.Probes;

/// <summary>
/// DHCP DISCOVER 를 브로드캐스트하고 OFFER 를 보내는 서버 IP 를 수집한다.
/// 정상 DHCP 서버(보통 방화벽/서버 1대) 외에 응답하는 IP 가 있으면
/// 그 장비가 자체 DHCP 를 돌리는 무단 공유기일 가능성이 매우 높다.
///
/// 주의: UDP 68 포트는 Windows DHCP 클라이언트가 점유하는 경우가 많아
/// 바인딩에 실패할 수 있다. 그럴 때는 빈 결과를 반환하고 상위에서 안내한다.
/// 정확도를 높이려면 관리자 권한 + 임시로 DHCP 클라이언트 중지가 필요할 수 있다.
/// </summary>
public static class DhcpProbe
{
    /// <summary>OFFER 를 보낸 서버 IP 집합. 실패 시 null(=프로브 불가).</summary>
    public static async Task<HashSet<IPAddress>?> FindDhcpServersAsync(int waitMs = 3000)
    {
        var servers = new HashSet<IPAddress>();
        byte[] xid = { 0x39, 0x03, 0xF3, 0x26 };
        byte[] discover = BuildDiscover(xid);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.EnableBroadcast = true;
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, 68));
        }
        catch (SocketException)
        {
            return null; // 68 포트 점유 → 프로브 불가
        }

        await udp.SendAsync(discover, discover.Length, new IPEndPoint(IPAddress.Broadcast, 67));

        using var cts = new CancellationTokenSource(waitMs);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var recv = await udp.ReceiveAsync(cts.Token);
                if (IsDhcpOffer(recv.Buffer, xid))
                    servers.Add(recv.RemoteEndPoint.Address);
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException) { }

        return servers;
    }

    private static byte[] BuildDiscover(byte[] xid)
    {
        var p = new byte[300];
        p[0] = 0x01;            // op = BOOTREQUEST
        p[1] = 0x01;            // htype = Ethernet
        p[2] = 0x06;            // hlen
        Array.Copy(xid, 0, p, 4, 4);   // transaction id
        p[10] = 0x80;           // flags = broadcast
        // chaddr(28~) 은 임의 MAC
        byte[] mac = { 0x00, 0x16, 0x3E, 0x12, 0x34, 0x56 };
        Array.Copy(mac, 0, p, 28, 6);
        // magic cookie
        p[236] = 0x63; p[237] = 0x82; p[238] = 0x53; p[239] = 0x63;
        // option 53: DHCP Message Type = DISCOVER(1)
        p[240] = 53; p[241] = 1; p[242] = 1;
        // option 55: parameter request list (subnet, router, dns)
        p[243] = 55; p[244] = 3; p[245] = 1; p[246] = 3; p[247] = 6;
        p[248] = 0xFF;          // end
        return p;
    }

    private static bool IsDhcpOffer(byte[] buf, byte[] xid)
    {
        if (buf.Length < 244) return false;
        if (buf[0] != 0x02) return false;                  // op = BOOTREPLY
        for (int i = 0; i < 4; i++) if (buf[4 + i] != xid[i]) return false;
        // 옵션에서 메시지 타입 53 == 2(OFFER) 확인
        for (int i = 240; i < buf.Length - 2;)
        {
            byte opt = buf[i];
            if (opt == 0xFF) break;
            if (opt == 0x00) { i++; continue; }
            byte len = buf[i + 1];
            if (opt == 53 && len == 1) return buf[i + 2] == 2;
            i += 2 + len;
        }
        return false;
    }
}
