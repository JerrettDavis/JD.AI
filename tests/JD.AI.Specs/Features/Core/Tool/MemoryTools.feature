@tools
Feature: Memory Tools
  As an AI agent
  I need semantic memory storage and retrieval
  So that I can remember and recall information across turns

  Scenario: Store a fact in memory
    Given a configured semantic memory
    When I store "The project uses .NET 10" with category "fact"
    Then the memory result should contain "Stored in memory with ID"

  Scenario: Search memory returns relevant results
    Given a configured semantic memory
    And I have stored "The sky is blue" in memory
    When I search memory for "sky color"
    Then the memory result should contain "sky is blue"

  Scenario: Search memory with no results
    Given a configured semantic memory
    When I search memory for "quantum physics"
    Then the memory result should be "No relevant memories found."

  Scenario: Forget a memory entry
    Given a configured semantic memory
    And I have stored "temporary data" in memory with id "mem-1"
    When I forget memory "mem-1"
    Then the memory result should contain "Removed memory mem-1"

  Scenario: Memory unavailable without embedding model
    Given no semantic memory configured
    When I store "data" with category "fact"
    Then the memory result should contain "Memory is not available"

  Scenario: Memory search unavailable without embedding model
    Given no semantic memory configured
    When I search memory for "anything"
    Then the memory result should contain "Memory is not available"
