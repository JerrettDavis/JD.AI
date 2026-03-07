@gateway @auth
Feature: API Key Auth Middleware
    As a gateway administrator
    I want API key authentication on protected endpoints
    So that only authorized clients can access the gateway API

    Scenario: Request to API endpoint without key when auth enabled returns 401
        Given the gateway is running with auth enabled and key "test-key-123"
        When I send an unauthenticated GET request to "/api/agents"
        Then the response status should be 401

    Scenario: Valid API key passes authentication
        Given the gateway is running with auth enabled and key "test-key-123"
        When I send an authenticated GET request to "/api/agents" with API key "test-key-123"
        Then the response status should be 200

    Scenario: Invalid API key returns 401
        Given the gateway is running with auth enabled and key "test-key-123"
        When I send an authenticated GET request to "/api/agents" with API key "wrong-key"
        Then the response status should be 401

    Scenario: Health endpoints bypass authentication
        Given the gateway is running with auth enabled and key "test-key-123"
        When I send an unauthenticated GET request to "/health"
        Then the response status should be 200

    Scenario: Ready endpoint bypasses authentication
        Given the gateway is running with auth enabled and key "test-key-123"
        When I send an unauthenticated GET request to "/ready"
        Then the response status should be 200
