@ui
Feature: Control > Overview Page
    As a gateway operator
    I want a dedicated /control/overview route
    So that I can immediately assess gateway health on arrival

    Background:
        Given I navigate to "/control/overview"

    @smoke
    Scenario: Page renders title and subtitle
        Then I should see the heading "Overview"
        And I should see the text "System health, gateway status, and live counters."

    @smoke
    Scenario: Snapshot cards are visible
        Then I should see snapshot cards or the gateway error state

    Scenario: Gateway access form is visible
        Then I should see element "[data-testid='input-websocket-url']"
        And I should see element "[data-testid='input-gateway-token']"
        And I should see element "[data-testid='input-password']"
        And I should see element "[data-testid='select-language']"
        And I should see element "[data-testid='btn-connect']"
        And I should see element "[data-testid='btn-refresh']"

    Scenario: Refresh button triggers data reload
        When I click "[data-testid='btn-refresh']"
        Then the page should not show an error state

    Scenario: Home route redirects to Control Overview
        Given I navigate to "/"
        Then I should see the heading "Overview"

    Scenario: Sidebar Control > Overview link is active on this route
        Then the nav link "[data-testid='nav-control-overview']" should be active

    Scenario: Recent sessions table shows session key column
        Then I should see element "[data-testid='recent-sessions-card']"

    Scenario: Counter cards show agent and channel counts
        Then I should see element "[data-testid='counter-card-agents']"
        And I should see element "[data-testid='counter-card-channels']"
        And I should see element "[data-testid='counter-card-sessions']"
