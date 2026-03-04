@gateway @api @sessions
Feature: Session Endpoints
    As a gateway consumer
    I want to manage chat sessions via the REST API
    So that I can list, retrieve, and close sessions

    Background:
        Given the gateway is running

    Scenario: List sessions returns OK
        When I send a GET request to "/api/sessions"
        Then the response status should be 200
        And the response body should be a JSON array

    Scenario: List sessions with limit parameter
        When I send a GET request to "/api/sessions?limit=10"
        Then the response status should be 200
        And the response body should be a JSON array

    Scenario: Get session by non-existent ID returns 404
        When I send a GET request to "/api/sessions/nonexistent-session-id"
        Then the response status should be 404

    Scenario: Close non-existent session returns 204
        When I send a POST request to "/api/sessions/nonexistent-session-id/close"
        Then the response status should be 204

    Scenario: Export non-existent session returns 404
        When I send a POST request to "/api/sessions/nonexistent-session-id/export"
        Then the response status should be 404
