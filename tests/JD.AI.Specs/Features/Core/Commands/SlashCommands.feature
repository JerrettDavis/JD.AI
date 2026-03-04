@commands
Feature: Slash Commands
  As a platform
  I need command routing
  So that users can invoke commands from any channel

  Background:
    Given a command registry

  Scenario: Register and look up a command
    Given a registered command "help" with description "Show help"
    When I look up command "help"
    Then the command should be found
    And the command description should be "Show help"

  Scenario: Unknown command returns null
    When I look up command "nonexistent"
    Then the command should not be found

  Scenario: Command lookup is case-insensitive
    Given a registered command "Status" with description "Show status"
    When I look up command "status"
    Then the command should be found

  Scenario: Register replaces existing command
    Given a registered command "help" with description "Old help"
    And a registered command "help" with description "New help"
    When I look up command "help"
    Then the command description should be "New help"

  Scenario: List all registered commands
    Given a registered command "help" with description "Show help"
    And a registered command "status" with description "Show status"
    When I list all commands
    Then I should see 2 commands
