@core @session @export
Feature: Session Export
    As a user of the AI agent
    I want to export sessions to JSON and markdown
    So that I can archive or share conversations

    Background:
        Given a temporary export directory

    Scenario: Exports session to JSON file
        Given a session with ID "abc123" for project "/tmp/project" with hash "aabbccdd"
        And the session has a user turn "Hello" and assistant turn "Hi there!"
        When the session is exported to JSON
        Then a JSON file should exist at the expected export path
        And the exported JSON should contain the session ID "abc123"

    Scenario: Exported JSON contains turn data
        Given a session with ID "def456" for project "/tmp/project" with hash "aabbccdd"
        And the session has a user turn "Explain quantum computing" and assistant turn "Quantum computing uses qubits"
        When the session is exported to JSON
        Then the exported JSON should contain "Explain quantum computing"
        And the exported JSON should contain role "user"
        And the exported JSON should contain role "assistant"

    Scenario: Lists exported session files
        Given a session with ID "sess001" for project "/tmp/project" with hash "aabbccdd"
        And the session is exported to JSON
        And a session with ID "sess002" for project "/tmp/project" with hash "aabbccdd"
        And the session is exported to JSON
        When exported files for project hash "aabbccdd" are listed
        Then the export file list should contain 2 entries
