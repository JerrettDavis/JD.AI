@gateway @signalr
Feature: SignalR Streaming
    As a gateway consumer
    I want to connect to the agent hub via SignalR
    So that I can receive streamed agent responses in real time

    Scenario: Client connects to agent hub successfully
        Given the gateway is running
        When I connect to the SignalR hub at "/hubs/agent"
        Then the SignalR connection should be established

    Scenario: Client can disconnect from agent hub cleanly
        Given the gateway is running
        And I am connected to the SignalR hub at "/hubs/agent"
        When I disconnect from the SignalR hub
        Then the SignalR connection should be closed

    Scenario: Streaming chat returns start and end chunks
        Given the gateway is running
        And I have spawned an agent via the API
        And I am connected to the SignalR hub at "/hubs/agent"
        When I stream a chat message "Hello" to the spawned agent
        Then I should receive a chunk with type "start"
