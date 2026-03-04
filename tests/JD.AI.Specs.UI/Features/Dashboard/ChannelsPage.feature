@ui
Feature: Channels Page
    As a gateway operator
    I want to view and manage messaging channel connections
    So that I can control which platforms my agents communicate through

    Background:
        Given I am on the channels page

    Scenario: Displays channels page heading
        Then I should see the channels page heading "Channels"

    Scenario: Sync OpenClaw button is visible
        Then I should see the sync OpenClaw button

    Scenario: Displays channel list when channels exist
        Given there are configured channels
        Then I should see channel cards
        And each channel card should display a name

    Scenario: Shows channel status badges
        Given there are configured channels
        Then each channel card should show a status badge
        And status badges should display "Online" or "Offline"

    Scenario: Channel configuration is accessible via override
        Given there are configured channels
        Then each channel card should have an override button
