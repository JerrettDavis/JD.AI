@core @agent @model-switching
Feature: Model Switching
    As a user of the AI agent
    I want to switch between AI models during a session
    So that I can use the best model for each task

    Background:
        Given a provider registry with a "test-provider" provider
        And a mock chat service that returns "Hello"
        And an agent session with model "gpt-4o" from provider "test-provider"

    Scenario: Switch model records in history
        When the user switches to model "claude-3-opus" on provider "test-provider"
        Then the model switch history should contain 1 entry
        And the latest model switch should be to "claude-3-opus"

    Scenario: Switch creates a fork point
        When the user switches to model "claude-3-opus" on provider "test-provider"
        Then the fork points should contain 1 entry
        And the latest fork point should reference model "gpt-4o"

    Scenario: Model switch fires the ModelChanged event
        Given the session has a ModelChanged event handler attached
        When the user switches to model "claude-3-opus" on provider "test-provider"
        Then the ModelChanged event should have fired with model "claude-3-opus"

    Scenario: Multiple switches create ordered history
        When the user switches to model "claude-3-opus" on provider "test-provider"
        And the user switches to model "gemini-pro" on provider "test-provider"
        And the user switches to model "gpt-4o-mini" on provider "test-provider"
        Then the model switch history should contain 3 entries
        And the fork points should contain 3 entries

    Scenario Outline: Switch modes are recorded correctly
        When the user switches to model "claude-3-opus" on provider "test-provider" with mode "<mode>"
        Then the latest model switch should have mode "<mode>"

        Examples:
            | mode      |
            | preserve  |
            | compact   |
            | fresh     |
