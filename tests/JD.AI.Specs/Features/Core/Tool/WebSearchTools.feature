@tools
Feature: Web Search Tools
  As an AI agent
  I need web search capability
  So that I can find current information online

  Scenario: Search returns no results when API key missing
    Given no Bing API key configured
    When I search the web for "test query"
    Then the search result should contain "not configured"

  Scenario: Search returns formatted results
    Given a mock Bing search returning results
    When I search the web for "dotnet 10 features"
    Then the search result should contain results with titles and URLs

  Scenario: Search handles HTTP errors gracefully
    Given a mock Bing search that fails
    When I search the web for "test"
    Then the search result should contain "Search failed"

  Scenario: Search respects count parameter
    Given a mock Bing search returning results
    When I search the web for "test" with count 3
    Then the search should request at most 3 results
