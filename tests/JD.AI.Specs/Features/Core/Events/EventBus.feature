@events
Feature: Event Bus
  As a platform
  I need a publish/subscribe event bus
  So that components can communicate through events

  Background:
    Given an in-process event bus

  Scenario: Publish event and subscriber receives it
    Given a subscriber for event type "tool.invoked"
    When I publish an event of type "tool.invoked" from source "agent-1"
    Then the subscriber should have received 1 event
    And the received event type should be "tool.invoked"

  Scenario: Subscriber with filter only receives matching events
    Given a subscriber for event type "tool.invoked"
    When I publish an event of type "model.requested" from source "agent-1"
    Then the subscriber should have received 0 events

  Scenario: Multiple subscribers receive the same event
    Given 3 subscribers for event type "status.changed"
    When I publish an event of type "status.changed" from source "system"
    Then all 3 subscribers should have received 1 event

  Scenario: Unsubscribed handler stops receiving events
    Given a subscriber for event type "test.event"
    When the subscriber is disposed
    And I publish an event of type "test.event" from source "test"
    Then the subscriber should have received 0 events

  Scenario: Subscriber with no filter receives all events
    Given a subscriber with no event type filter
    When I publish an event of type "any.event" from source "test"
    Then the subscriber should have received 1 event
