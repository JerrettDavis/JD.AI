@gateway @ratelimit
Feature: Rate Limit Middleware
    As a gateway administrator
    I want rate limiting on API endpoints
    So that the gateway is protected from abuse

    Scenario: Requests within the rate limit succeed
        Given the gateway is running with rate limiting enabled at 10 requests per minute
        When I send 5 GET requests to "/api/agents"
        Then all responses should have status 200

    Scenario: Requests exceeding the rate limit return 429
        Given the gateway is running with rate limiting enabled at 3 requests per minute
        When I send 5 GET requests to "/api/agents"
        Then at least one response should have status 429

    Scenario: Health endpoints are exempt from rate limiting
        Given the gateway is running with rate limiting enabled at 2 requests per minute
        When I send 5 GET requests to "/health"
        Then all responses should have status 200
