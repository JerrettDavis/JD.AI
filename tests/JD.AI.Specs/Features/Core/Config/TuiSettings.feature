@core @config @tui
Feature: TUI Settings
    As a user of the AI agent
    I want to configure the terminal UI display settings
    So that I can personalize my experience

    Background:
        Given a temporary data directory for TUI settings

    Scenario: Loads default settings when no file exists
        When TUI settings are loaded
        Then the spinner style should be "Normal"
        And the system prompt budget percent should be 20
        And the output style should be "Rich"
        And prompt caching should be enabled

    Scenario: Persists changed settings
        Given TUI settings are saved with spinner style "Nerdy"
        When TUI settings are loaded
        Then the spinner style should be "Nerdy"

    Scenario Outline: Spinner style options
        Given TUI settings are saved with spinner style "<style>"
        When TUI settings are loaded
        Then the spinner style should be "<style>"

        Examples:
            | style   |
            | None    |
            | Minimal |
            | Normal  |
            | Rich    |
            | Nerdy   |

    Scenario: System prompt budget is clamped to valid range
        Given TUI settings are saved with system prompt budget 150
        When TUI settings are loaded
        Then the system prompt budget percent should be 100

    Scenario: Vim mode defaults to disabled
        When TUI settings are loaded
        Then vim mode should be disabled

    Scenario: Prompt cache TTL setting persists
        Given TUI settings are saved with prompt cache TTL "OneHour"
        When TUI settings are loaded
        Then the prompt cache TTL should be "OneHour"
