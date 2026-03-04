@tools
Feature: Clipboard Tools
  As an AI agent
  I need clipboard access
  So that I can read and write system clipboard content

  Scenario: Write text to clipboard
    When I write "clipboard content" to the clipboard
    Then the clipboard result should contain "Copied"

  Scenario: Read from clipboard
    When I read the clipboard
    Then the clipboard result should not be empty
