using System.Reflection;
using FluentAssertions;
using JD.AI.Rendering;
using Spectre.Console;

namespace JD.AI.Tests.Rendering;

public sealed class PromptPagingTests
{
    [Fact]
    public void SelectionPrompt_WithAdaptivePaging_ReturnsSamePrompt()
    {
        var prompt = new SelectionPrompt<string>();

        var result = prompt.WithAdaptivePaging(8, totalChoices: 3, singularNoun: "option");

        result.Should().BeSameAs(prompt);
    }

    [Fact]
    public void MultiSelectionPrompt_WithAdaptivePaging_ReturnsSamePrompt()
    {
        var prompt = new MultiSelectionPrompt<string>();

        var result = prompt.WithAdaptivePaging(8, totalChoices: 12, singularNoun: "option", pluralNoun: "options");

        result.Should().BeSameAs(prompt);
    }

    [Fact]
    public void SelectionPrompt_WithAdaptivePaging_NullPrompt_Throws()
    {
        SelectionPrompt<string>? prompt = null;
        var act = () => prompt!.WithAdaptivePaging(8, totalChoices: 3, singularNoun: "option");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateNouns_EmptySingular_Throws()
    {
        var act = () => InvokeNonPublic("ValidateNouns", "", null);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage("*singular noun*");
    }

    [Fact]
    public void ValidateNouns_WhitespacePlural_Throws()
    {
        var act = () => InvokeNonPublic("ValidateNouns", "option", " ");

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage("*Plural noun*");
    }

    [Fact]
    public void ResolvePageSize_ClampsToMinimum()
    {
        var result = (int)InvokeNonPublic("ResolvePageSize", 0)!;

        result.Should().Be(5);
    }

    [Fact]
    public void ResolvePageSize_ClampsToTerminalCapacity()
    {
        var capacity = (int)InvokeNonPublic("GetTerminalCapacity")!;
        var result = (int)InvokeNonPublic("ResolvePageSize", capacity + 100)!;

        result.Should().Be(capacity);
    }

    [Fact]
    public void ApplyOverflowText_NoHiddenChoices_DoesNotInvokeSetter()
    {
        string? rendered = null;

        InvokeApplyOverflowText(
            totalChoices: 5,
            pageSize: 5,
            singularNoun: "option",
            pluralNoun: "options",
            setMoreChoicesText: (_, text) => rendered = text);

        rendered.Should().BeNull();
    }

    [Fact]
    public void ApplyOverflowText_SingleHiddenChoice_UsesSingularNoun()
    {
        string? rendered = null;

        InvokeApplyOverflowText(
            totalChoices: 6,
            pageSize: 5,
            singularNoun: "option",
            pluralNoun: "options",
            setMoreChoicesText: (_, text) => rendered = text);

        rendered.Should().Be("[dim](1 more option)[/]");
    }

    [Fact]
    public void ApplyOverflowText_MultipleHiddenChoices_UsesProvidedPlural()
    {
        string? rendered = null;

        InvokeApplyOverflowText(
            totalChoices: 9,
            pageSize: 5,
            singularNoun: "entry",
            pluralNoun: "entries",
            setMoreChoicesText: (_, text) => rendered = text);

        rendered.Should().Be("[dim](4 more entries)[/]");
    }

    [Fact]
    public void ApplyOverflowText_MultipleHiddenChoices_UsesDefaultPluralSuffix()
    {
        string? rendered = null;

        InvokeApplyOverflowText(
            totalChoices: 8,
            pageSize: 5,
            singularNoun: "item",
            pluralNoun: null,
            setMoreChoicesText: (_, text) => rendered = text);

        rendered.Should().Be("[dim](3 more items)[/]");
    }

    private static object? InvokeNonPublic(string methodName, params object?[] args)
    {
        var method = typeof(PromptPaging).GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return method!.Invoke(null, args);
    }

    private static void InvokeApplyOverflowText(
        int totalChoices,
        int pageSize,
        string singularNoun,
        string? pluralNoun,
        Action<object, string> setMoreChoicesText)
    {
        var method = typeof(PromptPaging).GetMethod(
            "ApplyOverflowText",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var generic = method!.MakeGenericMethod(typeof(object));
        generic.Invoke(
            null,
            [
                new object(),
                totalChoices,
                pageSize,
                singularNoun,
                pluralNoun,
                setMoreChoicesText,
            ]);
    }
}
