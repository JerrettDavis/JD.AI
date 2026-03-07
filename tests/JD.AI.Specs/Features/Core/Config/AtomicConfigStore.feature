@core @config
Feature: Atomic Config Store
    As the configuration system
    I want to read and write configuration safely
    So that settings persist correctly even under concurrent access

    Background:
        Given an atomic config store backed by a temporary file

    Scenario: Reads default values when no config exists
        When the configuration is read
        Then the default provider should be null
        And the default model should be null

    Scenario: Writes and reads a default provider
        When the default provider is set to "openai"
        And the configuration is read
        Then the default provider should be "openai"

    Scenario: Writes and reads a default model
        When the default model is set to "gpt-4o"
        And the configuration is read
        Then the default model should be "gpt-4o"

    Scenario: Project-scoped defaults override global defaults
        Given the global default provider is set to "openai"
        And the project "/tmp/my-project" default provider is set to "anthropic"
        When the default provider is read for project "/tmp/my-project"
        Then the default provider should be "anthropic"

    Scenario: Global default is used when no project override exists
        Given the global default provider is set to "openai"
        When the default provider is read for project "/tmp/other-project"
        Then the default provider should be "openai"

    Scenario: Concurrent writes are safe
        When 10 concurrent writes set different default models
        And the configuration is read
        Then the default model should be one of the written values

    Scenario: Missing key returns default empty config
        When the configuration is read
        Then the config object should not be null
        And the config defaults should not be null
