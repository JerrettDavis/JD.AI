@ui
@smoke
Feature: Dashboard Smoke
    As an operator
    I want core dashboard pages to render in a clean environment
    So that CI verifies the baseline UX without subscription dependencies

    Scenario: Home overview renders
        Given I open the dashboard route "/"
        Then I should see the page heading "Overview"

    Scenario: Agents page renders with either data or empty state
        Given I open the dashboard route "/agents"
        Then I should see the page heading "Agents"
        And I should see either an agents data grid or the agents empty state

    Scenario: Channels page renders dashboard-only load error
        Given I open the dashboard route "/channels"
        Then I should see the page heading "Channels"
        And I should see the sync OpenClaw button
        And I should see the channels load error

    Scenario: Providers page renders dashboard-only load error
        Given I open the dashboard route "/providers"
        Then I should see the page heading "Model Providers"
        And I should see the providers load error

    Scenario: Routing page renders dashboard-only load error
        Given I open the dashboard route "/routing"
        Then I should see the page heading "Agent Routing"
        And I should see the routing load error state

    Scenario: Sessions page renders with either data or empty state
        Given I open the dashboard route "/sessions"
        Then I should see the page heading "Sessions"
        And I should see either a sessions data grid or the sessions empty state

    Scenario: Settings page renders tabs or unavailable state
        Given I open the dashboard route "/settings"
        Then I should see the page heading "Settings"
        And I should see either the settings tab strip or a settings unavailable message

    Scenario: Chat page renders baseline controls
        Given I open the dashboard route "/chat"
        Then I should see the web chat header
        And I should see the message input field
        And I should see the agent selector or no-agents warning
