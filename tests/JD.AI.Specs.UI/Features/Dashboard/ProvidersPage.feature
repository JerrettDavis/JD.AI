@ignore
@ui
Feature: Providers Page
    As a gateway operator
    I want to view configured AI model providers
    So that I can monitor provider availability and available models

    Background:
        Given I am on the providers page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays providers page heading
        Then I should see the heading "Model Providers"

    @smoke
    Scenario: Refresh button is visible
        Then I should see the refresh button

    # ── Loading states ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the providers are loading
        Then I should see 2 skeleton provider cards

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state when no providers
        Given there are no configured providers
        Then I should see the providers empty state
        And the empty state should display "No providers configured"

    # ── Data display ──────────────────────────────────────
    @requires-providers
    Scenario: Provider cards show name and availability
        Given there are configured providers
        Then I should see provider cards
        And each provider card should display the provider name in bold
        And available providers should show model count subtitle
        And unavailable providers should show status message subtitle

    @requires-providers
    Scenario: Provider status badges show Online or Offline
        Given there are configured providers
        Then each provider card should show a status badge
        And available providers should show "Online" badge in green
        And unavailable providers should show "Offline" badge in red

    @requires-providers
    Scenario: Provider avatar color reflects availability
        Given there are configured providers
        Then available provider avatars should be green
        And unavailable provider avatars should be red

    # ── Model table ───────────────────────────────────────
    @requires-providers
    Scenario: Model table shown for available providers with models
        Given there are available providers with models
        Then available provider cards should show a model table
        And the model table should have "Model ID" and "Display Name" columns
        And model IDs should be displayed in monospace font

    @requires-providers
    Scenario: No model table for providers without models
        Given there is a provider with no models
        Then that provider card should not show a model table

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Providers — JD.AI"

    # ── Planned ───────────────────────────────────────────
    @planned
    Scenario: Ollama provider shows model size and context window
        Given the Ollama provider is available
        Then the Ollama model table should show "Size" and "Context" columns
