@ui
Feature: Channels Page
    As a gateway operator
    I want to view and manage messaging channel connections
    So that I can control which platforms my agents communicate through

    Background:
        Given I am on the channels page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays channels page heading
        Then I should see the heading "Channels"

    @smoke
    Scenario: Sync OpenClaw button is visible
        Then I should see the "Sync OpenClaw" button with sync icon

    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the channels are loading
        Then I should see 3 skeleton channel cards

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state when no channels configured
        Given there are no configured channels
        Then I should see the channels empty state
        And the empty state should display "No channels configured"

    # ── Data display ──────────────────────────────────────
    @requires-channels
    Scenario: Channel cards display with name and type
        Given there are configured channels
        Then I should see channel cards
        And each channel card should display a display name
        And each channel card should display the channel type

    @requires-channels
    Scenario: Channel status badges show Online or Offline
        Given there are configured channels
        Then each channel card should show a status badge
        And status badges should display "Online" or "Offline"

    @requires-channels
    Scenario: Channel avatar reflects connection status
        Given there are configured channels
        Then connected channel avatars should be green
        And disconnected channel avatars should be default color

    # ── Connect/Disconnect ────────────────────────────────
    @requires-channels
    Scenario: Connect and disconnect buttons shown by status
        Given there are configured channels
        Then connected channels should show a "Disconnect" button
        And disconnected channels should show a "Connect" button

    @requires-channels
    Scenario: Connect channel triggers API and shows snackbar
        Given there is a disconnected channel
        When I click the "Connect" button on the channel
        Then a success snackbar should appear with "connected"

    @requires-channels
    Scenario: Disconnect channel triggers API and shows snackbar
        Given there is a connected channel
        When I click the "Disconnect" button on the channel
        Then a warning snackbar should appear with "disconnected"

    # ── Override dialog ───────────────────────────────────
    @requires-channels
    Scenario: Override button available on each channel
        Given there are configured channels
        Then each channel card should have an "Override" button with edit icon

    @requires-channels
    Scenario: Override dialog opens with channel name
        Given there are configured channels
        When I click the "Override" button on a channel
        Then the override dialog should be visible
        And the dialog title should contain the channel name

    @requires-channels
    Scenario: Override dialog contains all configuration fields
        Given there are configured channels
        When I click the "Override" button on a channel
        Then the dialog should contain an "Agent ID" field
        And the dialog should contain a "Model" field
        And the dialog should contain a "Routing Mode" dropdown
        And the "Routing Mode" dropdown should have options "Passthrough", "Sidecar", "Intercept"
        And the dialog should contain an "Override Enabled" switch
        And the dialog should have "Cancel" and "Save" buttons

    @requires-channels
    Scenario: Override dialog can be cancelled
        Given there are configured channels
        When I click the "Override" button on a channel
        And I click "Cancel" in the override dialog
        Then the override dialog should close

    @requires-channels
    Scenario: Override save shows success snackbar
        Given there are configured channels
        When I click the "Override" button on a channel
        And I fill in the agent ID
        And I click "Save" in the override dialog
        Then a success snackbar should appear with "Override saved"

    # ── Sync OpenClaw ─────────────────────────────────────
    Scenario: Sync OpenClaw triggers sync and shows snackbar
        When I click the "Sync OpenClaw" button
        Then a success snackbar should appear with "sync complete"

    # ── Channel icons ─────────────────────────────────────
    @requires-channels
    Scenario: Channel icons match channel type
        Given there are configured channels
        Then each channel card should display an icon matching its type

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Channels — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Real-time channel status updates
        Given there are configured channels
        When a channel status changes via SignalR
        Then the status badge should update without page refresh

    @planned
    Scenario: Test channel connectivity
        Given there are configured channels
        When I click the "Test" button on a channel
        Then a connectivity test result should appear
