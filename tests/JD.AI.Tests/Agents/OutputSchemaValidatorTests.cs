using FluentAssertions;
using JD.AI.Core.Agents;

namespace JD.AI.Tests.Agents;

public sealed class OutputSchemaValidatorTests
{
    // ── Valid output ────────────────────────────────────────────────────

    [Fact]
    public void Validate_ValidObjectWithRequiredProps_NoErrors()
    {
        var schema = """{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}""";
        var output = """{"name":"test"}""";

        OutputSchemaValidator.Validate(output, schema).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidArray_NoErrors()
    {
        var schema = """{"type":"array","items":{"type":"string"}}""";
        var output = """["a","b","c"]""";

        OutputSchemaValidator.Validate(output, schema).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidNumber_NoErrors()
    {
        var schema = """{"type":"number"}""";
        var output = "42";

        OutputSchemaValidator.Validate(output, schema).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidBoolean_NoErrors()
    {
        var schema = """{"type":"boolean"}""";
        var output = "true";

        OutputSchemaValidator.Validate(output, schema).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidString_NoErrors()
    {
        var schema = """{"type":"string"}""";
        var output = "\"hello\"";

        OutputSchemaValidator.Validate(output, schema).Should().BeEmpty();
    }

    // ── Invalid output ─────────────────────────────────────────────────

    [Fact]
    public void Validate_InvalidJson_ReturnsError()
    {
        var schema = """{"type":"object"}""";
        var output = "not json at all";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("not valid JSON");
    }

    [Fact]
    public void Validate_InvalidSchema_ReturnsError()
    {
        var schema = "not a schema";
        var output = """{"name":"test"}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("Schema is not valid JSON");
    }

    [Fact]
    public void Validate_SchemaNotObject_ReturnsError()
    {
        var schema = "[1,2,3]";
        var output = """{"name":"test"}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("must be a JSON object");
    }

    [Fact]
    public void Validate_MissingRequiredProperty_ReturnsError()
    {
        var schema = """{"type":"object","required":["name","age"],"properties":{"name":{"type":"string"},"age":{"type":"integer"}}}""";
        var output = """{"name":"test"}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("missing required property 'age'");
    }

    [Fact]
    public void Validate_WrongType_ReturnsError()
    {
        var schema = """{"type":"object","properties":{"name":{"type":"string"}}}""";
        var output = """{"name":42}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("expected string");
    }

    [Fact]
    public void Validate_ExpectedObjectGotArray_ReturnsError()
    {
        var schema = """{"type":"object"}""";
        var output = "[1,2,3]";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("expected object");
    }

    [Fact]
    public void Validate_ExpectedArrayGotObject_ReturnsError()
    {
        var schema = """{"type":"array"}""";
        var output = """{"key":"val"}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("expected array");
    }

    [Fact]
    public void Validate_ArrayItemTypeMismatch_ReturnsErrors()
    {
        var schema = """{"type":"array","items":{"type":"number"}}""";
        var output = """[1,"not a number",3]""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("expected number");
    }

    [Fact]
    public void Validate_NestedObject_ValidatesRecursively()
    {
        var schema = """
            {"type":"object","properties":{
                "user":{"type":"object","required":["email"],"properties":{
                    "email":{"type":"string"}
                }}
            }}
            """;
        var output = """{"user":{}}""";

        var errors = OutputSchemaValidator.Validate(output, schema);
        errors.Should().ContainSingle();
        errors[0].Should().Contain("email");
    }

    // ── LoadSchema ─────────────────────────────────────────────────────

    [Fact]
    public void LoadSchema_InlineJson_ReturnsAsIs()
    {
        var json = """{"type":"object"}""";
        OutputSchemaValidator.LoadSchema(json).Should().Be(json);
    }

    [Fact]
    public void LoadSchema_InlineJsonWithWhitespace_ReturnsAsIs()
    {
        var json = "  { \"type\": \"object\" }";
        OutputSchemaValidator.LoadSchema(json).Should().Be(json);
    }

    [Fact]
    public void LoadSchema_FileNotFound_Throws()
    {
        var act = () => OutputSchemaValidator.LoadSchema("/nonexistent/schema.json");
        act.Should().Throw<FileNotFoundException>();
    }

    // ── GenerateRetryPrompt ────────────────────────────────────────────

    [Fact]
    public void GenerateRetryPrompt_IncludesErrors()
    {
        var errors = new List<string> { "missing 'name'", "wrong type for 'age'" };
        var schema = """{"type":"object"}""";

        var prompt = OutputSchemaValidator.GenerateRetryPrompt(errors, schema);

        prompt.Should().Contain("missing 'name'");
        prompt.Should().Contain("wrong type for 'age'");
        prompt.Should().Contain(schema);
    }

    [Fact]
    public void GenerateRetryPrompt_IncludesSchema()
    {
        var schema = """{"type":"array","items":{"type":"string"}}""";
        var prompt = OutputSchemaValidator.GenerateRetryPrompt(["error"], schema);

        prompt.Should().Contain(schema);
    }

    // ── ExitCode constant ──────────────────────────────────────────────

    [Fact]
    public void SchemaValidationExitCode_Is3()
    {
        OutputSchemaValidator.SchemaValidationExitCode.Should().Be(3);
    }
}
