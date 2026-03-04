@core @agent @streaming
Feature: Agent Streaming
    As a user of the AI agent
    I want to receive streaming responses with thinking and content
    So that I see tokens as they arrive and can observe reasoning

    Background:
        Given a provider registry with a "test-provider" provider

    Scenario: Streaming response with content
        Given a mock streaming chat service that yields chunks "Hello" ", " "world!"
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "Hi" in streaming mode
        Then the response should be "Hello, world!"
        And streaming output should have started and ended

    Scenario: Streaming with thinking content via metadata
        Given a mock streaming chat service with reasoning metadata "Let me think..." and content "The answer is 42"
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "What is the meaning?" in streaming mode
        Then the response should be "The answer is 42"
        And thinking output should have started
        And the captured thinking content should contain "Let me think..."

    Scenario: Streaming with think tags in content
        Given a mock streaming chat service that yields chunks "<think>reasoning here</think>actual response"
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "Explain" in streaming mode
        Then the response should be "actual response"
        And the captured thinking content should contain "reasoning here"

    Scenario: Empty streaming response renders fallback
        Given a mock streaming chat service that yields no content
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "Hello" in streaming mode
        Then the response should be "(no response)"

    Scenario: Streaming with mixed thinking and content
        Given a mock streaming chat service that yields chunks "<think>step 1</think>result part 1<think>step 2</think> result part 2"
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "Complex query" in streaming mode
        Then the response should contain "result part 1"
        And the response should contain "result part 2"
        And the captured thinking content should contain "step 1"
        And the captured thinking content should contain "step 2"

    Scenario: Streaming turn records metrics
        Given a mock streaming chat service that yields chunks "Hello" " world"
        And an agent session with model "gpt-4o" from provider "test-provider"
        When the user sends "Hi" in streaming mode
        Then the turn metrics should have been recorded
