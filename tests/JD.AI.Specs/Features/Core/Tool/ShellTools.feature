@tools
Feature: Shell Tools
  As an AI agent
  I need to execute shell commands
  So that I can run builds, tests, and system operations

  Scenario: Execute a simple command
    When I run shell command "echo hello"
    Then the shell result should contain "Exit code: 0"
    And the shell result should contain "hello"

  Scenario: Capture exit code for failing command
    When I run shell command "exit 1"
    Then the shell result should contain "Exit code: 1"

  Scenario: Command with working directory
    Given a temporary directory
    When I run shell command "echo test" in the temporary directory
    Then the shell result should contain "test"

  Scenario: Command output is truncated when too long
    When I run a command that produces very long output
    Then the shell result should contain "truncated"

  Scenario Outline: Execute platform-appropriate commands
    When I run shell command "<command>"
    Then the shell result should contain "Exit code: 0"

    Examples:
      | command                 |
      | echo test               |
      | echo multi word output  |
