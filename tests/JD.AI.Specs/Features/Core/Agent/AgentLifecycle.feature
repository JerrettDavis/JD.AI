@core @agent
Feature: Agent Lifecycle
    As a user of the AI agent
    I want to create sessions, send messages, and receive responses
    So that I can interact with AI models reliably

    Background:
        Given a provider registry with a "test-provider" provider
        And a mock chat service that returns "Hello, world!"
        And an agent session with model "gpt-4o" from provider "test-provider"

    Scenario: Session creation initializes empty history
        Then the session history should be empty
        And the current model should be "gpt-4o"

    Scenario: Non-streaming turn execution returns response
        When the user sends "Hello"
        Then the response should be "Hello, world!"
        And the session history should contain 2 messages

    Scenario: Response is added to session history
        When the user sends "Hello"
        Then the session history should contain a user message "Hello"
        And the session history should contain an assistant message "Hello, world!"

    Scenario: Turn index increments after each turn
        When the user sends "First message"
        And the user sends "Second message"
        Then the session history should contain 4 messages

    Scenario: Error during turn returns error message and records in history
        Given a mock chat service that throws "Service unavailable"
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "Hello"
        Then the response should contain "Error:"
        And the agent output should have rendered an error

    Scenario: Clearing history resets conversation
        When the user sends "Hello"
        And the user clears the history
        Then the session history should be empty

    Scenario Outline: Multiple turns accumulate history correctly
        When the user sends "<message1>"
        And the user sends "<message2>"
        Then the session history should contain <count> messages

        Examples:
            | message1 | message2     | count |
            | Hi       | How are you? | 4     |
            | Start    | Continue     | 4     |
