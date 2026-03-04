@ui
Feature: Providers Page
    As a gateway operator
    I want to view configured AI model providers
    So that I can monitor provider availability and available models

    Background:
        Given I am on the providers page

    Scenario: Displays providers page heading
        Then I should see the providers page heading "Model Providers"

    Scenario: Refresh button is visible
        Then I should see the providers refresh button

    Scenario: Displays provider list when providers exist
        Given there are configured providers
        Then I should see provider cards
        And each provider card should display a name

    Scenario: Shows provider availability status
        Given there are configured providers
        Then each provider card should show an availability status
        And availability status should display "Online" or "Offline"

    Scenario: Empty state shown when no providers
        Given there are no configured providers
        Then I should see the providers empty state
        And the empty state should display "No providers configured"
