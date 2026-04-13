@ui
Feature: Navigation Menu — Grouped Sidebar
    As a gateway operator
    I want a grouped sidebar with collapsible sections
    So that I can quickly navigate to relevant pages

    Background:
        Given I am on the home page

    @smoke
    Scenario: Chat link is always visible at top level
        Then I should see a nav link labeled "Chat" with href "/chat"

    @smoke
    Scenario: Control group is visible and expanded by default
        Then I should see the nav group "Control"
        And the "Control" group should be expanded

    @smoke
    Scenario: Agents group contains Agents and Skills links
        Then I should see the nav group "Agents"
        And I should see a nav link labeled "Agents"
        And I should see a nav link labeled "Skills"

    @smoke
    Scenario: Settings group contains four sub-links
        Then I should see the nav group "Settings"
        And I should see a nav link labeled "AI & Agents"
        And I should see a nav link labeled "Communication"
        And I should see a nav link labeled "Config"
        And I should see a nav link labeled "Logs"

    Scenario: Collapsing a group hides its links
        When I click the "Agents" nav group toggle
        Then the "Agents" group should be collapsed
        And I should not see a nav link labeled "Skills"

    Scenario: Expand/collapse state persists on reload
        When I click the "Settings" nav group toggle
        And I reload the page
        Then the "Settings" group should be collapsed
