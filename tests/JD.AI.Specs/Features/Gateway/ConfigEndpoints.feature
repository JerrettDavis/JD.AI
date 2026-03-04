@gateway @api @config
Feature: Gateway Config Endpoints
    As a gateway administrator
    I want to view and update gateway configuration via the REST API
    So that I can manage the gateway at runtime

    Background:
        Given the gateway is running

    Scenario: Get gateway config returns OK with redacted secrets
        When I send a GET request to "/api/gateway/config"
        Then the response status should be 200
        And the response body should be a JSON object
        And the response body should have property "server"
        And the response body should have property "auth"
        And the response body should have property "rateLimit"

    Scenario: Get gateway status returns operational info
        When I send a GET request to "/api/gateway/status"
        Then the response status should be 200
        And the response body should have property "status"
        And the response body property "status" should be "running"

    Scenario: Get raw config returns full typed config
        When I send a GET request to "/api/gateway/config/raw"
        Then the response status should be 200
        And the response body should be a JSON object

    Scenario: Update server config section
        When I send a PUT request to "/api/gateway/config/server" with body:
            """
            { "port": 18790, "host": "localhost", "verbose": false }
            """
        Then the response status should be 200
        And the response body should have property "port"

    Scenario: Update rate limit config section
        When I send a PUT request to "/api/gateway/config/ratelimit" with body:
            """
            { "enabled": true, "maxRequestsPerMinute": 120 }
            """
        Then the response status should be 200
