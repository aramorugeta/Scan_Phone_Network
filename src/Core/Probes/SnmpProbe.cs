using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ScanPhoneNetwork.Probes;

/// <summary>
/// SNMP v1 GET 으로 sysDescr(1.3.6.1.2.1.1.1.0) 를 읽어 모델/설명을 얻는다.
/// 프린터·복합기는 community "public" 으로 모델명을 대부분 노출한다.
/// </summary>
public static class SnmpProbe
{
    // sysDescr.0 GET 요청(community=public) 미리 인코딩한 BER 패킷
    private static readonly byte[] _sysDescrGet =
    {
        0x30, 0x29,
          0x02, 0x01, 0x00,                               // version v1
          0x04, 0x06, 0x70,0x75,0x62,0x6C,0x69,0x63,      // "public"
          0xA0, 0x1C,                                     // GetRequest PDU
            0x02, 0x04, 0x13,0x57,0x9B,0xDF,              // request-id
            0x02, 0x01, 0x00,                             // error-status
            0x02, 0x01, 0x00,                             // error-index
            0x30, 0x0E,                                   // varbind list
              0x30, 0x0C,                                 // varbind
                0x06, 0x08, 0x2B,0x06,0x01,0x02,0x01,0x01,0x01,0x00, // OID sysDescr.0
                0x05, 0x00,                               // NULL
    };

    public static async Task<string?> GetSysDescrAsync(IPAddress ip, int timeoutMs = 800)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork) return null;
        try
        {
            using var udp = new UdpClient(AddressFamily.InterNetwork);
            await udp.SendAsync(_sysDescrGet, _sysDescrGet.Length, new IPEndPoint(ip, 161));
            using var cts = new CancellationTokenSource(timeoutMs);
            var res = await udp.ReceiveAsync(cts.Token);
            return ExtractSysDescr(res.Buffer);
        }
        catch { return null; }
    }

    /// <summary>응답에서 sysDescr OID 다음의 OCTET STRING 값을 꺼낸다.</summary>
    private static string? ExtractSysDescr(byte[] buf)
    {
        // sysDescr.0 OID 패턴을 찾고, 그 뒤의 값(TLV) 을 읽는다.
        byte[] oid = { 0x06, 0x08, 0x2B, 0x06, 0x01, 0x02, 0x01, 0x01, 0x01, 0x00 };
        int idx = IndexOf(buf, oid);
        if (idx < 0) return null;
        int p = idx + oid.Length;
        if (p + 2 > buf.Length) return null;

        byte type = buf[p++];
        if (type != 0x04) return null;       // OCTET STRING 이어야 함
        int len = buf[p++];
        if ((len & 0x80) != 0)               // 장형 길이(0x81 nn)
        {
            int n = len & 0x7F;
            len = 0;
            for (int i = 0; i < n && p < buf.Length; i++) len = (len << 8) | buf[p++];
        }
        if (p + len > buf.Length) len = buf.Length - p;
        if (len <= 0) return null;

        string s = Encoding.UTF8.GetString(buf, p, len).Trim();
        // 첫 구분자(;,개행) 이전의 핵심 모델 부분만, 과도하게 길면 자른다
        int cut = s.IndexOfAny(new[] { ';', '\n', '\r' });
        if (cut > 0) s = s[..cut].Trim();
        return s.Length > 60 ? s[..60] : s;
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= hay.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }
}
