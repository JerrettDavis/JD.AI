@tools
Feature: Git Tools
  As an AI agent
  I need git operations
  So that I can manage version control

  Background:
    Given a temporary git repository

  Scenario: Get status of a clean repository
    When I run git status
    Then the result should contain "no output"

  Scenario: Get status with modified files
    Given a tracked file "readme.md" with content "initial"
    And the file "readme.md" is modified to "updated"
    When I run git status
    Then the result should contain "readme.md"

  Scenario: Get diff of unstaged changes
    Given a tracked file "code.cs" with content "original"
    And the file "code.cs" is modified to "changed"
    When I run git diff
    Then the result should contain "changed"

  Scenario: View commit log
    Given a tracked file "file.txt" with content "content"
    When I run git log with count 5
    Then the result should contain "Initial commit"

  Scenario: Create a commit
    Given a tracked file "file.txt" with content "content"
    And the file "file.txt" is modified to "new content"
    When I commit with message "Update file"
    Then the result should contain "Update file"

  Scenario: List branches
    When I list branches
    Then the result should contain "main"

  Scenario: Create a new branch
    When I create branch "feature-test"
    Then the result should not contain "Error"
