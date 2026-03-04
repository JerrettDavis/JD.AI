@core @agent @conversation-transform
Feature: Conversation Transformation
    As a user switching AI models
    I want conversation history to be transformed appropriately
    So that the new model has proper context

    Background:
        Given a provider registry with a "test-provider" provider
        And a mock chat service that returns "Summary of conversation"

    Scenario: Preserve mode returns same history
        Given an agent session with model "gpt-4o" from provider "test-provider"
        And the session has conversation history with 4 messages
        When the conversation is transformed with mode "Preserve" for model "claude-3-opus" on provider "test-provider"
        Then the transformed history should contain 4 messages

    Scenario: Fresh mode returns empty history
        Given an agent session with model "gpt-4o" from provider "test-provider"
        And the session has conversation history with 4 messages
        When the conversation is transformed with mode "Fresh" for model "claude-3-opus" on provider "test-provider"
        Then the transformed history should be empty

    Scenario: Compact mode produces summarized history
        Given an agent session with model "gpt-4o" from provider "test-provider"
        And the session has conversation history with 4 messages
        When the conversation is transformed with mode "Compact" for model "claude-3-opus" on provider "test-provider"
        Then the transformed history should contain a system message with the summary

    Scenario: Cancel mode throws OperationCanceledException
        Given an agent session with model "gpt-4o" from provider "test-provider"
        When the conversation is transformed with mode "Cancel" for model "claude-3-opus" on provider "test-provider"
        Then an OperationCanceledException should be thrown

    Scenario: Transform mode produces briefing document
        Given an agent session with model "gpt-4o" from provider "test-provider"
        And the session has conversation history with 4 messages
        When the conversation is transformed with mode "Transform" for model "claude-3-opus" on provider "test-provider"
        Then the transformed history should contain a system message
        And the briefing should not be null
