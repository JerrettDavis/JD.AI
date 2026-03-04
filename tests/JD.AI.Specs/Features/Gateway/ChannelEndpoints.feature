@gateway @api @channels
Feature: Channel Endpoints
    As a gateway consumer
    I want to manage messaging channels via the REST API
    So that I can list, connect, and disconnect channel adapters

    Background:
        Given the gateway is running

    Scenario: List channels returns OK
        When I send a GET request to "/api/channels"
        Then the response status should be 200
        And the response body should be a JSON array

    Scenario: Connect non-existent channel returns 404
        When I send a POST request to "/api/channels/nonexistent/connect"
        Then the response status should be 404

    Scenario: Disconnect non-existent channel returns 404
        When I send a POST request to "/api/channels/nonexistent/disconnect"
        Then the response status should be 404

    Scenario: Send message to non-existent channel returns 404
        When I send a channel message to "nonexistent" with conversation "conv1" and content "Hello"
        Then the response status should be 404
