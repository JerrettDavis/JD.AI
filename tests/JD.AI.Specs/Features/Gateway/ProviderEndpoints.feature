@gateway @api @providers
Feature: Provider Endpoints
    As a gateway consumer
    I want to detect and list AI providers via the REST API
    So that I can discover available models

    Background:
        Given the gateway is running

    Scenario: List providers returns OK
        When I send a GET request to "/api/providers"
        Then the response status should be 200
        And the response body should be a JSON array

    Scenario: Detect providers includes provider metadata
        When I send a GET request to "/api/providers"
        Then the response status should be 200
        And each provider should have a name property

    Scenario: Get models for non-existent provider returns 404
        When I send a GET request to "/api/providers/nonexistent-provider/models"
        Then the response status should be 404
