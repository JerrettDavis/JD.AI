@core @agent @orchestration
Feature: Agent Orchestration
    As the orchestration system
    I want to coordinate multiple subagents using different strategies
    So that complex tasks are completed effectively

    Background:
        Given a provider registry with a "test-provider" provider
        And a mock chat service that returns "Agent output"
        And an agent session with model "gpt-4o" from provider "test-provider"
        And a mock subagent executor

    Scenario: Sequential strategy executes agents in order
        Given 3 subagent configurations named "explorer", "planner", "implementer"
        When the team is orchestrated with "sequential" strategy and goal "Build feature"
        Then the result should indicate strategy "sequential"
        And all 3 agent results should be present
        And the result should be successful

    Scenario: Fan-out strategy runs agents in parallel
        Given 3 subagent configurations named "reviewer-1", "reviewer-2", "reviewer-3"
        When the team is orchestrated with "fan-out" strategy and goal "Review code"
        Then the result should indicate strategy "fan-out"
        And a synthesizer agent result should be present
        And the result should be successful

    Scenario: Debate strategy collects perspectives and judges
        Given 2 subagent configurations named "optimist", "skeptic" with perspectives
        When the team is orchestrated with "debate" strategy and goal "Evaluate architecture"
        Then the result should indicate strategy "debate"
        And a judge agent result should be present

    Scenario: Supervisor strategy delegates and reviews
        Given 2 subagent configurations named "worker-1", "worker-2"
        When the team is orchestrated with "supervisor" strategy and goal "Implement feature"
        Then the result should indicate strategy "supervisor"
        And the result should contain a supervisor review

    Scenario: Unknown strategy returns error result
        Given 2 subagent configurations named "agent-1", "agent-2"
        When the team is orchestrated with "unknown-strategy" strategy and goal "Do something"
        Then the result should not be successful
        And the result output should contain "Unknown strategy"

    Scenario: Team context tracks events
        Given 2 subagent configurations named "agent-a", "agent-b"
        When the team is orchestrated with "sequential" strategy and goal "Test events"
        Then the team context should contain recorded events
