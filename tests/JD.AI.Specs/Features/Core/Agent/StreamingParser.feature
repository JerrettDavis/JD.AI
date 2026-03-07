@core @agent @streaming-parser
Feature: Streaming Content Parser
    As the agent streaming pipeline
    I want to correctly parse think tags from streaming content
    So that thinking and response content are separated

    Scenario: Plain text passes through as content
        Given a new streaming content parser
        When the parser processes chunk "Hello world"
        Then the segments should contain 1 content segment with text "Hello world"

    Scenario: Think tags extract thinking content
        Given a new streaming content parser
        When the parser processes chunk "<think>reasoning</think>response"
        Then the segments should contain an EnterThinking segment
        And the segments should contain a thinking segment with text "reasoning"
        And the segments should contain an ExitThinking segment
        And the segments should contain a content segment with text "response"

    Scenario: Nested content after think block handled correctly
        Given a new streaming content parser
        When the parser processes chunk "<think>step 1</think>answer<think>step 2</think>final"
        Then the parser should have produced content segments containing "answer"
        And the parser should have produced content segments containing "final"
        And the parser should have produced thinking segments containing "step 1"
        And the parser should have produced thinking segments containing "step 2"

    Scenario: Partial tags are buffered and flushed as content
        Given a new streaming content parser
        When the parser processes chunk "Hello <thi"
        And the parser is flushed
        Then the accumulated content should be "Hello <thi"

    Scenario: Think tag split across chunks
        Given a new streaming content parser
        When the parser processes chunk "<thi"
        And the parser processes chunk "nk>thinking content</think>visible"
        Then the parser should have produced thinking segments containing "thinking content"
        And the parser should have produced content segments containing "visible"

    Scenario: Parser reset clears state
        Given a new streaming content parser
        When the parser processes chunk "<think>partial"
        And the parser is reset
        And the parser processes chunk "clean content"
        Then the segments should contain 1 content segment with text "clean content"
        And the parser should not be in thinking state
