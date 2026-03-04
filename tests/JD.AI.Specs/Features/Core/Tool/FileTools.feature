@tools
Feature: File Tools
  As an AI agent
  I need file system operations
  So that I can read, write, edit, and list files

  Background:
    Given a temporary directory

  Scenario: Read an existing file
    Given a file "hello.txt" with content "Hello, World!"
    When I read file "hello.txt"
    Then the result should contain "Hello, World!"

  Scenario: Read a file with line range
    Given a file "lines.txt" with content:
      """
      Line 1
      Line 2
      Line 3
      Line 4
      Line 5
      """
    When I read file "lines.txt" from line 2 to line 4
    Then the result should contain "Line 2"
    And the result should contain "Line 4"
    And the result should not contain "Line 5"

  Scenario: Read a nonexistent file returns error
    When I read file "missing.txt"
    Then the result should contain "Error: File not found"

  Scenario: Write a new file
    When I write "New content" to file "output.txt"
    Then the result should contain "Wrote 11 characters"
    And the file "output.txt" should exist with content "New content"

  Scenario: Write creates parent directories
    When I write "nested" to file "a/b/c/deep.txt"
    Then the file "a/b/c/deep.txt" should exist with content "nested"

  Scenario: Edit replaces text in a file
    Given a file "code.cs" with content "var x = 1;"
    When I edit file "code.cs" replacing "var x = 1" with "var x = 42"
    Then the result should contain "Replaced"
    And the file "code.cs" should exist with content "var x = 42;"

  Scenario: Edit fails when old text not found
    Given a file "code.cs" with content "var x = 1;"
    When I edit file "code.cs" replacing "NOTFOUND" with "replacement"
    Then the result should contain "Error: old_str not found"

  Scenario: List directory contents
    Given a file "file1.txt" with content "a"
    And a file "file2.txt" with content "b"
    And a subdirectory "subdir"
    When I list the directory
    Then the result should contain "file1.txt"
    And the result should contain "file2.txt"
    And the result should contain "subdir/"
