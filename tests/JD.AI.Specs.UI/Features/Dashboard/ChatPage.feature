@ui
Feature: Chat Page
    As a gateway operator
    I want to interact with AI agents through a web chat interface
    So that I can test agent responses and have conversations

    Background:
        Given I am on the chat page

    Scenario: Displays chat header with title
        Then I should see the web chat header
        And the header should display "Web Chat"

    Scenario: Displays message input area
        Then I should see the message input field
        And the message input should have placeholder text

    Scenario: Agent selector is available
        Then I should see the agent selector or no-agents warning

    Scenario: Empty state shown when no messages
        Then I should see the chat empty state
        And the empty state should display "Start a conversation"

    Scenario: Message history is displayed after sending
        Given an agent is selected
        When I type "Hello" in the message input
        And I send the message
        Then a user message bubble should appear
        And the message bubble should contain "Hello"

    Scenario: Clear chat button is available
        Then I should see the clear chat button
