using FluentAssertions;
using JD.AI.Core.Agents;
using Xunit;

namespace JD.AI.Tests.Agents;

/// <summary>
/// Unit tests for <see cref="AgentLoop.ExtractFirstToolCallJson"/>.
/// Verifies that JSON tool calls are detected in various response formats
/// produced by small models that emit tool calls as plain text.
/// </summary>
public sealed class AgentLoopTextToolCallTests
{
    // ── Bare JSON ────────────────────────────────────────────────────────

    [Fact]
    public void ExtractFirstToolCallJson_BareJson_ReturnsJson()
    {
        const string Response = """
            {
              "name": "shell-run_command",
              "arguments": { "command": "pwd" }
            }
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"name\"");
        result.Should().Contain("\"shell-run_command\"");
    }

    // ── Fenced code blocks ───────────────────────────────────────────────

    [Fact]
    public void ExtractFirstToolCallJson_FencedJsonBlock_ReturnsJson()
    {
        const string Response = """
            ```json
            {
              "name": "environment-get_environment",
              "arguments": { "includeEnvVars": false }
            }
            ```
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"environment-get_environment\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_FencedBlockNoLang_ReturnsJson()
    {
        const string Response = """
            ```
            { "name": "shell-run_command", "arguments": { "command": "ls" } }
            ```
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"shell-run_command\"");
    }

    // ── Duplicate / multiple blocks ──────────────────────────────────────

    [Fact]
    public void ExtractFirstToolCallJson_TwoIdenticalFencedBlocks_ReturnsFirst()
    {
        // Small models sometimes emit the same tool call twice in one response
        const string Response = """
            ```json
            { "name": "toolDiscovery-discover_tools", "arguments": { "page": 1 } }
            ```
            ```json
            { "name": "toolDiscovery-discover_tools", "arguments": { "page": 1 } }
            ```
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"toolDiscovery-discover_tools\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_ProseAroundJson_ReturnsJson()
    {
        const string Response = """
            I'll use the shell tool to run that command.
            ```json
            { "name": "shell-run_command", "arguments": { "command": "pwd", "cwd": "" } }
            ```
            This will show the current working directory.
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"shell-run_command\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_TaggedToolCallBlock_ReturnsJson()
    {
        const string Response = """
            Let me check.
            <tool_call> {"name": "run_command", "parameters": {"command": "pwd"}} </tool_call>
            <tool_response> /home/user </tool_response>
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"run_command\"");
        result.Should().Contain("\"parameters\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_TaggedToolUseBlock_ReturnsJson()
    {
        const string Response = """
            I'll run ls for you.
            <tool_use>
            {"name":"run_command","arguments":{"command":"ls"}}
            </tool_use>
            <tool_response>{"output":""}</tool_response>
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"run_command\"");
        result.Should().Contain("\"arguments\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_BareJsonWithParameters_ReturnsJson()
    {
        const string Response = """
            { "name": "run_command", "parameters": { "command": "pwd" } }
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"run_command\"");
        result.Should().Contain("\"parameters\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_BareJsonArray_ReturnsJson()
    {
        const string Response = """
            [{"name":"file-read_file","arguments":{"path":"package.json"}}]
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"file-read_file\"");
    }

    // ── Not a tool call ──────────────────────────────────────────────────

    [Fact]
    public void ExtractFirstToolCallJson_PlainText_ReturnsNull()
    {
        const string Response = "The current directory is C:\\Users\\jd";

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFirstToolCallJson_JsonWithoutName_ReturnsNull()
    {
        const string Response = """{ "foo": "bar", "baz": 42 }""";

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFirstToolCallJson_JsonWithNameButNoArguments_ReturnsNull()
    {
        const string Response = """{ "name": "something", "params": {} }""";

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractFirstToolCallJson_NullOrEmpty_ReturnsNull()
    {
        AgentLoop.ExtractFirstToolCallJson(string.Empty).Should().BeNull();
        AgentLoop.ExtractFirstToolCallJson("   ").Should().BeNull();
    }

    // ── Balanced brace scanning ──────────────────────────────────────────

    [Fact]
    public void ExtractFirstToolCallJson_EmbeddedJsonInBraces_ReturnsToolCall()
    {
        // Response contains a non-tool-call JSON object first, then a tool call
        const string Response = """
            Here is some data: {"info": "hello"}.
            Tool call: {"name": "shell-run_command", "arguments": {"command": "echo hi"}}
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"shell-run_command\"");
    }

    [Fact]
    public void ExtractFirstToolCallJson_NestedArgumentsObject_ParsesCorrectly()
    {
        const string Response = """
            {
              "name": "fs-read_file",
              "arguments": {
                "path": "src/foo.cs",
                "options": { "encoding": "utf8" }
              }
            }
            """;

        var result = AgentLoop.ExtractFirstToolCallJson(Response);

        result.Should().NotBeNull();
        result.Should().Contain("\"fs-read_file\"");
    }

    [Fact]
    public void IsStandaloneToolCallPayload_BareArray_ReturnsTrue()
    {
        const string Response = """
            [{"name":"file-read_file","arguments":{"path":"package.json"}}]
            """;

        AgentLoop.IsStandaloneToolCallPayload(Response).Should().BeTrue();
    }

    [Fact]
    public void IsStandaloneToolCallPayload_EntireTaggedToolUse_ReturnsTrue()
    {
        const string Response = """
            <tool_use>
            {"name":"run_command","arguments":{"command":"pwd"}}
            </tool_use>
            """;

        AgentLoop.IsStandaloneToolCallPayload(Response).Should().BeTrue();
    }

    [Fact]
    public void IsStandaloneToolCallPayload_EntireFencedJson_ReturnsTrue()
    {
        const string Response = """
            ```json
            {"name":"run_command","arguments":{"command":"pwd"}}
            ```
            """;

        AgentLoop.IsStandaloneToolCallPayload(Response).Should().BeTrue();
    }

    [Fact]
    public void IsStandaloneToolCallPayload_EntireFencedJsonMissingArguments_ReturnsFalse()
    {
        const string Response = """
            ```json
            {"name":"run_command","params":{"command":"pwd"}}
            ```
            """;

        AgentLoop.IsStandaloneToolCallPayload(Response).Should().BeFalse();
    }

    [Fact]
    public void IsStandaloneToolCallPayload_ProsePlusTaggedCall_ReturnsFalse()
    {
        const string Response = """
            Let me check.
            <tool_call> {"name": "run_command", "arguments": {"command": "pwd"}} </tool_call>
            """;

        AgentLoop.IsStandaloneToolCallPayload(Response).Should().BeFalse();
    }
}
