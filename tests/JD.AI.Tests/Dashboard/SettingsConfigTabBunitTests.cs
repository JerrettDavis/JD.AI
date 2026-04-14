using System.Net;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using JD.AI.Dashboard.Wasm.Components;
using JD.AI.Dashboard.Wasm.Models;
using JD.AI.Dashboard.Wasm.Services;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace JD.AI.Tests.Dashboard;

public class SettingsConfigTabBunitTests : DashboardBunitTestContext
{
    private static readonly string SchemaJson = JsonSerializer.Serialize(new
    {
        sections = new object[]
        {
            new
            {
                key = "updates",
                label = "Updates",
                category = "system",
                fields = new object[]
                {
                    new { key = "autoUpdateEnabled", label = "Auto Update Enabled", description = "Enable automatic updates", type = "boolean", category = (string?)null, enumValues = (string[]?)null, defaultValue = false, sensitive = false },
                    new { key = "updateChannel", label = "Update Channel", description = "Which channel to use", type = "enum", category = (string?)null, enumValues = new[] { "stable", "beta", "dev" }, defaultValue = "stable", sensitive = false },
                    new { key = "checkIntervalHours", label = "Check Interval Hours", description = "How often to check", type = "integer", category = (string?)null, enumValues = (string[]?)null, defaultValue = 1, sensitive = false },
                    new { key = "allowedHosts", label = "Allowed Hosts", description = "Hosts allowed to connect", type = "array", category = (string?)null, enumValues = (string[]?)null, defaultValue = Array.Empty<string>(), sensitive = false }
                }
            }
        }
    });

    private static readonly string CurrentConfigJson = JsonSerializer.Serialize(new
    {
        updates = new
        {
            autoUpdateEnabled = false,
            updateChannel = "stable",
            checkIntervalHours = 1,
            allowedHosts = new[] { "localhost", "127.0.0.1" }
        }
    });

    private GatewayApiClient CreateMockApiClient()
    {
        return CreateApiClient(request =>
        {
            var path = request.RequestUri?.LocalPath ?? "";
            return path switch
            {
                "/api/config/schema" => JsonResponse(SchemaJson),
                "/api/config/current" => JsonResponse(CurrentConfigJson),
                "/api/config/save" or "/api/config/apply" => JsonResponse("{}"),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });
    }

    [Fact]
    public async Task ConfigTab_RendersSidebarWithSections()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100); // Allow async load
        component.Render();

        var sidebar = component.FindComponent<SettingsConfigTab>().Find("[data-testid=\"config-sidebar\"]");
        Assert.NotNull(sidebar);

        var items = component.FindComponent<SettingsConfigTab>().FindAll("[data-testid=\"config-sidebar-item-updates\"]");
        Assert.NotEmpty(items);
    }

    [Fact]
    public async Task ConfigTab_FormModeRendersFields()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        // Click on section
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        // Check fields are rendered
        var autoUpdateField = tab.Find("[data-testid=\"config-field-autoUpdateEnabled\"]");
        Assert.NotNull(autoUpdateField);

        var updateChannelField = tab.Find("[data-testid=\"config-field-updateChannel\"]");
        Assert.NotNull(updateChannelField);

        var checkIntervalField = tab.Find("[data-testid=\"config-field-checkIntervalHours\"]");
        Assert.NotNull(checkIntervalField);

        var allowedHostsField = tab.Find("[data-testid=\"config-field-allowedHosts\"]");
        Assert.NotNull(allowedHostsField);
    }

    [Fact]
    public async Task ConfigTab_BooleanField_RendersToggle()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        var boolField = tab.Find("[data-testid=\"config-field-autoUpdateEnabled\"]");
        var checkbox = boolField.QuerySelector("input[type=\"checkbox\"]");
        Assert.NotNull(checkbox);
    }

    [Fact]
    public async Task ConfigTab_EnumField_RendersSelect()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        var enumField = tab.Find("[data-testid=\"config-field-updateChannel\"]");
        var select = enumField.QuerySelector(".mud-select");
        Assert.NotNull(select);
    }

    [Fact]
    public async Task ConfigTab_IntegerField_RendersNumeric()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        var intField = tab.Find("[data-testid=\"config-field-checkIntervalHours\"]");
        var input = intField.QuerySelector("input");
        Assert.NotNull(input);
    }

    [Fact]
    public async Task ConfigTab_ArrayField_RendersMultilineEditor()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        var field = tab.Find("[data-testid=\"config-field-allowedHosts\"]");
        var editor = field.QuerySelector("textarea");
        Assert.NotNull(editor);
        Assert.Contains("localhost", editor.TextContent);
        Assert.Contains("127.0.0.1", editor.TextContent);
    }

    [Fact]
    public async Task ConfigTab_ToggleToRawMode_ShowsJsonEditor()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var rawModeButton = tab.Find("[data-testid=\"mode-toggle-raw\"]");
        await rawModeButton.ClickAsync();

        component.Render();

        var jsonEditor = tab.Find("[data-testid=\"raw-json-editor\"]");
        Assert.NotNull(jsonEditor);
    }

    [Fact]
    public async Task ConfigTab_ChangeBadgeVisible_AfterEdit()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        var boolField = tab.Find("[data-testid=\"config-field-autoUpdateEnabled\"]");
        var checkbox = boolField.QuerySelector("input[type=\"checkbox\"]");
        Assert.NotNull(checkbox);

        await checkbox.ChangeAsync(new ChangeEventArgs { Value = true });
        component.Render();

        var badge = tab.Find("[data-testid=\"changes-badge\"]");
        Assert.NotNull(badge);
    }

    [Fact]
    public async Task ConfigTab_ResetButton_ClearsChanges()
    {
        var api = CreateMockApiClient();
        var component = RenderWithMudProviders<SettingsConfigTab>(p => p.Add(x => x.Api, api));

        await Task.Delay(100);
        component.Render();

        var tab = component.FindComponent<SettingsConfigTab>();
        var sectionItem = tab.Find("[data-testid=\"config-sidebar-item-updates\"]");
        await sectionItem.ClickAsync();

        component.Render();

        var boolField = tab.Find("[data-testid=\"config-field-autoUpdateEnabled\"]");
        var checkbox = boolField.QuerySelector("input[type=\"checkbox\"]");
        await checkbox!.ChangeAsync(new ChangeEventArgs { Value = true });

        component.Render();

        var resetButton = tab.Find("[data-testid=\"reset-button\"]");
        await resetButton.ClickAsync();

        component.Render();

        var badge = tab.FindAll("[data-testid=\"changes-badge\"]");
        Assert.Empty(badge);
    }
}
