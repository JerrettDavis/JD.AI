@ui
Feature: Home Page - Gateway Overview
    As a gateway operator
    I want to see an overview of the system status
    So that I can quickly assess the health of my AI gateway

    Background:
        Given I am on the home page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays overview heading
        Then I should see the heading "Gateway Overview"

    @smoke
    Scenario: Displays four stat cards
        Then I should see 4 stat cards
        And I should see a stat card labeled "Agents"
        And I should see a stat card labeled "Channels"
        And I should see a stat card labeled "Sessions"
        And I should see a stat card labeled "OpenClaw"

    Scenario: App bar displays logo
        Then the app bar should display "JD.AI"
        And the app bar should display "Gateway"

    # ── Data display ──────────────────────────────────────
    Scenario: Stat cards show numeric counts from gateway status
        Then the "Agents" stat card should display a numeric value
        And the "Channels" stat card should display a numeric value
        And the "Sessions" stat card should display a numeric value

    Scenario: OpenClaw card shows connection status
        Then the "OpenClaw" stat card should display "Connected" or "Offline"

    @requires-openclaw
    Scenario: OpenClaw bridge table shown when bridge data exists
        Given the OpenClaw bridge is configured
        Then I should see the OpenClaw Bridge details table
        And the table should show the "Enabled" property
        And the table should show "Registered Agents"

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the gateway status is loading
        Then I should see skeleton stat card placeholders
        And I should see a skeleton activity section

    # ── Activity feed ─────────────────────────────────────
    Scenario: Recent Activity section with heading and refresh
        Then I should see the "Recent Activity" section heading
        And I should see a refresh button in the activity section

    Scenario: Activity feed empty state
        Given there are no recent activity events
        Then I should see "No recent activity" in the activity feed

    @requires-agents
    Scenario: Activity events display event details
        Given there are recent activity events
        Then each activity event should show an event type chip
        And each activity event should show a message
        And each activity event should show a timestamp

    @requires-agents
    Scenario: Activity feed shows most recent events first
        Given there are multiple activity events
        Then the activity feed should display events in reverse chronological order
        And the feed should show at most 20 items

    # ── Real-time ─────────────────────────────────────────
    @requires-agents
    Scenario: New activity events appear without page refresh
        Given I am observing the activity feed
        When a new gateway event occurs
        Then the event should appear in the activity feed

    # ── Error handling ────────────────────────────────────
    Scenario: Graceful degradation when API is unreachable
        Given the gateway API is unavailable
        Then the stat cards should display zero counts
        And no error dialog should block the page

    # ── Navigation ────────────────────────────────────────
    @smoke
    Scenario: Sidebar navigates to agents page
        When I click the "Agents" navigation link
        Then I should be on the "/agents" page

    @smoke
    Scenario: Sidebar navigates to chat page
        When I click the "Chat" navigation link
        Then I should be on the "/chat" page

    Scenario: Sidebar navigates to channels page
        When I click the "Channels" navigation link
        Then I should be on the "/channels" page

    Scenario: Sidebar navigates to sessions page
        When I click the "Sessions" navigation link
        Then I should be on the "/sessions" page

    Scenario: Sidebar navigates to providers page
        When I click the "Providers" navigation link
        Then I should be on the "/providers" page

    Scenario: Sidebar navigates to routing page
        When I click the "Routing" navigation link
        Then I should be on the "/routing" page

    Scenario: Sidebar navigates to settings page
        When I click the "Settings" navigation link
        Then I should be on the "/settings" page

    # ── SignalR connection ────────────────────────────────
    Scenario: SignalR connection indicator shows status
        Then the app bar should show a connection status indicator
        And the indicator should display "Live" or "Offline"

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "JD.AI — Gateway"
