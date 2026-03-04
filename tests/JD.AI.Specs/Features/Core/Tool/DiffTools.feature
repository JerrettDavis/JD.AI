@tools
Feature: Diff Tools
  As an AI agent
  I need to create and apply patches
  So that I can make structured multi-location file edits

  Background:
    Given a temporary directory

  Scenario: Create a unified diff patch
    Given a file "target.cs" with content "var x = 1;"
    When I create a patch replacing "var x = 1" with "var x = 42" in "target.cs"
    Then the patch result should contain "--- a/"
    And the patch result should contain "+++ b/"

  Scenario: Apply a patch successfully
    Given a file "target.cs" with content "var x = 1;"
    When I apply a patch replacing "var x = 1" with "var x = 42" in "target.cs"
    Then the patch result should contain "Applied 1 edit(s)"
    And the file "target.cs" should exist with content "var x = 42;"

  Scenario: Apply patch fails atomically when old text not found
    Given a file "a.cs" with content "aaa"
    And a file "b.cs" with content "bbb"
    When I apply a patch with edits that fail on "b.cs"
    Then the patch result should contain "Error"
    And the file "a.cs" should exist with content "aaa"

  Scenario: Create patch for nonexistent file
    When I create a patch replacing "old" with "new" in "missing.cs"
    Then the patch result should contain "file not found"

  Scenario: Apply patch with missing path returns error
    When I apply a patch with a missing path field
    Then the patch result should contain "Error"
