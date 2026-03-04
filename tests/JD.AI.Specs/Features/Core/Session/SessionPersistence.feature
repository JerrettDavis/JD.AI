@core @session
Feature: Session Persistence
    As a user of the AI agent
    I want sessions to be saved and loaded from storage
    So that I can resume conversations across restarts

    Background:
        Given a session store backed by a temporary database

    Scenario: Creates a new session
        When a new session is created for project "/tmp/my-project"
        Then the session should have a non-empty ID
        And the session should be marked as active
        And the session should have project path "/tmp/my-project"

    Scenario: Saves and loads session by ID
        Given a session exists for project "/tmp/my-project"
        When the session is loaded by its ID
        Then the loaded session should have the same ID
        And the loaded session should be marked as active

    Scenario: Saves turns and loads them with session
        Given a session exists for project "/tmp/my-project"
        And a user turn "Hello" is saved to the session
        And an assistant turn "Hi there!" is saved to the session
        When the session is loaded by its ID
        Then the loaded session should have 2 turns
        And the first turn should have role "user" and content "Hello"
        And the second turn should have role "assistant" and content "Hi there!"

    Scenario: Lists sessions ordered by update time
        Given a session exists for project "/tmp/project-a"
        And a session exists for project "/tmp/project-b"
        When sessions are listed
        Then the session list should contain 2 entries

    Scenario: Deletes session turns after a given index
        Given a session exists for project "/tmp/my-project"
        And a user turn "Turn 0" is saved to the session
        And an assistant turn "Response 0" is saved to the session
        And a user turn "Turn 1" is saved to the session
        And an assistant turn "Response 1" is saved to the session
        When turns after index 1 are deleted
        And the session is loaded by its ID
        Then the loaded session should have 2 turns

    Scenario: Closes a session
        Given a session exists for project "/tmp/my-project"
        When the session is closed
        And the session is loaded by its ID
        Then the loaded session should be marked as inactive
