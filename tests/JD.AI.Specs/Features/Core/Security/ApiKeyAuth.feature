@security
Feature: API Key Authentication
  As a gateway security system
  I need API key authentication
  So that only authorized users can access the system

  Background:
    Given an API key auth provider

  Scenario: Valid key authenticates successfully
    Given a registered key "sk-valid-123" for user "Alice" with role "User"
    When I authenticate with key "sk-valid-123"
    Then authentication should succeed
    And the identity display name should be "Alice"
    And the identity role should be "User"

  Scenario: Invalid key is rejected
    Given a registered key "sk-valid-123" for user "Alice" with role "User"
    When I authenticate with key "sk-wrong-key"
    Then authentication should fail

  Scenario: Missing key is rejected
    When I authenticate with key ""
    Then authentication should fail

  Scenario: Admin role key authenticates
    Given a registered key "sk-admin-key" for user "Admin" with role "Admin"
    When I authenticate with key "sk-admin-key"
    Then authentication should succeed
    And the identity role should be "Admin"

  Scenario: Multiple keys can be registered
    Given a registered key "sk-key-1" for user "User1" with role "User"
    And a registered key "sk-key-2" for user "User2" with role "Operator"
    When I authenticate with key "sk-key-2"
    Then authentication should succeed
    And the identity display name should be "User2"
