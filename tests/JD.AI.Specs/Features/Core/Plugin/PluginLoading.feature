@plugin
Feature: Plugin Loading
  As a platform
  I need dynamic plugin loading
  So that extensions can be added at runtime

  Scenario: Discover plugins from empty directory
    Given an empty plugin directory
    When I load plugins from the directory
    Then no plugins should be loaded

  Scenario: Load a single plugin assembly
    Given a valid plugin assembly
    When I load the plugin assembly
    Then the plugin should be in the loaded list

  Scenario: Invalid assembly is handled gracefully
    Given a plugin directory with an invalid DLL
    When I load plugins from the directory
    Then no plugins should be loaded
    And no exception should be thrown

  Scenario: Unload a loaded plugin
    Given a loaded plugin "test-plugin"
    When I unload plugin "test-plugin"
    Then the plugin list should be empty

  Scenario: Missing directory returns empty list
    When I load plugins from a nonexistent directory
    Then no plugins should be loaded
