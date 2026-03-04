@ui
Feature: Agents Page
    As a gateway operator
    I want to manage AI agents from the dashboard
    So that I can spawn, monitor, and remove agents

    Background:
        Given I am on the agents page

    Scenario: Displays agent page heading
        Then I should see the agents page heading "Agents"

    Scenario: Spawn button is visible
        Then I should see the spawn agent button

    Scenario: Spawn button opens dialog
        When I click the spawn agent button
        Then the spawn agent dialog should be visible
        And the dialog should contain an agent ID input
        And the dialog should contain a provider input
        And the dialog should contain a model input

    Scenario: Empty state shown when no agents
        Given there are no active agents
        Then I should see the agents empty state
        And the empty state should display "No active agents"

    Scenario: Agent list displays when agents exist
        Given there are active agents
        Then I should see the agents data grid
        And the data grid should contain agent rows

    Scenario: Delete agent button triggers confirmation
        Given there are active agents
        When I click the delete button on the first agent
        Then a confirmation dialog should appear
