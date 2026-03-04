@governance
Feature: Audit Sinks
  As a governance system
  I need audit events written to various destinations
  So that audit data is persisted and accessible

  Scenario: File sink writes to daily-rotated file
    Given a file audit sink with a temporary directory
    When I write an audit event with action "file.test"
    Then a JSONL audit file should exist for today
    And the audit file should contain "file.test"

  Scenario: File sink writes multiple events
    Given a file audit sink with a temporary directory
    When I write 3 audit events
    Then the audit file should contain 3 lines

  Scenario: Webhook sink posts event as JSON
    Given a webhook audit sink with a mock HTTP handler
    When I write an audit event with action "webhook.test"
    Then the mock HTTP handler should have received 1 POST request
    And the POST body should contain "webhook.test"

  Scenario: Webhook sink swallows errors
    Given a webhook audit sink that returns HTTP 500
    When I write an audit event with action "error.test"
    Then no exception should be thrown
