@channels
Feature: Channel Registry
  As a platform
  I need to manage messaging channel adapters
  So that I can route messages across platforms

  Background:
    Given a channel registry

  Scenario: Register a channel
    When I register a channel of type "discord"
    Then the registry should contain 1 channel
    And the registry should have channel type "discord"

  Scenario: List registered channels
    Given I have registered channels:
      | channelType |
      | discord     |
      | slack       |
      | signal      |
    When I list all channels
    Then I should see 3 channels

  Scenario: Get channel by type
    Given I have registered a channel of type "telegram"
    When I get channel "telegram"
    Then the returned channel type should be "telegram"

  Scenario: Get nonexistent channel returns null
    When I get channel "nonexistent"
    Then the returned channel should be null

  Scenario: Remove a channel by type
    Given I have registered a channel of type "slack"
    When I unregister channel "slack"
    Then the registry should contain 0 channels
