@ui
Feature: Chat Page
    As a gateway operator
    I want to interact with AI agents through a web chat interface
    So that I can test agent responses and have conversations

    Background:
        Given I am on the chat page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays chat header with title
        Then I should see the chat header
        And the header should display "Web Chat"
        And the header should have a chat icon

    @smoke
    Scenario: Displays message input area
        Then I should see the message input field
        And the message input should have placeholder "Type a message…"
        And the message input should have a send icon

    # ── Agent selector ────────────────────────────────────
    @requires-agents
    Scenario: Agent selector populates from running agents
        Then I should see the agent selector dropdown
        And the dropdown should contain at least one agent option

    Scenario: No agents warning when none running
        Given there are no running agents
        Then I should see "No agents running" warning chip

    # ── Empty state ───────────────────────────────────────
    Scenario: Empty state shown when no messages
        Then I should see the chat empty state
        And the empty state should display "Start a conversation"
        And the empty state should display "Type a message below"

    # ── Input behavior ────────────────────────────────────
    Scenario: Input disabled when no agent selected
        Given there are no running agents
        Then the message input should be disabled

    Scenario: Empty input does not send
        Given an agent is selected
        When I type "" in the message input
        And I press Enter in the message input
        Then no message bubble should appear

    # ── Sending messages ──────────────────────────────────
    @requires-agents
    Scenario: User message bubble appears after sending
        Given an agent is selected
        When I type "Hello" in the message input
        And I send the message
        Then a user message bubble should appear on the right
        And the message bubble should contain "Hello"
        And the user bubble should show "You" label
        And the user bubble should show a timestamp

    @requires-agents
    Scenario: Agent response streams with cursor indicator
        Given an agent is selected
        When I type "Hi there" in the message input
        And I send the message
        Then a streaming indicator should appear
        And an agent response bubble should eventually appear on the left

    # ── Clear chat ────────────────────────────────────────
    Scenario: Clear chat button disabled when empty
        Then the clear chat button should be disabled

    @requires-agents
    Scenario: Clear chat removes all messages
        Given an agent is selected
        And I have sent a message "test"
        When I click the clear chat button
        Then no message bubbles should be visible
        And the empty state should be displayed

    # ── Error handling ────────────────────────────────────
    @requires-agents
    Scenario: Error during chat shows snackbar
        Given an agent is selected
        When a chat error occurs
        Then an error snackbar should appear

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Chat — JD.AI"
