@core @provider @model-search
Feature: Model Search
    As a user of the AI agent
    I want to search for models across all providers
    So that I can find the right model for my task

    Background:
        Given a provider registry with detectors for "openai" and "anthropic"
        And the "openai" detector reports as available with models "gpt-4o", "gpt-4o-mini"
        And the "anthropic" detector reports as available with models "claude-3-opus", "claude-3-sonnet"

    Scenario: Searches across all providers
        When the model catalog is retrieved
        Then the catalog should contain 4 models

    Scenario: Filters models by provider
        When models are filtered by provider "anthropic"
        Then the filtered model list should contain 2 models
        And all filtered models should be from provider "anthropic"

    Scenario: Returns model metadata including context window
        When the model catalog is retrieved
        Then each model should have a non-empty ID
        And each model should have a non-empty display name
        And each model should have a non-empty provider name
        And each model should have a positive context window size

    Scenario: Model search with no results
        When models are filtered by provider "nonexistent"
        Then the filtered model list should be empty

    Scenario: Model capabilities are exposed
        When the model catalog is retrieved
        Then each model should have capabilities flags
