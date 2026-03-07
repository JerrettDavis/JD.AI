using JD.AI.Core.Agents;

namespace JD.AI.Tests;

public sealed class OutputSchemaValidatorTests
{
    [Fact]
    public void Validate_ValidObject_ReturnsNoErrors()
    {
        var schema = """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";
        var output = """{"name":"test"}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsError()
    {
        var schema = """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";
        var output = """{"age":42}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Single(errors);
        Assert.Contains("name", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WrongType_ReturnsError()
    {
        var schema = """{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}""";
        var output = """{"name":42}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Single(errors);
        Assert.Contains("string", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsError()
    {
        var schema = """{"type":"object"}""";
        var output = "not json at all";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Single(errors);
        Assert.Contains("not valid JSON", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_ArraySchema_ValidArray()
    {
        var schema = """{"type":"array","items":{"type":"object","properties":{"id":{"type":"number"}},"required":["id"]}}""";
        var output = """[{"id":1},{"id":2}]""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ArraySchema_MissingItemProperty()
    {
        var schema = """{"type":"array","items":{"type":"object","properties":{"id":{"type":"number"}},"required":["id"]}}""";
        var output = """[{"id":1},{"name":"no-id"}]""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Single(errors);
        Assert.Contains("[1]", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_BooleanType_Valid()
    {
        var schema = """{"type":"object","properties":{"active":{"type":"boolean"}},"required":["active"]}""";
        var output = """{"active":true}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ExpectedObjectGotArray_ReturnsError()
    {
        var schema = """{"type":"object"}""";
        var output = """[1,2,3]""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        Assert.Single(errors);
        Assert.Contains("expected object", errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public void LoadSchema_InlineJson_ReturnsSame()
    {
        var inline = """{"type":"string"}""";
        var result = OutputSchemaValidator.LoadSchema(inline);
        Assert.Equal(inline, result);
    }

    [Fact]
    public void LoadSchema_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            OutputSchemaValidator.LoadSchema("nonexistent-file.json"));
    }

    [Fact]
    public void GenerateRetryPrompt_IncludesErrors()
    {
        var errors = new List<string> { "missing required property 'name'" };
        var schema = """{"type":"object"}""";

        var prompt = OutputSchemaValidator.GenerateRetryPrompt(errors, schema);
        Assert.Contains("missing required property 'name'", prompt, StringComparison.Ordinal);
        Assert.Contains("JSON schema", prompt, StringComparison.Ordinal);
    }
}
