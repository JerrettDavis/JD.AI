@governance
Feature: Audit Service
  As a governance system
  I need to emit audit events to configured sinks
  So that all actions are recorded for compliance

  Scenario: Emit audit event to a single sink
    Given an audit service with 1 mock sink
    When I emit an audit event with action "tool.invoke" and resource "read_file"
    Then the mock sink should have received 1 event
    And the received event action should be "tool.invoke"

  Scenario: Emit to multiple sinks
    Given an audit service with 3 mock sinks
    When I emit an audit event with action "model.request"
    Then all 3 sinks should have received 1 event each

  Scenario: Failing sink does not block other sinks
    Given an audit service with 1 failing sink and 1 working sink
    When I emit an audit event with action "test.event"
    Then the working sink should have received 1 event

  Scenario: Flush calls flush on all sinks
    Given an audit service with 2 mock sinks
    When I flush the audit service
    Then all sinks should have been flushed

  Scenario: Audit event has expected properties
    Given an audit service with 1 mock sink
    When I emit an audit event with action "policy.deny" and severity "Warning"
    Then the received event should have severity "Warning"
    And the received event should have a non-empty event ID
