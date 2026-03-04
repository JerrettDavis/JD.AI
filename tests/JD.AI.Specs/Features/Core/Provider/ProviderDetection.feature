@core @provider
Feature: Provider Detection
    As the provider system
    I want to detect available AI providers automatically
    So that users can discover and use available models

    Scenario: Detects available providers
        Given a provider registry with detectors for "openai" and "anthropic"
        And the "openai" detector reports as available with models "gpt-4o", "gpt-4o-mini"
        And the "anthropic" detector reports as available with models "claude-3-opus"
        When providers are detected
        Then the detected providers should include "openai"
        And the detected providers should include "anthropic"
        And the "openai" provider should have 2 models

    Scenario: Skips unavailable providers
        Given a provider registry with detectors for "openai" and "anthropic"
        And the "openai" detector reports as available with models "gpt-4o"
        And the "anthropic" detector reports as unavailable with message "No API key"
        When providers are detected
        Then the detected providers should include "openai" as available
        And the detected providers should include "anthropic" as unavailable

    Scenario: Returns unified model catalog from available providers
        Given a provider registry with detectors for "openai" and "anthropic"
        And the "openai" detector reports as available with models "gpt-4o", "gpt-4o-mini"
        And the "anthropic" detector reports as available with models "claude-3-opus"
        When the model catalog is retrieved
        Then the catalog should contain 3 models
        And the catalog should include model "gpt-4o" from provider "openai"
        And the catalog should include model "claude-3-opus" from provider "anthropic"

    Scenario: Detector failure is handled gracefully
        Given a provider registry with detectors for "openai" and "failing-provider"
        And the "openai" detector reports as available with models "gpt-4o"
        And the "failing-provider" detector throws an exception
        When providers are detected
        Then the detected providers should include "openai" as available
        And the detected providers should include "failing-provider" as unavailable

    Scenario: Empty registry returns no providers
        Given an empty provider registry
        When providers are detected
        Then the detected providers list should be empty
