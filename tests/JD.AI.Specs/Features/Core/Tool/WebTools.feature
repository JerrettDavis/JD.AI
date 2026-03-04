@tools
Feature: Web Tools
  As an AI agent
  I need to fetch web content
  So that I can retrieve information from URLs

  Scenario: Fetch a URL successfully
    Given a mock HTTP server returning HTML "<html><body><p>Hello Web</p></body></html>"
    When I fetch the mock URL
    Then the web result should contain "Hello Web"

  Scenario: Fetch strips script and style tags
    Given a mock HTTP server returning HTML "<html><head><script>alert(1)</script><style>.x{}</style></head><body><p>Content</p></body></html>"
    When I fetch the mock URL
    Then the web result should contain "Content"
    And the web result should not contain "alert"
    And the web result should not contain ".x{}"

  Scenario: Fetch truncates long content
    Given a mock HTTP server returning a very long response
    When I fetch the mock URL with max length 100
    Then the web result should contain "truncated"

  Scenario: Fetch handles HTTP errors
    Given a mock HTTP server returning error 404
    When I fetch the mock URL
    Then the web result should contain "Error fetching"
