@memory
Feature: Vector Store
  As a memory subsystem
  I need vector storage and similarity search
  So that I can store and retrieve embeddings efficiently

  Background:
    Given a SQLite vector store with a temporary database

  Scenario: Store an embedding entry
    When I upsert a memory entry with id "e1" and content "test content"
    Then the vector store count should be 1

  Scenario: Search returns similar entries
    Given I have stored entry "e1" with embedding [1.0, 0.0, 0.0]
    And I have stored entry "e2" with embedding [0.9, 0.1, 0.0]
    And I have stored entry "e3" with embedding [0.0, 0.0, 1.0]
    When I search with embedding [1.0, 0.0, 0.0] for top 2
    Then the search results should contain "e1"
    And the search results should contain "e2"
    And the search results should not contain "e3"

  Scenario: Delete an entry by ID
    Given I have stored entry "e1" with embedding [1.0, 0.0, 0.0]
    When I delete entry "e1"
    Then the vector store count should be 0

  Scenario: Search with category filter
    Given I have stored entry "e1" in category "code" with embedding [1.0, 0.0, 0.0]
    And I have stored entry "e2" in category "docs" with embedding [0.9, 0.1, 0.0]
    When I search with embedding [1.0, 0.0, 0.0] in category "code"
    Then the search results should contain "e1"
    And the search results should not contain "e2"

  Scenario: Upsert replaces existing entry
    Given I have stored entry "e1" with content "original"
    When I upsert entry "e1" with content "updated"
    Then the vector store count should be 1
