@core @provider @credentials
Feature: Provider Credentials
    As a user of the AI agent
    I want to securely store and retrieve API credentials
    So that my keys are protected and persist between sessions

    Background:
        Given a credential store backed by a temporary directory

    Scenario: Stores and retrieves encrypted credentials
        When a credential "openai-key" is stored with value "sk-test-12345"
        And the credential "openai-key" is retrieved
        Then the retrieved credential should be "sk-test-12345"

    Scenario: Returns null for missing credentials
        When the credential "nonexistent-key" is retrieved
        Then the retrieved credential should be null

    Scenario: Removes stored credentials
        Given a credential "openai-key" is stored with value "sk-test-12345"
        When the credential "openai-key" is removed
        And the credential "openai-key" is retrieved
        Then the retrieved credential should be null

    Scenario: Overwrites existing credential
        Given a credential "openai-key" is stored with value "old-value"
        When a credential "openai-key" is stored with value "new-value"
        And the credential "openai-key" is retrieved
        Then the retrieved credential should be "new-value"

    Scenario: Lists keys matching a prefix
        Given a credential "openai-key" is stored with value "sk-1"
        And a credential "openai-backup" is stored with value "sk-2"
        And a credential "anthropic-key" is stored with value "ak-1"
        When keys are listed with prefix "openai"
        Then the key list should contain 2 entries
        And the key list should include "openai-key"
        And the key list should include "openai-backup"

    Scenario: Store reports availability
        Then the credential store should report as available
        And the credential store should have a non-empty store name
