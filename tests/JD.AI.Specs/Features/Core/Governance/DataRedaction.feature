@governance
Feature: Data Redaction
  As a governance system
  I need to redact sensitive data
  So that PII and secrets are not sent to AI providers

  Scenario: Redact email addresses
    Given a data redactor with pattern "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}"
    When I redact "Contact john@example.com for info"
    Then the redacted text should be "Contact [REDACTED] for info"

  Scenario: Redact phone numbers
    Given a data redactor with pattern "\b\d{3}[-.]?\d{3}[-.]?\d{4}\b"
    When I redact "Call 555-123-4567 now"
    Then the redacted text should be "Call [REDACTED] now"

  Scenario: Preserve non-sensitive data
    Given a data redactor with pattern "\b\d{3}-\d{2}-\d{4}\b"
    When I redact "The project is version 2.0"
    Then the redacted text should be "The project is version 2.0"

  Scenario: Multiple patterns applied
    Given a data redactor with patterns:
      | pattern                                                        |
      | [a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}               |
      | \b\d{3}[-.]?\d{3}[-.]?\d{4}\b                                 |
    When I redact "Email bob@test.com or call 555-000-1234"
    Then the redacted text should be "Email [REDACTED] or call [REDACTED]"

  Scenario: No patterns acts as pass-through
    Given a data redactor with no patterns
    When I redact "Sensitive: 123-45-6789"
    Then the redacted text should be "Sensitive: 123-45-6789"

  Scenario: Detect sensitive content
    Given a data redactor with pattern "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}"
    When I check "Contact admin@corp.com" for sensitive content
    Then sensitive content should be detected

  Scenario: No sensitive content detected in clean text
    Given a data redactor with pattern "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}"
    When I check "No emails here" for sensitive content
    Then sensitive content should not be detected
