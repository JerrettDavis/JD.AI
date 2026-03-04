@gateway @orchestrator
Feature: Gateway Orchestrator
    As a gateway operator
    I want the gateway to start successfully and initialize subsystems
    So that all configured channels, agents, and routes are operational

    Background:
        Given the gateway is running

    Scenario: Gateway starts and health check is healthy
        When I send a GET request to "/health"
        Then the response status should be 200

    Scenario: Gateway status endpoint reports running
        When I send a GET request to "/api/gateway/status"
        Then the response status should be 200
        And the response body should have property "status"
        And the response body property "status" should be "running"

    Scenario: Gateway exposes channel information in status
        When I send a GET request to "/api/gateway/status"
        Then the response status should be 200
        And the response body should have property "channels"

    Scenario: Gateway exposes agent information in status
        When I send a GET request to "/api/gateway/status"
        Then the response status should be 200
        And the response body should have property "agents"

    Scenario: Gateway exposes route information in status
        When I send a GET request to "/api/gateway/status"
        Then the response status should be 200
        And the response body should have property "routes"
