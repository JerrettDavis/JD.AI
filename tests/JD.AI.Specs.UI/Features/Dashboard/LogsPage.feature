@ui
Feature: Logs Page
    As a gateway operator
    I want to inspect audit events from the dashboard
    So that I can verify activity and filter the event stream

    @smoke
    Scenario: Sidebar navigation reaches logs page
        Given I open the dashboard route "/"
        When I click the "Logs" navigation link
        Then I should be on the "/logs" page
        And I should see the heading "Logs"

    @smoke
    Scenario: Logs page renders filter controls
        Given I am on the logs page
        Then I should see the heading "Logs"
        And I should see the logs filter panel
        And I should see the auto-refresh toggle
        And I should see either the logs grid or the logs empty state

    @requires-audit
    Scenario: Search filters can narrow logs to zero results
        Given I am on the logs page
        And there is at least one log event row
        When I search logs for a guaranteed-miss term
        Then I should see the logs empty state
