// Licensed under the MIT License.

using System.Text.Json;
using FluentAssertions;
using JD.AI.Core.Infrastructure;

namespace JD.AI.Tests.Infrastructure;

public sealed class JsonDefaultsTests
{
    private sealed record TestPerson(string FirstName, int Age, string? Nickname = null);

    [Fact]
    public void Options_UsesCamelCase()
    {
        var json = JsonDefaults.Serialize(new TestPerson("Alice", 30));

        json.Should().Contain("\"firstName\"");
        json.Should().Contain("\"age\"");
    }

    [Fact]
    public void Options_SkipsNullValues()
    {
        var json = JsonDefaults.Serialize(new TestPerson("Alice", 30));

        json.Should().NotContain("nickname");
    }

    [Fact]
    public void Options_IncludesNonNullValues()
    {
        var json = JsonDefaults.Serialize(new TestPerson("Alice", 30, "Ali"));

        json.Should().Contain("\"nickname\"");
        json.Should().Contain("Ali");
    }

    [Fact]
    public void Options_IsIndented()
    {
        var json = JsonDefaults.Serialize(new TestPerson("Bob", 25));

        json.Should().Contain("\n");
    }

    [Fact]
    public void Compact_IsNotIndented()
    {
        var json = JsonDefaults.SerializeCompact(new TestPerson("Bob", 25));

        json.Should().NotContain("\n");
    }

    [Fact]
    public void Deserialize_CaseInsensitive()
    {
        const string Json = """{"FirstName":"Charlie","Age":40}""";
        var person = JsonDefaults.Deserialize<TestPerson>(Json);

        person.Should().NotBeNull();
        person!.FirstName.Should().Be("Charlie");
        person.Age.Should().Be(40);
    }

    [Fact]
    public void Deserialize_CamelCase()
    {
        const string Json = """{"firstName":"Dana","age":22}""";
        var person = JsonDefaults.Deserialize<TestPerson>(Json);

        person.Should().NotBeNull();
        person!.FirstName.Should().Be("Dana");
    }

    [Fact]
    public void Options_SerializesEnumsAsStrings()
    {
        var json = JsonDefaults.Serialize(new { Status = DayOfWeek.Monday });

        json.Should().Contain("monday");
    }

    [Fact]
    public void Strict_DoesNotSkipNulls()
    {
        var json = JsonSerializer.Serialize(new TestPerson("Eve", 28), JsonDefaults.Strict);

        json.Should().Contain("null");
    }

    [Fact]
    public void DeserializeElement_Works()
    {
        var doc = JsonDocument.Parse("""{"firstName":"Frank","age":35}""");
        var person = JsonDefaults.Deserialize<TestPerson>(doc.RootElement);

        person.Should().NotBeNull();
        person!.FirstName.Should().Be("Frank");
    }
}
