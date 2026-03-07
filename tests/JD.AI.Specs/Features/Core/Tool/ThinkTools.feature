@tools
Feature: Think Tools
  As an AI agent
  I need a scratchpad for reasoning
  So that I can organize thoughts without side effects

  Scenario: Record a thought
    When I think "I should check the database schema first"
    Then the think result should contain "[Thought recorded]"
    And the think result should contain "I should check the database schema first"

  Scenario: Record an empty thought
    When I think ""
    Then the think result should contain "[Thought recorded]"

  Scenario: Think preserves multi-line reasoning
    When I think:
      """
      Step 1: Read the file
      Step 2: Parse the JSON
      Step 3: Update the field
      """
    Then the think result should contain "Step 1: Read the file"
    And the think result should contain "Step 3: Update the field"
