@governance
Feature: Policy Evaluation
  As a governance system
  I need to evaluate requests against policies
  So that I can enforce tool, provider, and model restrictions

  Scenario: Allow tool when no policy is configured
    Given a policy with no tool restrictions
    When I evaluate tool "read_file"
    Then the policy decision should be "Allow"

  Scenario: Deny tool in the denied list
    Given a policy denying tools:
      | tool       |
      | run_command |
    When I evaluate tool "run_command"
    Then the policy decision should be "Deny"
    And the policy reason should contain "denied list"

  Scenario: Allow tool in the allowed list
    Given a policy allowing only tools:
      | tool      |
      | read_file |
      | write_file |
    When I evaluate tool "read_file"
    Then the policy decision should be "Allow"

  Scenario: Deny tool not in the allowed list
    Given a policy allowing only tools:
      | tool      |
      | read_file |
    When I evaluate tool "run_command"
    Then the policy decision should be "Deny"
    And the policy reason should contain "not in the allowed list"

  Scenario: Deny provider in the denied list
    Given a policy denying providers:
      | provider |
      | OpenAI   |
    When I evaluate provider "OpenAI"
    Then the policy decision should be "Deny"

  Scenario: Allow provider when no restrictions
    Given a policy with no provider restrictions
    When I evaluate provider "Anthropic"
    Then the policy decision should be "Allow"

  Scenario: Deny model matching denied pattern
    Given a policy denying models matching "gpt-*"
    When I evaluate model "gpt-4o"
    Then the policy decision should be "Deny"

  Scenario: Deny model exceeding max context window
    Given a policy with max context window 8000
    When I evaluate model "large-model" with context window 16000
    Then the policy decision should be "Deny"
    And the policy reason should contain "exceeds maximum"
