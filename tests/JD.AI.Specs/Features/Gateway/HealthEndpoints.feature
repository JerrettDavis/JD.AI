@gateway @health
Feature: Health Endpoints
    As a gateway operator
    I want health, ready, and liveness endpoints
    So that I can monitor the gateway and integrate with orchestrators

    Background:
        Given the gateway is running

    Scenario: Health endpoint returns healthy
        When I send a GET request to "/health"
        Then the response status should be 200

    Scenario: Ready endpoint responds with status
        When I send a GET request to "/ready"
        Then the response status should be 200
        And the response body should have property "status"
        And the response body property "status" should be "Ready"

    Scenario: Health ready endpoint responds
        When I send a GET request to "/health/ready"
        Then the response status should be 200

    Scenario: Live endpoint responds with status
        When I send a GET request to "/health/live"
        Then the response status should be 200
        And the response body should have property "status"
        And the response body property "status" should be "Live"
