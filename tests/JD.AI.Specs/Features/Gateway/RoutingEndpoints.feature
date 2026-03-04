@gateway @api @routing
Feature: Routing Endpoints
    As a gateway consumer
    I want to manage channel-to-agent routing via the REST API
    So that I can control message flow between channels and agents

    Background:
        Given the gateway is running

    Scenario: List routing mappings returns OK
        When I send a GET request to "/api/routing/mappings"
        Then the response status should be 200
        And the response body should be a JSON object

    Scenario: Map a channel to an agent
        When I map channel "test-channel" to agent "test-agent"
        Then the response status should be 200
        And the response body should have property "status"
        And the response body property "status" should be "mapped"

    Scenario: Mapping persists and is visible in mappings list
        Given I have mapped channel "my-channel" to agent "my-agent"
        When I send a GET request to "/api/routing/mappings"
        Then the response status should be 200
        And the routing mappings should contain channel "my-channel"
