@gateway @api @agents
Feature: Agent Endpoints
    As a gateway consumer
    I want to manage AI agent instances via the REST API
    So that I can spawn, query, message, and stop agents

    Background:
        Given the gateway is running

    Scenario: List agents returns OK with empty array initially
        When I send a GET request to "/api/agents"
        Then the response status should be 200
        And the response body should be a JSON array

    Scenario: Spawn agent returns 201 Created
        When I spawn an agent with provider "ollama" and model "llama3"
        Then the response status should be 201
        And the response body should have property "id"

    Scenario: Send message to a non-existent agent returns 404
        When I send a message "Hello" to agent "nonexistent-id"
        Then the response status should be 404

    Scenario: Delete agent returns 204 NoContent
        When I send a DELETE request to "/api/agents/some-agent-id"
        Then the response status should be 204

    Scenario: List agents after spawn shows the agent
        Given I have spawned an agent with provider "ollama" and model "llama3"
        When I send a GET request to "/api/agents"
        Then the response status should be 200
        And the response body should be a JSON array
        And the agents list should not be empty
