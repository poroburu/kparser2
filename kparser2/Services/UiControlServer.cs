using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace kparser2.Services;

public sealed class UiControlServer : IDisposable
{
    private readonly string _service;
    private readonly string _descriptorPath;
    private readonly string _token;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Func<string> _status;
    private readonly Func<string, string, string, string, string, string, string> _reset;
    private Task? _acceptLoop;

    public UiControlServer(
        string service,
        string descriptorPath,
        Func<string> status,
        Func<string, string, string, string, string, string, string> reset)
    {
        _service = service;
        _descriptorPath = descriptorPath;
        _status = status;
        _reset = reset;
        _token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _listener = new TcpListener(IPAddress.Loopback, 0);
    }

    public int Port { get; private set; }

    public void Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        WriteDescriptor();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                _ = Task.Run(() => HandleClientAsync(client), _shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, leaveOpen: true))
        {
            try
            {
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine))
                    return;

                var authorization = "";
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    var separator = line.IndexOf(':');
                    if (separator > 0 &&
                        line[..separator].Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        authorization = line[(separator + 1)..].Trim();
                    }
                }

                var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    await WriteResponseAsync(stream, 400, """{"ok":false,"error":"bad request"}""");
                    return;
                }

                if (!authorization.Equals("Bearer " + _token, StringComparison.Ordinal))
                {
                    await WriteResponseAsync(stream, 401, """{"ok":false,"error":"unauthorized"}""");
                    return;
                }

                var method = parts[0].ToUpperInvariant();
                var target = parts[1];
                var path = target.Split('?', 2)[0];
                var resetId = QueryValue(target, "reset_id") ?? "";
                var sessionUuid = QueryValue(target, "session_uuid") ?? "";
                var afterMessageId = QueryValue(target, "after_message_id") ?? "0";
                var boundaryMode = QueryValue(target, "boundary_mode") ?? "degraded";
                var boundaryQuality = QueryValue(target, "boundary_quality") ?? "unavailable";
                var boundaryReason = QueryValue(target, "boundary_reason") ?? "";

                if (method == "GET" && path == "/status")
                {
                    await WriteResponseAsync(stream, 200, _status());
                }
                else if (method == "POST" && path == "/reset")
                {
                    await WriteResponseAsync(
                        stream,
                        200,
                        _reset(
                            resetId,
                            sessionUuid,
                            afterMessageId,
                            boundaryMode,
                            boundaryQuality,
                            boundaryReason));
                }
                else
                {
                    await WriteResponseAsync(stream, 404, """{"ok":false,"error":"not found"}""");
                }
            }
            catch (Exception ex)
            {
                await WriteResponseAsync(stream, 500, JsonError(ex.Message));
            }
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, string body)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var reason = status == 200 ? "OK" :
            status == 401 ? "Unauthorized" :
            status == 404 ? "Not Found" :
            status == 500 ? "Internal Server Error" : "Bad Request";
        var header =
            $"HTTP/1.1 {status} {reason}\r\n" +
            "Content-Type: application/json\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes);
        await stream.WriteAsync(payload);
    }

    private static string? QueryValue(string target, string name)
    {
        var queryStart = target.IndexOf('?');
        if (queryStart < 0)
            return null;

        foreach (var pair in target[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = pair.Split('=', 2);
            if (pieces.Length == 2 &&
                pieces[0].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return Uri.UnescapeDataString(pieces[1]);
            }
        }

        return null;
    }

    private static string JsonError(string message) =>
        $"{{\"ok\":false,\"error\":\"{JsonEscape(message)}\"}}";

    private static string JsonEscape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n");

    private void WriteDescriptor()
    {
        var directory = Path.GetDirectoryName(_descriptorPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var descriptor =
            "{" +
            $"\"schema_version\":1,\"service\":\"{JsonEscape(_service)}\"," +
            $"\"pid\":{Environment.ProcessId},\"port\":{Port}," +
            $"\"base_url\":\"http://127.0.0.1:{Port}\"," +
            $"\"token\":\"{_token}\",\"started_at_utc\":\"{DateTimeOffset.UtcNow:O}\"" +
            "}";
        File.WriteAllText(_descriptorPath, descriptor, new UTF8Encoding(false));
    }

    public void Dispose()
    {
        if (_shutdown.IsCancellationRequested)
            return;

        _shutdown.Cancel();
        _listener.Stop();
        try { _acceptLoop?.GetAwaiter().GetResult(); } catch { }
        try { File.Delete(_descriptorPath); } catch { }
        _shutdown.Dispose();
    }
}
