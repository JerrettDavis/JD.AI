@tools
Feature: Batch Edit Tools
  As an AI agent
  I need to apply multiple edits atomically
  So that coordinated changes across files succeed or fail together

  Background:
    Given a temporary directory

  Scenario: Apply a single edit to one file
    Given a file "app.cs" with content "Console.WriteLine(\"Hello\");"
    When I batch edit replacing "Hello" with "World" in "app.cs"
    Then the batch result should contain "Applied 1 edit(s) across 1 file(s)"
    And the file "app.cs" should exist with content "Console.WriteLine(\"World\");"

  Scenario: Apply multiple edits to the same file
    Given a file "app.cs" with content "var a = 1; var b = 2;"
    When I batch edit with multiple replacements in "app.cs":
      | oldText   | newText   |
      | var a = 1 | var a = 10 |
      | var b = 2 | var b = 20 |
    Then the batch result should contain "Applied 2 edit(s)"

  Scenario: Atomic failure when old text not found
    Given a file "a.cs" with content "original-a"
    And a file "b.cs" with content "original-b"
    When I batch edit with an invalid replacement in "b.cs"
    Then the batch result should contain "Error"
    And the file "a.cs" should exist with content "original-a"
    And the file "b.cs" should exist with content "original-b"

  Scenario: Empty edits returns message
    When I batch edit with no edits
    Then the batch result should be "No edits provided."

  Scenario: Missing file returns error
    When I batch edit replacing "x" with "y" in "nonexistent.cs"
    Then the batch result should contain "Error: file not found"
