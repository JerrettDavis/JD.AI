@channels
Feature: Channel Adapters
  As the platform
  I need messaging channel adapters for multiple platforms
  So that I can receive and send messages across Discord, Slack, Telegram, Signal, and Web

  Scenario Outline: Each channel adapter reports correct type
    Given a <channel> channel adapter
    Then the channel type should be "<type>"
    And the display name should be "<name>"

    Examples:
      | channel   | type     | name              |
      | discord   | discord  | Discord           |
      | slack     | slack    | Slack             |
      | telegram  | telegram | Telegram          |
      | signal    | signal   | Signal (test-acc) |
      | web       | web      | WebChat           |

  Scenario Outline: Channel adapters start disconnected
    Given a <channel> channel adapter
    Then the channel should not be connected

    Examples:
      | channel   |
      | discord   |
      | slack     |
      | telegram  |
      | signal    |
      | web       |

  Scenario: Web channel connects and disconnects
    Given a web channel adapter
    When I connect the channel
    Then the channel should be connected
    When I disconnect the channel
    Then the channel should not be connected

  Scenario: Web channel ingests messages
    Given a web channel adapter
    And the channel is connected
    When a message "Hello" arrives from user "user-1" on connection "conn-1"
    Then the message received event should fire
    And the message content should be "Hello"
    And the message sender ID should be "user-1"

  Scenario: Web channel stores sent messages
    Given a web channel adapter
    And the channel is connected
    When I send "Reply" to conversation "conv-1"
    Then the conversation "conv-1" should have 1 stored message

  Scenario Outline: Command-aware channels accept command registration
    Given a <channel> channel adapter that supports commands
    When I register a command registry
    Then no error should occur

    Examples:
      | channel |
      | discord |
      | slack   |
      | signal  |

  Scenario Outline: Channel disposal is safe when not connected
    Given a <channel> channel adapter
    When I dispose the channel
    Then no error should occur

    Examples:
      | channel   |
      | discord   |
      | slack     |
      | telegram  |
      | signal    |
      | web       |
