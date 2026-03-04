@ui
Feature: Sessions Page
    As a gateway operator
    I want to view and manage conversation sessions
    So that I can monitor active conversations and review past ones

    Background:
        Given I am on the sessions page

    Scenario: Displays sessions page heading
        Then I should see the sessions page heading "Sessions"

    Scenario: Displays session data grid when sessions exist
        Given there are active sessions
        Then I should see the sessions data grid
        And the grid should contain session rows

    Scenario: Session details are accessible
        Given there are active sessions
        Then each session row should have a view button
        And each session row should have an export button

    Scenario: Empty state when no sessions
        Given there are no sessions
        Then I should see the sessions empty state
        And the empty state should display "No sessions found"

    Scenario: Session status is displayed
        Given there are active sessions
        Then session rows should display status chips
