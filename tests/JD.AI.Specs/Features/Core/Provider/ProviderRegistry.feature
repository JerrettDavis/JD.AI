@core @provider
Feature: Provider Registry
    As the provider management system
    I want to register providers, build kernels, and manage model selection
    So that the agent can interact with multiple AI backends

    Scenario: Registers providers and retrieves detector
        Given a provider registry with detectors for "openai" and "anthropic"
        When the detector for "openai" is requested
        Then the detector should not be null
        And the detector provider name should be "openai"

    Scenario: Builds kernel for a specific model
        Given a provider registry with detectors for "openai"
        And the "openai" detector reports as available with models "gpt-4o"
        When a kernel is built for model "gpt-4o" from provider "openai"
        Then the kernel should not be null

    Scenario: Lists all available models across providers
        Given a provider registry with detectors for "openai" and "anthropic"
        And the "openai" detector reports as available with models "gpt-4o", "gpt-4o-mini"
        And the "anthropic" detector reports as available with models "claude-3-opus"
        When all available models are listed
        Then the model list should contain 3 models

    Scenario: Returns null for unknown provider detector
        Given a provider registry with detectors for "openai"
        When the detector for "nonexistent" is requested
        Then the detector should be null

    Scenario: Build kernel throws for unregistered provider
        Given a provider registry with detectors for "openai"
        When building a kernel for an unregistered provider "unknown" with model "test-model"
        Then an InvalidOperationException should be thrown
