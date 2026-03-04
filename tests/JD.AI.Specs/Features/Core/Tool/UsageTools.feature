@tools
Feature: Usage Tools
  As an AI agent
  I need to track token usage
  So that I can report costs and monitor consumption

  Scenario: Get usage with no activity
    Given a new usage tracker
    When I get usage statistics
    Then the usage result should contain "Prompt tokens: 0"
    And the usage result should contain "Total tokens: 0"

  Scenario: Record and report usage
    Given a new usage tracker
    And I have recorded 1000 prompt tokens and 500 completion tokens with 3 tool calls
    When I get usage statistics
    Then the usage result should contain "Prompt tokens: 1,000"
    And the usage result should contain "Completion tokens: 500"
    And the usage result should contain "Tool calls: 3"

  Scenario: Reset usage counters
    Given a new usage tracker
    And I have recorded 1000 prompt tokens and 500 completion tokens with 3 tool calls
    When I reset usage
    Then the usage result should be "Usage counters reset."
    When I get usage statistics
    Then the usage result should contain "Total tokens: 0"

  Scenario: Usage accumulates across multiple recordings
    Given a new usage tracker
    And I have recorded 100 prompt tokens and 50 completion tokens with 1 tool calls
    And I have recorded 200 prompt tokens and 100 completion tokens with 2 tool calls
    When I get usage statistics
    Then the usage result should contain "Prompt tokens: 300"
    And the usage result should contain "Tool calls: 3"
