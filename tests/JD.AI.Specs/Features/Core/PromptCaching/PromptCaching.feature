@core @prompt-caching
Feature: Prompt Caching
    As the prompt caching system
    I want to apply cache headers for supported providers
    So that repeated prompts are served faster and cheaper

    Scenario: Applies cache headers for Anthropic provider
        Given execution settings for model "claude-3-opus" on provider "anthropic"
        And the chat history has sufficient tokens for caching
        When the prompt cache policy is applied with caching enabled
        Then the execution settings should contain the cache enabled extension key
        And the execution settings should contain the TTL extension key with value "5m"

    Scenario: Skips caching for unsupported provider
        Given execution settings for model "gpt-4o" on provider "openai"
        And the chat history has sufficient tokens for caching
        When the prompt cache policy is applied with caching enabled
        Then the execution settings should not contain the cache enabled extension key

    Scenario: Skips caching when disabled
        Given execution settings for model "claude-3-opus" on provider "anthropic"
        And the chat history has sufficient tokens for caching
        When the prompt cache policy is applied with caching disabled
        Then the execution settings should not contain the cache enabled extension key

    Scenario: Respects one-hour TTL setting
        Given execution settings for model "claude-3-opus" on provider "anthropic"
        And the chat history has sufficient tokens for caching
        When the prompt cache policy is applied with caching enabled and TTL "OneHour"
        Then the execution settings should contain the TTL extension key with value "1h"

    Scenario: Skips caching when token count is below minimum
        Given execution settings for model "claude-3-opus" on provider "anthropic"
        And the chat history has insufficient tokens for caching
        When the prompt cache policy is applied with caching enabled
        Then the execution settings should not contain the cache enabled extension key

    Scenario Outline: Provider name detection for cache support
        When checking if provider "<provider>" with model "<model>" supports caching
        Then the result should be <supported>

        Examples:
            | provider   | model          | supported |
            | anthropic  | claude-3-opus  | true      |
            | claude     | claude-3-haiku | true      |
            | openai     | gpt-4o         | false     |
            | google     | gemini-pro     | false     |
            | test       | claude-custom  | true      |

    Scenario Outline: Minimum token thresholds vary by model
        When checking the minimum tokens for model "<model>"
        Then the minimum should be <tokens>

        Examples:
            | model            | tokens |
            | claude-3-opus    | 1024   |
            | claude-3-sonnet  | 1024   |
            | claude-3-haiku   | 2048   |
