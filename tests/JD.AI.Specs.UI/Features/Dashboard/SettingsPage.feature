@ui
Feature: Settings Page
    As a gateway operator
    I want to configure the gateway through a settings interface
    So that I can manage server, provider, agent, channel, routing, and OpenClaw settings

    Background:
        Given I am on the settings page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays settings page heading
        Then I should see the heading "Settings"

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Loading skeleton shown while fetching config
        Given the settings are loading
        Then I should see a settings loading skeleton

    # ── Error state ───────────────────────────────────────
    Scenario: Error alert when gateway unreachable
        Given the gateway configuration cannot be loaded
        Then I should see an error alert
        And the alert should display "Unable to load gateway configuration"

    # ── Tab strip ─────────────────────────────────────────
    Scenario: Tab strip with all six tabs
        Then I should see the settings tab strip
        And the tab strip should contain a "Server" tab
        And the tab strip should contain a "Providers" tab
        And the tab strip should contain a "Agents" tab
        And the tab strip should contain a "Channels" tab
        And the tab strip should contain a "Routing" tab
        And the tab strip should contain an "OpenClaw" tab

    Scenario: Tabs have correct icons
        Then the "Server" tab should have a server icon
        And the "Providers" tab should have a hub icon
        And the "Agents" tab should have a robot icon
        And the "Channels" tab should have a cable icon
        And the "Routing" tab should have a route icon
        And the "OpenClaw" tab should have a cloud icon

    Scenario: Tabs have tooltips
        Then the "Server" tab should have tooltip "Network, auth, and rate-limit settings"
        And the "Providers" tab should have tooltip "AI model provider configuration"
        And the "Agents" tab should have tooltip "Agent definitions and auto-spawn settings"
        And the "Channels" tab should have tooltip "Messaging channel connections"
        And the "Routing" tab should have tooltip "Channel-to-agent routing rules"
        And the "OpenClaw" tab should have tooltip "OpenClaw bridge integration"

    # ── Tab navigation ────────────────────────────────────
    Scenario: Server tab is accessible
        When I click the "Server" tab
        Then the server settings panel should be visible

    Scenario: Providers tab accessible with save
        When I click the "Providers" tab
        Then the providers settings panel should be visible
        And a save providers button should be available

    Scenario: Agents tab accessible with save
        When I click the "Agents" tab
        Then the agents settings panel should be visible
        And a save agents button should be available

    Scenario: Channels tab accessible with save
        When I click the "Channels" tab
        Then the channels settings panel should be visible
        And a save channels button should be available

    Scenario: Routing tab is accessible
        When I click the "Routing" tab
        Then the routing settings panel should be visible

    Scenario: OpenClaw tab is accessible
        When I click the "OpenClaw" tab
        Then the OpenClaw settings panel should be visible

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Settings — JD.AI"

    # ── Server Tab Content ────────────────────────────────
    Scenario: Server tab shows network configuration
        When I click the "Server" tab
        Then the server settings panel should be visible
        And I should see a "Host" input field
        And I should see a "Port" input field

    Scenario: Server tab shows behavior toggles
        When I click the "Server" tab
        Then I should see a "Verbose Logging" toggle
        And I should see an "API Key Authentication" toggle
        And I should see a "Rate Limiting" toggle

    Scenario: Server tab has save button
        When I click the "Server" tab
        Then I should see the "Save Server Settings" button

    # ── Agents Tab Content ────────────────────────────────
    Scenario: Agents tab shows agent definitions
        When I click the "Agents" tab
        Then the agents settings panel should be visible
        And I should see the add agent button

    Scenario: Agents tab shows agent configuration fields
        When I click the "Agents" tab
        And there are configured agents
        Then each agent entry should have an "Agent ID" field
        And each agent entry should have a "Provider" select
        And each agent entry should have a "Model Parameters" expansion panel

    # ── Channels Tab Content ──────────────────────────────
    Scenario: Channels tab shows channel configuration list
        When I click the "Channels" tab
        Then the channels settings panel should be visible
        And I should see channel entries

    Scenario: Channels tab has enabled toggles per channel
        When I click the "Channels" tab
        Then each channel entry should have an enabled toggle

    Scenario: Channels tab masks secret settings
        When I click the "Channels" tab
        And a channel has a setting containing "token" in its key
        Then that setting field should be a password input

    # ── Providers Tab Content ─────────────────────────────
    Scenario: Providers tab shows provider list
        When I click the "Providers" tab
        Then the providers settings panel should be visible
        And I should see provider entries

    Scenario: Providers tab has test buttons
        When I click the "Providers" tab
        Then each provider entry should have a "Test" button

    # ── Routing Tab Content ───────────────────────────────
    Scenario: Routing tab shows default agent selector
        When I click the "Routing" tab
        Then the routing settings panel should be visible
        And I should see a "Default Agent" select

    Scenario: Routing tab has add rule button
        When I click the "Routing" tab
        Then I should see the "Add Rule" button

    # ── OpenClaw Tab Content ──────────────────────────────
    Scenario: OpenClaw tab shows bridge configuration
        When I click the "OpenClaw" tab
        Then the OpenClaw settings panel should be visible
        And I should see an "Enable OpenClaw" toggle

    Scenario: OpenClaw tab shows WebSocket URL field
        When I click the "OpenClaw" tab
        Then I should see a "WebSocket URL" input

    Scenario: OpenClaw tab shows registered agents section
        When I click the "OpenClaw" tab
        Then I should see the registered agents section
