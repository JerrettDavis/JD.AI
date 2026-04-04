@ignore
@ui
Feature: Sessions Page
    As a gateway operator
    I want to view and manage conversation sessions
    So that I can monitor active conversations and review past ones

    Background:
        Given I am on the sessions page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays sessions page heading
        Then I should see the heading "Sessions"

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton rows shown while loading
        Given the sessions list is loading
        Then I should see 5 skeleton loading rows

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state when no sessions
        Given there are no sessions
        Then I should see the sessions empty state
        And the empty state should display "No sessions found"

    # ── Data display ──────────────────────────────────────
    @requires-sessions
    Scenario: Session data grid displays with correct columns
        Given there are sessions
        Then I should see the sessions data grid
        And the grid should have a "Session ID" column
        And the grid should have a "Model" column
        And the grid should have a "Provider" column
        And the grid should have a "Messages" column
        And the grid should have a "Tokens" column
        And the grid should have a "Status" column
        And the grid should have a "Created" column

    @requires-sessions
    Scenario: Session status chips display correctly
        Given there are sessions
        Then active sessions should show "Active" chip in green
        And closed sessions should show "Closed" chip in default color

    @requires-sessions
    Scenario: Data grid supports filtering
        Given there are sessions
        Then the data grid should have filter controls

    @requires-sessions
    Scenario: Data grid supports sorting
        Given there are sessions
        When I click a column header
        Then the grid should sort by that column

    # ── Row actions ───────────────────────────────────────
    @requires-sessions
    Scenario: Row action buttons are present
        Given there are sessions
        Then each session row should have a view button
        And each session row should have an export button

    @requires-sessions
    Scenario: Active sessions have close button
        Given there are active sessions
        Then active session rows should have a close button in warning color

    @requires-sessions
    Scenario: Closed sessions do not have close button
        Given there are closed sessions
        Then closed session rows should not have a close button

    # ── Turn viewer ───────────────────────────────────────
    @requires-sessions
    Scenario: View session opens turn viewer
        Given there are sessions
        When I click the view button on a session
        Then the turn viewer panel should appear below the grid
        And the turn viewer should show "Conversation:" with the session ID

    @requires-sessions
    Scenario: Turn viewer displays conversation turns
        Given there are sessions with turns
        When I click the view button on a session
        Then each turn should show a role chip
        And each turn should show message content
        And each turn should show token counts
        And each turn should show response duration
        And each turn should have a colored left border

    @requires-sessions
    Scenario: Turn viewer can be dismissed
        Given I am viewing a session's turns
        When I click the close button on the turn viewer
        Then the turn viewer should disappear

    @requires-sessions
    Scenario: Empty session shows no turns message
        Given there is a session with no turns
        When I click the view button on that session
        Then the turn viewer should show "No turns in this session"

    # ── Export ─────────────────────────────────────────────
    @requires-sessions
    Scenario: Export session shows success snackbar
        Given there are sessions
        When I click the export button on a session
        Then a success snackbar should appear with "exported"

    # ── Close session ─────────────────────────────────────
    @requires-sessions
    Scenario: Close session shows success and refreshes
        Given there are active sessions
        When I click the close button on an active session
        Then a success snackbar should appear with "closed"
        And the session list should refresh

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Sessions — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Filter sessions by agent, channel, and date range
        Given there are sessions
        Then I should see filter controls for agent, channel, and date range
        When I apply a filter
        Then only matching sessions should be displayed
