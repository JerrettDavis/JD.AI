@tools
Feature: Question Tools
  As an AI agent
  I need to present structured questions to the user
  So that I can gather specific information before proceeding

  Scenario: Ask a simple text question and receive answer
    Given a questionnaire runner that returns answers:
      | key      | answer    |
      | name     | Alice     |
    When I ask questions with title "User Info":
      """
      [{"key":"name","prompt":"What is your name?","type":"Text","required":true}]
      """
    Then the question result should contain "completed"
    And the question result should contain "Alice"

  Scenario: User cancels questionnaire
    Given a questionnaire runner that cancels
    When I ask questions with title "Cancelled":
      """
      [{"key":"choice","prompt":"Pick one","type":"Text"}]
      """
    Then the question result should indicate cancellation

  Scenario: Invalid JSON returns error
    Given a questionnaire runner that returns answers:
      | key  | answer |
      | x    | y      |
    When I ask questions with title "Bad JSON":
      """
      NOT VALID JSON
      """
    Then the question result should contain "error"

  Scenario: Empty questions array returns error
    Given a questionnaire runner that returns answers:
      | key  | answer |
      | x    | y      |
    When I ask questions with title "Empty":
      """
      []
      """
    Then the question result should contain "No questions provided"
