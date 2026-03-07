@tools
Feature: Task Tools
  As an AI agent
  I need task management
  So that I can track work items within a session

  Background:
    Given a new task tracker

  Scenario: Create a task
    When I create a task "Fix the login bug" with priority "high"
    Then the task result should contain "Created task"
    And the task result should contain "Fix the login bug"

  Scenario: List all tasks
    Given I have created task "Task A"
    And I have created task "Task B"
    When I list all tasks
    Then the task result should contain "Task A"
    And the task result should contain "Task B"

  Scenario: List tasks filtered by status
    Given I have created task "Pending task"
    And I have created and completed task "Done task"
    When I list tasks with status "done"
    Then the task result should contain "Done task"
    And the task result should not contain "Pending task"

  Scenario: Update task status
    Given I have created task "My task" with id "task-1"
    When I update task "task-1" status to "in_progress"
    Then the task result should contain "Updated task task-1"
    And the task result should contain "status=in_progress"

  Scenario: Complete a task
    Given I have created task "My task" with id "task-1"
    When I complete task "task-1"
    Then the task result should contain "Updated task task-1"
    And the task result should contain "status=done"

  Scenario: Update nonexistent task returns error
    When I update task "task-999" status to "done"
    Then the task result should contain "not found"

  Scenario: Export tasks as JSON
    Given I have created task "Export me"
    When I export tasks
    Then the task result should contain "Export me"
    And the task result should be valid JSON
