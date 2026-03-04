@ui
Feature: Routing Page
    As a gateway operator
    I want to configure channel-to-agent routing rules
    So that incoming messages are directed to the correct AI agents

    Background:
        Given I am on the routing page

    Scenario: Displays routing page heading
        Then I should see the routing page heading

    Scenario: Sync OpenClaw button is available
        Then I should see the routing sync OpenClaw button

    Scenario: Displays routing data grid
        Given there are routing mappings
        Then I should see the routing data grid
        And the grid should contain routing rows

    Scenario: Routing rows show channel-to-agent mappings
        Given there are routing mappings
        Then each routing row should display a channel type
        And each routing row should display an agent ID or default

    Scenario: Routing diagram is displayed
        Given there are routing mappings
        Then I should see the routing diagram section
        And the diagram should contain timeline items
