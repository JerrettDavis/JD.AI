@ui
Feature: Settings Page
    As a gateway operator
    I want to configure the gateway through a settings interface
    So that I can manage server, provider, agent, channel, and routing settings

    Background:
        Given I am on the settings page

    Scenario: Displays settings page heading
        Then I should see the settings page heading "Settings"

    Scenario: Displays settings tabs
        Then I should see the settings tab strip
        And the tab strip should contain a "Server" tab
        And the tab strip should contain a "Providers" tab
        And the tab strip should contain a "Agents" tab
        And the tab strip should contain a "Channels" tab
        And the tab strip should contain a "Routing" tab
        And the tab strip should contain an "OpenClaw" tab

    Scenario: Agents tab is accessible
        When I click the "Agents" settings tab
        Then the agents settings panel should be visible
        And a save agents button should be available

    Scenario: Channels tab is accessible
        When I click the "Channels" settings tab
        Then the channels settings panel should be visible
        And a save channels button should be available

    Scenario: Providers tab is accessible
        When I click the "Providers" settings tab
        Then the providers settings panel should be visible
        And a save providers button should be available
