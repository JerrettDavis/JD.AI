@workflows
Feature: Workflow Capture
  As a platform
  I need to capture workflow execution events
  So that I can observe and replay workflow runs

  Scenario: Capture step started event
    Given a workflow execution capture
    When a step "analyze-code" starts
    Then the capture should have 1 event
    And the last event should be a "Started" event for step "analyze-code"

  Scenario: Capture step completed event
    Given a workflow execution capture
    When a step "analyze-code" starts
    And the step "analyze-code" completes
    Then the completed count should be 1
    And the last event should be a "Completed" event for step "analyze-code"

  Scenario: Capture step failed event
    Given a workflow execution capture
    When a step "build" starts
    And the step "build" fails with error "Build failed"
    Then the failed count should be 1
    And the last event error should be "Build failed"

  Scenario: Build workflow definition from steps
    Given a workflow definition "test-flow" with steps:
      | name        | kind  | target     |
      | read-code   | Skill | read-code  |
      | analyze     | Skill | analyze    |
    When I build the workflow
    Then the workflow should be created successfully

  Scenario: Create workflow data with prompt
    Given a workflow builder with a kernel
    When I create workflow data with prompt "Fix the bug"
    Then the workflow data prompt should be "Fix the bug"
