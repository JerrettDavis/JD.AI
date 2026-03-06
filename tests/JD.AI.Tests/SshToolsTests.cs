using JD.AI.Core.Tools;

namespace JD.AI.Tests;

public sealed class SshToolsTests : IDisposable
{
    private readonly SshTools _sshTools = new();

    public void Dispose() => _sshTools.Dispose();

    #region connect_ssh tests

    [Fact]
    public async Task ConnectSsh_WithInvalidHost_ReturnsError()
    {
        var result = await _sshTools.ConnectSshAsync(
            host: "nonexistent.invalid.host.local",
            username: "testuser",
            timeoutSeconds: 2);

        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task ConnectSsh_WithMissingKeyFile_ReturnsError()
    {
        var result = await _sshTools.ConnectSshAsync(
            host: "localhost",
            username: "testuser",
            privateKeyPath: "/nonexistent/path/to/key");

        Assert.Contains("Error", result);
        Assert.Contains("key", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConnectSsh_HasCorrectKernelFunctionAttribute()
    {
        var method = typeof(SshTools).GetMethod(nameof(SshTools.ConnectSshAsync));
        Assert.NotNull(method);

        var attr = method!.GetCustomAttributes(typeof(Microsoft.SemanticKernel.KernelFunctionAttribute), false);
        Assert.Single(attr);

        var kernelFunc = (Microsoft.SemanticKernel.KernelFunctionAttribute)attr[0];
        Assert.Equal("connect_ssh", kernelFunc.Name);
    }

    #endregion

    #region run_remote_command tests

    [Fact]
    public async Task RunRemoteCommand_WithoutConnection_ReturnsError()
    {
        var result = await _sshTools.RunRemoteCommandAsync("echo hello");

        Assert.Contains("Error", result);
        Assert.Contains("not connected", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunRemoteCommand_HasCorrectKernelFunctionAttribute()
    {
        var method = typeof(SshTools).GetMethod(nameof(SshTools.RunRemoteCommandAsync));
        Assert.NotNull(method);

        var attr = method!.GetCustomAttributes(typeof(Microsoft.SemanticKernel.KernelFunctionAttribute), false);
        Assert.Single(attr);

        var kernelFunc = (Microsoft.SemanticKernel.KernelFunctionAttribute)attr[0];
        Assert.Equal("run_remote_command", kernelFunc.Name);
    }

    #endregion

    #region disconnect_ssh tests

    [Fact]
    public async Task DisconnectSsh_WithoutConnection_ReturnsNotConnectedMessage()
    {
        var result = await _sshTools.DisconnectSshAsync();

        Assert.Contains("not connected", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisconnectSsh_HasCorrectKernelFunctionAttribute()
    {
        var method = typeof(SshTools).GetMethod(nameof(SshTools.DisconnectSshAsync));
        Assert.NotNull(method);

        var attr = method!.GetCustomAttributes(typeof(Microsoft.SemanticKernel.KernelFunctionAttribute), false);
        Assert.Single(attr);

        var kernelFunc = (Microsoft.SemanticKernel.KernelFunctionAttribute)attr[0];
        Assert.Equal("disconnect_ssh", kernelFunc.Name);
    }

    #endregion

    #region get_remote_info tests

    [Fact]
    public async Task GetRemoteInfo_WithoutConnection_ReturnsError()
    {
        var result = await _sshTools.GetRemoteInfoAsync();

        Assert.Contains("Error", result);
        Assert.Contains("not connected", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRemoteInfo_HasCorrectKernelFunctionAttribute()
    {
        var method = typeof(SshTools).GetMethod(nameof(SshTools.GetRemoteInfoAsync));
        Assert.NotNull(method);

        var attr = method!.GetCustomAttributes(typeof(Microsoft.SemanticKernel.KernelFunctionAttribute), false);
        Assert.Single(attr);

        var kernelFunc = (Microsoft.SemanticKernel.KernelFunctionAttribute)attr[0];
        Assert.Equal("get_remote_info", kernelFunc.Name);
    }

    #endregion

    #region Connection state tests

    [Fact]
    public void IsConnected_Initially_ReturnsFalse()
    {
        Assert.False(_sshTools.IsConnected);
    }

    [Fact]
    public void ConnectionInfo_Initially_ReturnsNull()
    {
        Assert.Null(_sshTools.CurrentHost);
        Assert.Null(_sshTools.CurrentUsername);
    }

    #endregion

    #region Integration tests (require real SSH server - skipped by default)

    [Fact(Skip = "Requires local SSH server - run manually")]
    public async Task Integration_ConnectToLocalhost_RequiresSSHServer()
    {
        var result = await _sshTools.ConnectSshAsync(
            host: "localhost",
            username: Environment.UserName);

        // If we get here, connection was attempted
        // Result depends on whether key auth is configured
        Assert.NotNull(result);
    }

    [Fact(Skip = "Requires local SSH server - run manually")]
    public async Task Integration_RunCommandOnLocalhost_RequiresSSHServer()
    {
        var connectResult = await _sshTools.ConnectSshAsync(
            host: "localhost",
            username: Environment.UserName);

        // Skip if connection failed
        if (connectResult.Contains("Error"))
        {
            return;
        }

        var result = await _sshTools.RunRemoteCommandAsync("echo hello");
        Assert.Contains("hello", result);

        await _sshTools.DisconnectSshAsync();
    }

    #endregion
}
