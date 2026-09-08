using System.IO;
using System.Text.Json;
using kparser2.Abstractions;
using kparser2.Core;
using kparser2.Ingest;

namespace kparser2.Services;

public sealed class SessionCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _liveCancellation;
    private Task? _monitor;
    private DesktopPacketSource? _source;
    private string? _uuid;
    private string? _endedUuid;
    private bool _everConnected;
    private long _lastReceived;
    private CaptureState? _capture;
    private int _sessionGeneration;
    private DateTimeOffset _lastResetUtc;
    private string _boundarySessionUuid = "";
    private ulong _afterMessageId;
    private string _boundaryMode = "none";
    private string _boundaryQuality = "unavailable";
    private string _boundaryReason = "no reset boundary";
    public string CaptureDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "kparser2", "captures");
    public IAnalyticsSession Session { get; private set; } = PacketSessionFactory.fromSnapshot(new AnalyticsSnapshotDto());
    public string Mode { get; private set; } = "Waiting";
    public string Status { get; private set; } = "Waiting for kpacket on localhost:5556";
    public string? CapturePath => _capture?.Path;
    public int SessionGeneration => _sessionGeneration;
    public DateTimeOffset LastResetUtc => _lastResetUtc;
    public string SessionUuid => _uuid ?? "";
    public ulong LastMessageId => _source?.LastMessageId ?? 0UL;
    public string BoundarySessionUuid => _boundarySessionUuid;
    public ulong BoundaryMessageId => _afterMessageId;
    public string BoundaryMode => _boundaryMode;
    public string BoundaryQuality => _boundaryQuality;
    public string BoundaryReason => _boundaryReason;
    public event Action? Changed;
    public event Action? SessionChanged;

    private string RecoveryPath => Path.Combine(CaptureDirectory, "current.json");
    public sealed class CaptureState
    {
        public string Path { get; set; } = "";
        public string Uuid { get; set; } = "";
        public bool Complete { get; set; }
        public List<DateTimeOffset> Gaps { get; set; } = [];
    }

    public async Task StartLiveAsync(
        bool fresh = false,
        string? boundarySessionUuid = null,
        ulong afterMessageId = 0UL,
        string boundaryMode = "none",
        string boundaryQuality = "unavailable",
        string boundaryReason = "")
    {
        await StopAsync();
        Mode = "Live";
        Status = "Waiting for kpacket on localhost:5556 — load kpacket in Ashita";
        _capture = null;
        _endedUuid = null;
        _boundarySessionUuid = boundarySessionUuid ?? "";
        _afterMessageId = afterMessageId;
        _boundaryMode = boundaryMode;
        _boundaryQuality = boundaryQuality;
        _boundaryReason = boundaryReason;
        if (!fresh)
        {
            try
            {
                var saved = JsonSerializer.Deserialize<CaptureState>(await File.ReadAllTextAsync(RecoveryPath));
                if (saved is { Complete: false } && File.Exists(saved.Path)) _capture = saved;
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        }
        ReplaceSession(PacketSessionFactory.fromSnapshot(new AnalyticsSnapshotDto()));
        _sessionGeneration++;
        _lastResetUtc = DateTimeOffset.UtcNow;
        _liveCancellation = new CancellationTokenSource();
        _monitor = MonitorAsync(_liveCancellation.Token);
        Changed?.Invoke();
    }

    public Task ResetLiveAsync(
        string sessionUuid,
        ulong afterMessageId,
        string boundaryMode,
        string boundaryQuality,
        string boundaryReason) =>
        StartLiveAsync(
            true,
            sessionUuid,
            afterMessageId,
            boundaryMode,
            boundaryQuality,
            boundaryReason);

    private async Task MonitorAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var hello = await Task.Run(ConnectionProbe.helloInfo, ct);
                    ct.ThrowIfCancellationRequested();
                    if (hello is null || string.IsNullOrWhiteSpace(hello.Value.session_uuid))
                    {
                        Status = _everConnected ? "Reconnecting — capture preserved; packets may be missed" : "Waiting for kpacket on localhost:5556";
                    }
                    else
                    {
                        var uuid = hello.Value.session_uuid;
                        if (_source?.Ended == true)
                        {
                            _endedUuid = _uuid;
                            if (_capture is not null) { _capture.Complete = true; SaveRecovery(); }
                        }
                        if (uuid == _endedUuid)
                        {
                            Status = "Game session ended — waiting for the next session";
                        }
                        else
                        {
                            if (_uuid != uuid)
                            {
                                await ConnectAsync(uuid, ct);
                                _everConnected = true;
                            }
                            var count = _source?.ReceivedPackets ?? 0;
                            Status = _source?.IsRestoring == true
                                ? $"Restoring {_source.RecoveredPackets:N0} packets; capturing live continuation"
                                : count != _lastReceived ? $"Receiving — {count:N0} new packets" : "Connected — waiting for packets";
                            _lastReceived = count;
                            if (_capture?.Gaps.Count > 0) Status += " | Capture contains connection/restart gaps";
                            if (!string.IsNullOrEmpty(_source?.Error)) Status += " | " + _source.Error;
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex) { Status = "Connection error — retrying: " + ex.Message; }
                Changed?.Invoke();
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private async Task ConnectAsync(string uuid, CancellationToken ct)
    {
        var resume = _capture is { Complete: false } && _capture.Uuid == uuid && File.Exists(_capture.Path);
        if (!resume)
            _capture = new CaptureState { Uuid = uuid, Path = Path.Combine(CaptureDirectory, $"capture-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.ndjson") };
        else _capture!.Gaps.Add(DateTimeOffset.UtcNow);
        await Task.Run(() => Session.Dispose(), ct);
        var exactBoundary = _boundaryMode == "exact" && uuid == _boundarySessionUuid;
        var boundary = exactBoundary ? _afterMessageId : 0UL;
        _source = new DesktopPacketSource(
            new LivePacketSource("tcp://localhost:5555"),
            _capture!.Path,
            resume,
            uuid,
            boundary,
            exactBoundary,
            _boundaryMode,
            _boundaryQuality,
            _boundaryReason);
        Session = PacketSessionFactory.fromDesktopSource(_source);
        _uuid = uuid;
        _lastReceived = 0;
        SaveRecovery();
        SessionChanged?.Invoke();
    }

    private void SaveRecovery()
    {
        try
        {
            Directory.CreateDirectory(CaptureDirectory);
            File.WriteAllText(RecoveryPath + ".tmp", JsonSerializer.Serialize(_capture));
            File.Move(RecoveryPath + ".tmp", RecoveryPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Status = "Recovery metadata could not be saved: " + ex.Message; }
    }

    public async Task OpenCaptureAsync(string path)
    {
        await StopAsync();
        Mode = "Replay";
        Status = "Loading " + path;
        Changed?.Invoke();
        var session = await Task.Run(() =>
        {
            var replay = PacketSessionFactory.fromReplayDefault(path);
            replay.WaitForReplayComplete();
            return replay;
        });
        ReplaceSession(session);
        Status = "Replay — " + path;
        Changed?.Invoke();
    }

    public async Task OpenReportAsync(string path)
    {
        await StopAsync();
        IReportImporter importer = new FileReportImporter();
        var snapshot = await Task.Run(() => importer.ImportAsync(path));
        ReplaceSession(PacketSessionFactory.fromSnapshot(snapshot));
        Mode = "Report";
        Status = "Saved report — " + path;
        Changed?.Invoke();
    }

    private void ReplaceSession(IAnalyticsSession session)
    {
        Session.Dispose();
        Session = session;
        SessionChanged?.Invoke();
    }

    private async Task StopAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _liveCancellation?.Cancel();
            if (_monitor is not null) await _monitor;
            _monitor = null;
            _liveCancellation?.Dispose();
            _liveCancellation = null;
            await Task.Run(() => Session.Dispose());
            _source = null;
            _uuid = null;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
