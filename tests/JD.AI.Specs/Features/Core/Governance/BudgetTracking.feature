@governance
Feature: Budget Tracking
  As a governance system
  I need to track spending against budget limits
  So that costs remain within configured thresholds

  Background:
    Given a budget tracker with a temporary file

  Scenario: Record spend and check status
    When I record $0.50 spend for provider "Anthropic"
    And I get the budget status
    Then the daily spend should be at least $0.50

  Scenario: Within budget returns true when no policy set
    When I check if within budget with no policy
    Then the result should be within budget

  Scenario: Exceeds daily budget limit
    Given a budget policy with daily limit $1.00
    When I record $1.50 spend for provider "OpenAI"
    And I check if within budget
    Then the result should not be within budget

  Scenario: Exceeds monthly budget limit
    Given a budget policy with monthly limit $10.00
    When I record $11.00 spend for provider "OpenAI"
    And I check if within budget
    Then the result should not be within budget

  Scenario: Alert triggers at threshold
    Given a budget policy with daily limit $10.00 and alert at 80 percent
    When I record $8.50 spend for provider "Anthropic"
    And I get the budget status with policy
    Then the alert should be triggered

  Scenario: Multiple providers tracked separately
    When I record $1.00 spend for provider "OpenAI"
    And I record $2.00 spend for provider "Anthropic"
    And I get the budget status
    Then the daily spend should be at least $3.00
