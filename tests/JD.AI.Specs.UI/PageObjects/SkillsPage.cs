using Microsoft.Playwright;

namespace JD.AI.Specs.UI.PageObjects;

/// <summary>
/// Page object for the Skills page.
/// Route: /agents/skills
/// </summary>
public sealed class SkillsPage : BasePage
{
    public SkillsPage(IPage page, string baseUrl) : base(page, baseUrl) { }

    protected override string PagePath => "/agents/skills";

    // ── Navigation helpers ──
    public async Task NavigateToSkills() => await NavigateAsync();

    // ── Page header ──
    public ILocator PageHeading => Page.Locator("[data-testid='page-title']");
    public new ILocator RefreshButton => Page.Locator("[data-testid='refresh-button']");

    // ── Status filter tabs ──
    public ILocator FilterAll => Page.Locator("[data-testid='filter-all']");
    public ILocator FilterReady => Page.Locator("[data-testid='filter-ready']");
    public ILocator FilterNeedsSetup => Page.Locator("[data-testid='filter-needs-setup']");
    public ILocator FilterDisabled => Page.Locator("[data-testid='filter-disabled']");

    // ── Search ──
    public ILocator SearchBox => Page.Locator("[data-testid='skill-search'] input");

    // ── Skill cards ──
    public ILocator SkillCards => Page.Locator("[data-testid^='skill-card-']");
    public ILocator SkillCard(string skillId) => Page.Locator($"[data-testid='skill-card-{skillId}']");
    public ILocator SkillToggles => Page.Locator("[data-testid='skill-toggle']");
    public ILocator SkillConfigureButtons => Page.Locator("[data-testid='skill-configure']");

    // ── Empty state ──
    public ILocator EmptyState => Page.Locator("[data-testid='skills-empty']");

    // ── Configure dialog ──
    public ILocator ConfigDialog => Page.Locator(".mud-dialog");
    public ILocator ConfigSaveButton => Page.Locator("[data-testid='skill-config-save'] button");

    public ILocator ConfigField(string key) =>
        Page.Locator($"[data-testid='config-field-{key}'] input");

    // ── Snackbar ──
    public ILocator SnackbarWithText(string text) =>
        Page.Locator($".mud-snackbar >> text={text}");
}
