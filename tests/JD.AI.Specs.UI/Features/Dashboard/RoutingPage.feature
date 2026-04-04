@ignore
@ui
Feature: Routing Page
    As a gateway operator
    I want to configure channel-to-agent routing rules
    So that incoming messages are directed to the correct AI agents

    Background:
        Given I am on the routing page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays routing page heading
        Then I should see the heading "Channel → Agent Routing"

    @smoke
    Scenario: Sync OpenClaw button is available
        Then I should see the "Sync OpenClaw" button with sync icon

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton rows shown while loading
        Given the routing mappings are loading
        Then I should see 4 skeleton loading rows

    # ── Data grid ─────────────────────────────────────────
    @requires-routing
    Scenario: Routing data grid displays with correct columns
        Given there are routing mappings
        Then I should see the routing data grid
        And the grid should have a "Channel" column
        And the grid should have an "Agent ID" column
        And the grid should have a "Status" column

    @requires-routing
    Scenario: Channel column shows icon and name
        Given there are routing mappings
        Then each routing row should display a channel icon
        And each routing row should display the channel type name

    @requires-routing
    Scenario: Status chips show Override or Default
        Given there are routing mappings
        Then rows with an assigned agent should show "Override" chip in primary color
        And rows without an assigned agent should show "Default" chip

    @requires-routing
    Scenario: Agent ID column is editable inline
        Given there are routing mappings
        Then the Agent ID column should support inline cell editing

    @requires-routing
    Scenario: Editing agent ID updates routing via API
        Given there are routing mappings
        When I edit the agent ID on a routing row
        And I commit the cell edit
        Then a success snackbar should appear with "Routing updated"

    # ── Routing diagram ───────────────────────────────────
    @requires-routing
    Scenario: Routing diagram section is displayed
        Given there are routing mappings
        Then I should see the "Routing Diagram" section
        And the diagram should use a timeline layout

    @requires-routing
    Scenario: Diagram shows channel-to-agent flow
        Given there are routing mappings
        Then each timeline item should show a channel chip
        And each timeline item should show an arrow
        And each timeline item should show an agent chip or "OpenClaw Default"

    @requires-routing
    Scenario: Timeline items are color-coded by assignment
        Given there are routing mappings
        Then timeline items with assigned agents should be primary colored
        And timeline items using default should be default colored

    # ── Sync OpenClaw ─────────────────────────────────────
    Scenario: Sync OpenClaw triggers sync and shows snackbar
        When I click the "Sync OpenClaw" button
        Then a success snackbar should appear with "sync complete"
        And the routing data should refresh

    # ── Channel icons ─────────────────────────────────────
    @requires-routing
    Scenario: Channel icons match channel type
        Given there are routing mappings
        Then routing channel icons should match their type

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Routing — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Agent selector dropdown replaces free-text editing
        Given there are routing mappings
        When I click to edit an agent ID
        Then a dropdown with available agents should appear
