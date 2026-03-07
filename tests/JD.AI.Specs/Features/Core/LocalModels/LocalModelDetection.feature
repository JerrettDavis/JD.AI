@localmodels
Feature: Local Model Detection
  As a platform
  I need to detect locally available models
  So that users can use local inference without cloud providers

  Scenario: Detect when no models are available
    Given a local model detector with an empty models directory
    When I detect local models
    Then the provider should not be available
    And the status message should contain "No models found"

  Scenario: Provider name is "Local"
    Given a local model detector
    Then the provider name should be "Local"

  Scenario: Detection handles errors gracefully
    Given a local model detector with an inaccessible directory
    When I detect local models
    Then the provider should not be available
    And the status message should contain "No models found"
