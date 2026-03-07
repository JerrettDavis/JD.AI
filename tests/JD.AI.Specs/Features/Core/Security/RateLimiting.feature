@security
Feature: Rate Limiting
  As a gateway security system
  I need rate limiting
  So that no single client can overwhelm the system

  Scenario: Allows requests within limit
    Given a rate limiter allowing 5 requests per minute
    When I make 3 requests for key "user-1"
    Then all requests should be allowed

  Scenario: Blocks requests over limit
    Given a rate limiter allowing 3 requests per minute
    When I make 5 requests for key "user-1"
    Then 3 requests should be allowed
    And 2 requests should be blocked

  Scenario: Different keys have separate limits
    Given a rate limiter allowing 2 requests per minute
    When I make 2 requests for key "user-1"
    And I make 2 requests for key "user-2"
    Then all requests should be allowed

  Scenario: Window resets allow new requests
    Given a rate limiter allowing 2 requests per 1 second window
    When I make 2 requests for key "user-1"
    And I wait for the rate limit window to expire
    And I make 1 request for key "user-1"
    Then the last request should be allowed
