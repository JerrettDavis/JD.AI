@ui
Feature: Gateway Auth Gate
    As a gateway operator
    I want to see a connection form when the gateway is unreachable
    So that I can enter my credentials and connect

    @smoke
    Scenario: Auth gate shown when gateway is disconnected
        Given the gateway is disconnected
        When I navigate to the home page
        Then I should see the auth gate overlay
        And I should see a URL input prefilled with "ws://127.0.0.1:18789"
        And I should see a Connect button

    Scenario: Auth gate hidden when gateway is connected
        Given the gateway is connected
        When I navigate to the home page
        Then I should not see the auth gate overlay

    Scenario: URL pre-filled from localStorage
        Given localStorage has "jd-gateway-url" set to "ws://192.168.1.10:18789"
        When I navigate to the home page
        Then the URL input should show "ws://192.168.1.10:18789"

    Scenario: Successful connect hides the auth gate
        Given the gateway is disconnected
        When I navigate to the home page
        And I enter "ws://127.0.0.1:18789" in the URL input
        And I click the Connect button
        And the gateway connects successfully
        Then I should not see the auth gate overlay

    Scenario: Failed connect shows error message
        Given the gateway is disconnected
        When I navigate to the home page
        And I click the Connect button with an invalid URL
        Then I should see a toast error notification
