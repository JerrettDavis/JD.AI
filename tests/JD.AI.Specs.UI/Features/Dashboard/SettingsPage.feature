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
    @smoke
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
