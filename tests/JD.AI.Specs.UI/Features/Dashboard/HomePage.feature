@ui
Feature: Home Page - Gateway Overview
    As a gateway operator
    I want to see an overview of the system status
    So that I can quickly assess the health of my AI gateway

    Background:
        Given I am on the home page

    Scenario: Displays overview stat cards
        Then I should see the gateway overview heading
        And I should see 4 stat cards

    Scenario: Stat cards show correct labels
        Then I should see a stat card for "Agents"
        And I should see a stat card for "Channels"
        And I should see a stat card for "Sessions"
        And I should see a stat card for "OpenClaw"

    Scenario: Stat cards show numeric counts
        Then the "Agents" stat card should display a count
        And the "Channels" stat card should display a count
        And the "Sessions" stat card should display a count

    Scenario: Shows recent activity stream
        Then I should see the recent activity section
        And the recent activity section should have a title "Recent Activity"

    Scenario: Navigates to agents page via sidebar
        When I click the "Agents" navigation link
        Then I should be on the "/agents" page
