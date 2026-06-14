using Avalonia.Media;

namespace ScanPhoneNetwork.Gui;

/// <summary>DataGrid 한 행을 위한 표시용 래퍼.</summary>
public sealed class HostRow
{
    public HostRow(DiscoveredHost h)
    {
        Ip = h.Ip.ToString();
        Mac = h.Mac ?? "-";
        Category = CsvExporter.CategoryKo(h.Category);
        Confidence = h.Confidence;
        Vendor = h.Vendor ?? "";
        OpenPorts = string.Join(" ", h.OpenPorts);
        Evidence = string.Join("  |  ", h.Evidence);
        Source = h;
        IsSuspicious = h.Category is DeviceCategory.Router
                                  or DeviceCategory.WirelessAp
                                  or DeviceCategory.VoipPhone;
    }

    public string Ip { get; }
    public string Mac { get; }
    public string Category { get; }
    public int Confidence { get; }
    public string Vendor { get; }
    public string OpenPorts { get; }
    public string Evidence { get; }
    public bool IsSuspicious { get; }
    public DiscoveredHost Source { get; }

    /// <summary>의심 장비는 빨간 계열로 강조.</summary>
    public IBrush RowBackground => IsSuspicious
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xE0))
        : Brushes.Transparent;
}
