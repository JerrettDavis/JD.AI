@ui
Feature: Chat Page
    As a gateway operator
    I want to interact with AI agents through a web chat interface
    So that I can test agent responses and have conversations

    Background:
        Given I am on the chat page

    @smoke
    Scenario: Displays chat header and baseline controls
        Then I should see the chat header
        And the header should display "Chat"
        And the header should have a chat icon
        And the message input should have placeholder "Type a message…"
        And the message input should have a send icon
        And I should see the agent selector or no-agents warning
        And the clear chat button should be disabled

    @smoke
    Scenario: Empty state shown before any messages
        Then I should see the chat empty state
        And the empty state should display "Start a conversation"
        And the empty state should display "Type a message below to chat with the selected agent."

    @requires-agents
    Scenario: User message bubble appears after sending
        Given an agent is selected
        When I type "Hello from Reqnroll" in the message input
        And I send the message
        Then a user message bubble should appear on the right
        And the message bubble should contain "Hello from Reqnroll"
        And the user bubble should show "You" label
        And the user bubble should show a timestamp

    @smoke
    Scenario: Page title is correct
        Then the browser page title should be "Chat — JD.AI"

    @requires-agents
    Scenario: Cancel button appears during streaming and stops response
        Given an agent is selected
        When I type "Tell me a long story" in the message input
        And I send the message
        Then a streaming cancel button should be visible
        When I click the streaming cancel button
        Then the streaming cancel button should disappear
        And the message input should be enabled
