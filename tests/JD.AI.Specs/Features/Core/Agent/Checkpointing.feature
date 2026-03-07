@core @agent @checkpointing
Feature: Agent Checkpointing
    As a user of the AI agent
    I want to create and restore checkpoints of project state
    So that I can recover from unwanted changes

    Scenario: Stash checkpoint strategy creates checkpoint ID
        Given a stash checkpoint strategy for a git working directory
        And the working directory has uncommitted changes
        When a checkpoint is created with label "before-refactor"
        Then the checkpoint ID should contain "jdai-cp-before-refactor"

    Scenario: Stash checkpoint returns null when no changes exist
        Given a stash checkpoint strategy for a clean git working directory
        When a checkpoint is created with label "empty"
        Then the checkpoint ID should be null

    Scenario: Directory checkpoint persists files to disk
        Given a directory checkpoint strategy for a temporary directory
        And the working directory contains source files
        When a checkpoint is created with label "snapshot-1"
        Then the checkpoint ID should not be null
        And the checkpoint directory should contain the source files

    Scenario: Directory checkpoint lists existing checkpoints
        Given a directory checkpoint strategy for a temporary directory
        And the working directory contains source files
        And a checkpoint exists with label "snapshot-1"
        And a checkpoint exists with label "snapshot-2"
        When the checkpoints are listed
        Then the checkpoint list should contain 2 entries

    Scenario: Directory checkpoint restores files
        Given a directory checkpoint strategy for a temporary directory
        And the working directory contains a file "test.cs" with content "original"
        And a checkpoint exists with label "restore-point"
        And the working directory file "test.cs" is modified to "modified"
        When the checkpoint "restore-point" is restored
        Then the file "test.cs" should contain "original"

    Scenario: Commit checkpoint creates a git commit
        Given a commit checkpoint strategy for a git working directory
        And the working directory has uncommitted changes
        When a checkpoint is created with label "safe-point"
        Then the checkpoint ID should not be null

    Scenario: Clear removes all directory checkpoints
        Given a directory checkpoint strategy for a temporary directory
        And the working directory contains source files
        And a checkpoint exists with label "old-1"
        And a checkpoint exists with label "old-2"
        When all checkpoints are cleared
        Then the checkpoints list should be empty
