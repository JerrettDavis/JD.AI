@ignore
@ui
Feature: Skills Page
    As a gateway operator
    I want to manage skills from the dashboard
    So that I can configure, enable, and disable integrations

    Background:
        Given I am on the skills page

    # ── Rendering ──────────────────────────────────────────
    @smoke
    Scenario: Displays skills page heading
        Then I should see the heading "Skills"

    @smoke
    Scenario: Status filter tabs are visible
        Then I should see the "All" filter tab
        And I should see the "Ready" filter tab
        And I should see the "Needs Setup" filter tab
        And I should see the "Disabled" filter tab

    @smoke
    Scenario: Search box is visible
        Then I should see the skill search box

    # ── Loading state ────────────────────────────────────
    Scenario: Skeleton cards shown while loading
        Given the skills list is loading
        Then I should see skeleton loading cards

    # ── Filtering ─────────────────────────────────────────
    @planned
    Scenario: All filter shows all skills
        Given there are skills with mixed statuses
        When I click the "All" filter tab
        Then I should see all skills listed

    @planned
    Scenario: Ready filter shows only ready skills
        Given there are skills with mixed statuses
        When I click the "Ready" filter tab
        Then I should only see skills with "Ready" status

    @planned
    Scenario: Needs Setup filter shows only needs-setup skills
        Given there are skills with mixed statuses
        When I click the "Needs Setup" filter tab
        Then I should only see skills with "Needs Setup" status

    @planned
    Scenario: Disabled filter shows only disabled skills
        Given there are skills with mixed statuses
        When I click the "Disabled" filter tab
        Then I should only see skills with "Disabled" status

    # ── Search ────────────────────────────────────────────
    @planned
    Scenario: Search narrows skill list
        Given there are multiple skills
        When I type "github" in the search box
        Then I should only see skills matching "github"

    @planned
    Scenario: Clearing search restores full list
        Given there are multiple skills
        When I type "github" in the search box
        And I clear the search box
        Then I should see all skills listed

    # ── Empty state ───────────────────────────────────────
    @planned
    Scenario: Empty state shown when no skills match filter
        Given there are skills with mixed statuses
        When I click the "Disabled" filter tab
        And there are no disabled skills
        Then I should see the no-skills empty state

    # ── Skill cards ───────────────────────────────────────
    @planned
    Scenario: Skill card shows name, description, and status
        Given there are skills
        Then each skill card should show the skill name
        And each skill card should show the skill description
        And each skill card should show the skill status badge

    @planned
    Scenario: Skill card has enable/disable toggle
        Given there are skills
        Then each skill card should have an enable toggle

    @planned
    Scenario: Skill card has configure button
        Given there are skills
        Then each skill card should have a "Configure" button

    # ── Toggle ────────────────────────────────────────────
    @planned
    Scenario: Toggling skill enabled shows success snackbar
        Given there are skills
        When I toggle the first skill to enabled
        Then a success snackbar should appear with "enabled"

    @planned
    Scenario: Toggling skill disabled shows success snackbar
        Given there are enabled skills
        When I toggle the first skill to disabled
        Then a success snackbar should appear with "disabled"

    # ── Configure dialog ──────────────────────────────────
    @planned
    Scenario: Configure button opens config dialog
        Given there are skills with configuration
        When I click the "Configure" button on the first skill
        Then the skill configure dialog should be visible

    @planned
    Scenario: Configure dialog shows config fields
        Given there are skills with configuration
        When I click the "Configure" button on the first skill
        Then the configure dialog should show all config fields

    @planned
    Scenario: Configure dialog save button saves config
        Given there are skills with configuration
        When I click the "Configure" button on the first skill
        And I update a config field value
        And I click the "Save" button in the configure dialog
        Then a success snackbar should appear with "configuration saved"

    # ── Page title ────────────────────────────────────────
    Scenario: Page title is correct
        Then the browser page title should be "Skills — JD.AI"
