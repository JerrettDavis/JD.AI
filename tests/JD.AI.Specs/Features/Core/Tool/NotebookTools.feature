@tools
Feature: Notebook Tools
  As an AI agent
  I need to execute code snippets
  So that I can run and test code in various languages

  Scenario: Execute a PowerShell script
    When I execute powershell code "Write-Output 'Hello from pwsh'"
    Then the notebook result should contain "Hello from pwsh"

  Scenario: Execute another PowerShell snippet
    When I execute powershell code "'Result: ' + (2 + 3)"
    Then the notebook result should contain "Result: 5"

  Scenario: Unsupported language returns error
    When I execute "cobol" code "DISPLAY 'HELLO'"
    Then the notebook result should contain "Unsupported language"

  Scenario Outline: Execute code in supported languages
    When I execute "<language>" code "<code>"
    Then the notebook result should not contain "Unsupported language"

    Examples:
      | language   | code                  |
      | powershell | Write-Output test     |
