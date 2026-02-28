using System.Diagnostics;
using System.Text.Json;
using JD.AI.Core.Channels;

namespace JD.AI.Channels.Signal;

/// <summary>
/// Signal channel adapter that bridges to signal-cli via JSON-RPC over stdin/stdout.
/// Requires signal-cli to be installed and registered with a phone number.
/// </summary>
public sealed class SignalChannel : IChannel
{
    private readonly string _signalCliPath;
    private readonly string _account;
    private Process? _daemon;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;

    public SignalChannel(string account, string? signalCliPath = null)
    {
        _account = account;
        _signalCliPath = signalCliPath ?? "signal-cli";
    }

    public string ChannelType => "signal";
    public string DisplayName => $"Signal ({_account})";
    public bool IsConnected => _daemon is not null && !_daemon.HasExited;

    public event Func<ChannelMessage, Task>? MessageReceived;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _daemon = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _signalCliPath,
                Arguments = $"-a {_account} jsonRpc",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _daemon.Start();
        _writer = _daemon.StandardInput;

        // Start reading messages in background
        _ = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);

        await Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        if (_daemon is not null && !_daemon.HasExited)
        {
            _daemon.Kill();
        }
        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(string conversationId, string content, CancellationToken ct = default)
    {
        if (_writer is null) throw new InvalidOperationException("Not connected.");

        var rpc = new
        {
            jsonrpc = "2.0",
            method = "send",
            id = Guid.NewGuid().ToString("N")[..8],
            @params = new
            {
                recipient = new[] { conversationId },
                message = content
            }
        };

        await _writer.WriteLineAsync(JsonSerializer.Serialize(rpc));
        await _writer.FlushAsync(ct);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_daemon is null) return;
        var reader = _daemon.StandardOutput;

        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (root.TryGetProperty("method", out var method)
                    && method.GetString() == "receive"
                    && root.TryGetProperty("params", out var p)
                    && p.TryGetProperty("envelope", out var envelope)
                    && envelope.TryGetProperty("dataMessage", out var data))
                {
                    var msg = new ChannelMessage
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        ChannelId = envelope.TryGetProperty("source", out var src)
                            ? src.GetString() ?? "unknown"
                            : "unknown",
                        SenderId = envelope.TryGetProperty("source", out var sender)
                            ? sender.GetString() ?? "unknown"
                            : "unknown",
                        SenderDisplayName = envelope.TryGetProperty("sourceName", out var name)
                            ? name.GetString()
                            : null,
                        Content = data.TryGetProperty("message", out var msg2)
                            ? msg2.GetString() ?? ""
                            : "",
                        Timestamp = DateTimeOffset.UtcNow
                    };

                    if (MessageReceived is not null)
                        await MessageReceived.Invoke(msg);
                }
            }
#pragma warning disable CA1031
            catch { /* skip malformed lines */ }
#pragma warning restore CA1031
        }
    }

    public ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _daemon?.Kill();
        _daemon?.Dispose();
        _writer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
