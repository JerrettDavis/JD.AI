using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using Renci.SshNet;

namespace JD.AI.Core.Tools;

/// <summary>
/// SSH remote execution tools for the AI agent.
/// Allows connecting to remote machines and executing commands.
/// </summary>
public sealed class SshTools : IDisposable
{
    private SshClient? _client;
    private string? _currentHost;
    private string? _currentUsername;
    private bool _disposed;

    /// <summary>
    /// Gets whether there is an active SSH connection.
    /// </summary>
    public bool IsConnected => _client?.IsConnected == true;

    /// <summary>
    /// Gets the current connected host, or null if not connected.
    /// </summary>
    public string? CurrentHost => IsConnected ? _currentHost : null;

    /// <summary>
    /// Gets the current username, or null if not connected.
    /// </summary>
    public string? CurrentUsername => IsConnected ? _currentUsername : null;

    [KernelFunction("connect_ssh")]
    [Description("Establish an SSH connection to a remote host. Supports key-based auth (default) or password auth.")]
    public Task<string> ConnectSshAsync(
        [Description("Hostname or IP address to connect to (Tailscale names work)")] string host,
        [Description("Username for SSH authentication")] string username,
        [Description("Password for auth (optional, uses key auth if not provided)")] string? password = null,
        [Description("Path to private key file (default: ~/.ssh/id_rsa)")] string? privateKeyPath = null,
        [Description("SSH port (default: 22)")] int port = 22,
        [Description("Connection timeout in seconds (default: 30)")] int timeoutSeconds = 30)
    {
        try
        {
            // Disconnect existing connection if any
            DisconnectInternal();

            var authMethods = new List<AuthenticationMethod>();

            // Try password auth if provided
            if (!string.IsNullOrEmpty(password))
            {
                authMethods.Add(new PasswordAuthenticationMethod(username, password));
            }

            // Try key-based auth
            var keyPath = privateKeyPath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".ssh",
                "id_rsa");

            if (File.Exists(keyPath))
            {
                try
                {
                    var keyFile = new PrivateKeyFile(keyPath);
                    authMethods.Add(new PrivateKeyAuthenticationMethod(username, keyFile));
                }
                catch (Exception ex)
                {
                    // If key auth was explicitly requested but failed, report error
                    if (privateKeyPath != null)
                    {
                        return Task.FromResult($"Error: Failed to load private key at '{keyPath}': {ex.Message}");
                    }
                    // Otherwise, continue with other auth methods
                }
            }
            else if (privateKeyPath != null)
            {
                // Explicitly requested key path doesn't exist
                return Task.FromResult($"Error: Private key file not found at '{privateKeyPath}'");
            }

            if (authMethods.Count == 0)
            {
                return Task.FromResult("Error: No authentication method available. Provide a password or ensure SSH key exists at ~/.ssh/id_rsa");
            }

            var connectionInfo = new ConnectionInfo(host, port, username, authMethods.ToArray())
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
            };

            _client = new SshClient(connectionInfo);
            _client.Connect();

            _currentHost = host;
            _currentUsername = username;

            return Task.FromResult($"Connected to {username}@{host}:{port}");
        }
        catch (Exception ex)
        {
            DisconnectInternal();
            return Task.FromResult($"Error: Failed to connect to {host}: {ex.Message}");
        }
    }

    [KernelFunction("run_remote_command")]
    [Description("Execute a command on the connected remote host and return its output.")]
    public Task<string> RunRemoteCommandAsync(
        [Description("The command to execute on the remote host")] string command,
        [Description("Timeout in seconds (default: 60)")] int timeoutSeconds = 60)
    {
        if (!IsConnected || _client == null)
        {
            return Task.FromResult("Error: Not connected to any SSH host. Use connect_ssh first.");
        }

        try
        {
            using var cmd = _client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);

            var result = cmd.Execute();
            var stderr = cmd.Error;
            var exitCode = cmd.ExitStatus;

            var output = new StringBuilder();
            output.AppendLine($"Exit code: {exitCode}");

            if (!string.IsNullOrEmpty(result))
            {
                output.AppendLine("--- stdout ---");
                output.Append(result);
            }

            if (!string.IsNullOrEmpty(stderr))
            {
                output.AppendLine("--- stderr ---");
                output.Append(stderr);
            }

            // Truncate very long output
            const int maxLength = 10000;
            var outputStr = output.ToString();
            if (outputStr.Length > maxLength)
            {
                outputStr = string.Concat(outputStr.AsSpan(0, maxLength), $"\n... [truncated, {outputStr.Length - maxLength} more chars]");
            }

            return Task.FromResult(outputStr);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: Command execution failed: {ex.Message}");
        }
    }

    [KernelFunction("disconnect_ssh")]
    [Description("Close the current SSH connection.")]
    public Task<string> DisconnectSshAsync()
    {
        if (!IsConnected)
        {
            return Task.FromResult("Not connected to any SSH host.");
        }

        var host = _currentHost;
        DisconnectInternal();
        return Task.FromResult($"Disconnected from {host}");
    }

    [KernelFunction("get_remote_info")]
    [Description("Get information about the connected remote system (OS, hostname, etc.).")]
    public async Task<string> GetRemoteInfoAsync()
    {
        if (!IsConnected || _client == null)
        {
            return "Error: Not connected to any SSH host. Use connect_ssh first.";
        }

        try
        {
            var info = new StringBuilder();
            info.AppendLine($"Connected to: {_currentUsername}@{_currentHost}");
            info.AppendLine();

            // Get hostname
            var hostnameResult = await RunRemoteCommandAsync("hostname", timeoutSeconds: 5);
            if (!hostnameResult.Contains("Error"))
            {
                var hostname = ExtractStdout(hostnameResult);
                info.AppendLine($"Hostname: {hostname}");
            }

            // Get OS info (works on Linux/macOS)
            var unameResult = await RunRemoteCommandAsync("uname -a", timeoutSeconds: 5);
            if (!unameResult.Contains("Error"))
            {
                var uname = ExtractStdout(unameResult);
                info.AppendLine($"System: {uname}");
            }

            // Try to get more detailed OS info
            var osReleaseResult = await RunRemoteCommandAsync("cat /etc/os-release 2>/dev/null | head -5 || sw_vers 2>/dev/null", timeoutSeconds: 5);
            if (!osReleaseResult.Contains("Error"))
            {
                var osRelease = ExtractStdout(osReleaseResult);
                if (!string.IsNullOrWhiteSpace(osRelease))
                {
                    info.AppendLine($"OS Details:\n{osRelease}");
                }
            }

            // Get uptime
            var uptimeResult = await RunRemoteCommandAsync("uptime", timeoutSeconds: 5);
            if (!uptimeResult.Contains("Error"))
            {
                var uptime = ExtractStdout(uptimeResult);
                info.AppendLine($"Uptime: {uptime}");
            }

            return info.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Error: Failed to get remote info: {ex.Message}";
        }
    }

    private static string ExtractStdout(string commandResult)
    {
        // Extract content between "--- stdout ---" and either "--- stderr ---" or end
        var lines = commandResult.Split('\n');
        var inStdout = false;
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.Contains("--- stdout ---"))
            {
                inStdout = true;
                continue;
            }

            if (line.Contains("--- stderr ---"))
            {
                break;
            }

            if (inStdout)
            {
                result.AppendLine(line);
            }
        }

        return result.ToString().Trim();
    }

    private void DisconnectInternal()
    {
        if (_client != null)
        {
            if (_client.IsConnected)
            {
                _client.Disconnect();
            }

            _client.Dispose();
            _client = null;
        }

        _currentHost = null;
        _currentUsername = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisconnectInternal();
            _disposed = true;
        }
    }
}
