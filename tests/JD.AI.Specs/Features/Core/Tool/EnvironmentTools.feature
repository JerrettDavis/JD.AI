@tools
Feature: Environment Tools
  As an AI agent
  I need to inspect the runtime environment
  So that I can understand the system context

  Scenario: Get basic environment info
    When I get environment info
    Then the environment result should contain "OS:"
    And the environment result should contain "Architecture:"
    And the environment result should contain "Working Directory:"

  Scenario: Environment info includes .NET runtime
    When I get environment info
    Then the environment result should contain ".NET Runtime:"

  Scenario: Environment masks sensitive variables
    When I get environment info with env vars
    Then any variable containing "KEY" should show "***"
