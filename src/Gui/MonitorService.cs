using System.IO;

namespace ScanPhoneNetwork.Gui;

/// <summary>
/// 주기적으로 스캔을 돌려 업무망을 감시한다.
/// 승인 목록(AllowList)에 없는 의심 장비가 새로 나타나면 NewSuspicious 이벤트 발생.
/// </summary>
public sealed class MonitorService
{
    private readonly AllowList _allow;
    private readonly string _allowPath;
    private readonly HashSet<string> _alerted = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;

    public string? TargetCidr { get; set; }
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>스캔 1회 완료 시(결과 전체).</summary>
    public event Action<ScanReport>? ScanCompleted;
    /// <summary>새 의심 장비 발견 시(경고 대상).</summary>
    public event Action<DiscoveredHost>? NewSuspicious;
    public event Action<string>? StatusChanged;

    public MonitorService(string allowListPath)
    {
        _allowPath = allowListPath;
        _allow = AllowList.Load(allowListPath);
    }

    public AllowList AllowList => _allow;
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Approve(DiscoveredHost h)
    {
        if (h.Mac is not null)
        {
            _allow.Approve(h.Mac);
            _allow.Save(_allowPath);
            _alerted.Remove(Key(h));
        }
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        StatusChanged?.Invoke("감시 중지됨");
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var scanner = new Scanner();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                StatusChanged?.Invoke($"스캔 시작 {DateTime.Now:HH:mm:ss}");
                var report = await scanner.RunAsync(TargetCidr, null, ct);
                ScanCompleted?.Invoke(report);

                foreach (var h in report.Suspicious)
                {
                    if (_allow.IsApproved(h.Mac)) continue;
                    if (_alerted.Add(Key(h)))
                        NewSuspicious?.Invoke(h);
                }
                StatusChanged?.Invoke(
                    $"감시 중 · 마지막 {DateTime.Now:HH:mm:ss} · 의심 {report.Suspicious.Count()}대");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { StatusChanged?.Invoke("오류: " + ex.Message); }

            try { await Task.Delay(Interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static string Key(DiscoveredHost h) => h.Mac ?? h.Ip.ToString();
}
