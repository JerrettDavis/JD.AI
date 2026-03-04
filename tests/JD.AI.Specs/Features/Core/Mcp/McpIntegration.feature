@mcp
Feature: MCP Integration
  As a platform
  I need MCP server management
  So that I can discover and manage Model Context Protocol servers

  Scenario: List servers from registry
    Given an MCP manager with a mock registry containing servers:
      | name      | command    |
      | server-a  | npx srv-a  |
      | server-b  | npx srv-b  |
    When I list all MCP servers
    Then I should see 2 servers

  Scenario: Add a new MCP server
    Given an MCP manager with a writable provider
    When I add an MCP server "my-server" with command "npx my-tool"
    Then the server "my-server" should be in the configuration

  Scenario: Remove an MCP server
    Given an MCP manager with a writable provider containing "old-server"
    When I remove MCP server "old-server"
    Then the server "old-server" should not be in the configuration

  Scenario: Get status of unchecked server returns default
    Given an MCP manager with a mock registry
    When I get the status of "unknown-server"
    Then the status should be the default status

  Scenario: Set and get server status
    Given an MCP manager with a mock registry
    When I set the status of "test-server" to connected
    And I get the status of "test-server"
    Then the status should be connected
