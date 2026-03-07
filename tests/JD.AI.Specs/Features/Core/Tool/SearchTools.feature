@tools
Feature: Search Tools
  As an AI agent
  I need to search code by pattern and file name
  So that I can find relevant code quickly

  Background:
    Given a temporary directory with search files

  Scenario: Grep finds matching lines
    When I grep for "hello" in the search directory
    Then the grep result should contain "match"
    And the grep result should contain "hello"

  Scenario: Grep with no matches returns message
    When I grep for "ZZZNOMATCH" in the search directory
    Then the grep result should be "No matches found."

  Scenario: Grep with case-insensitive flag
    When I grep for "HELLO" case-insensitively in the search directory
    Then the grep result should contain "hello"

  Scenario: Grep with invalid regex returns error
    When I grep for "[invalid" in the search directory
    Then the grep result should contain "Error: Invalid regex"

  Scenario: Glob finds files matching pattern
    When I glob for "*.txt" in the search directory
    Then the glob result should contain "sample.txt"

  Scenario: Glob with no matches returns message
    When I glob for "*.xyz" in the search directory
    Then the glob result should be "No files found."

  Scenario: Grep with file glob filter
    When I grep for "hello" with glob "*.txt" in the search directory
    Then the grep result should contain "sample.txt"
