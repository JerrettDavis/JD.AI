@ignore
@ui
Feature: Agents Page
    As a gateway operator
    I want to manage AI agents from the dashboard
    So that I can spawn, monitor, and remove agents

    Background:
        Given I am on the agents page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays agent page heading
        Then I should see the heading "Agents"

    @smoke
    Scenario: Spawn button is visible
        Then I should see the "Spawn Agent" button with add icon

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton rows shown while loading
        Given the agents list is loading
        Then I should see 5 skeleton loading rows

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state shown when no agents
        Given there are no active agents
        Then I should see the agents empty state
        And the empty state should display "No active agents"

    # ── Data display ──────────────────────────────────────
    @requires-agents
    Scenario: Agent data grid displays with correct columns
        Given there are active agents
        Then I should see the agents data grid
        And the data grid should have an "ID" column
        And the data grid should have a "Provider" column
        And the data grid should have a "Model" column
        And the data grid should have a "Turns" column
        And the data grid should have a "Created" column

    @requires-agents
    Scenario: Each agent row has delete button
        Given there are active agents
        Then each agent row should have a red delete button

    # ── Spawn dialog ──────────────────────────────────────
    Scenario: Spawn dialog opens with all fields
        When I click the "Spawn Agent" button
        Then the spawn agent dialog should be visible
        And the dialog title should be "Spawn New Agent"
        And the dialog should contain an "Agent ID" input
        And the dialog should contain a "Provider" input
        And the dialog should contain a "Model" input
        And the dialog should contain a "System Prompt" multiline input
        And the dialog should contain a "Max Turns" numeric input
        And the dialog should have a "Cancel" button
        And the dialog should have a "Spawn" button

    @requires-agents
    Scenario: Successful spawn shows snackbar and refreshes list
        When I click the "Spawn Agent" button
        And I fill in the agent ID with a unique value
        And I fill in the provider with "ollama"
        And I fill in the model with "test-model"
        And I click the "Spawn" button
        Then a success snackbar should appear with "spawned"
        And the agents data grid should refresh

    Scenario: Spawn with empty ID does not submit
        When I click the "Spawn Agent" button
        And I leave the agent ID empty
        And I click the "Spawn" button
        Then the dialog should remain open

    Scenario: Spawn dialog can be cancelled
        When I click the "Spawn Agent" button
        And I click the "Cancel" button
        Then the dialog should close

    # ── Delete agent ──────────────────────────────────────
    @requires-agents
    Scenario: Delete agent with confirmation
        Given there are active agents
        When I click the delete button on the first agent
        Then a confirmation dialog should appear with "Stop agent"
        When I confirm the deletion
        Then a success snackbar should appear with "stopped"

    @requires-agents
    Scenario: Delete agent can be cancelled
        Given there are active agents
        When I click the delete button on the first agent
        Then a confirmation dialog should appear
        When I cancel the deletion
        Then the agent should still be in the list

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Agents — JD.AI"

    # ── Detail panel (#473) ───────────────────────────────
    @planned
    Scenario: Clicking agent row opens detail panel
        Given there are active agents
        When I click on the first agent row
        Then the agent detail panel should be visible

    @planned
    Scenario: Detail panel shows Overview tab by default
        Given there are active agents
        When I click on the first agent row
        Then I should see the overview tab content

    @planned
    Scenario: Detail panel Tools tab shows agent tools
        Given there are active agents
        When I click on the first agent row
        And I click the "Tools" tab in the detail panel
        Then I should see the tools list

    @planned
    Scenario: Detail panel Skills tab shows assigned skills
        Given there are active agents
        When I click on the first agent row
        And I click the "Skills" tab in the detail panel
        Then I should see the assigned skills list

    @planned
    Scenario: Detail panel close button hides panel
        Given there are active agents
        When I click on the first agent row
        And I click the detail panel close button
        Then the agent detail panel should not be visible

    @planned
    Scenario: Toolbar Copy ID button is present when agent selected
        Given there are active agents
        When I click on the first agent row
        Then the "Copy ID" toolbar button should be enabled

    @planned
    Scenario: Toolbar Set Default button is present when agent selected
        Given there are active agents
        When I click on the first agent row
        Then the "Set Default" toolbar button should be enabled

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Agent grid shows expandable details
        Given there are active agents
        When I expand the first agent row
        Then I should see the agent's system prompt
        And I should see the agent's active sessions

    @planned
    Scenario: Test message action
        Given there are active agents
        When I click the test message button on the first agent
        Then a test response should appear
