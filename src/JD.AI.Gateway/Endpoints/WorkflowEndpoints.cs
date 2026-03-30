using JD.AI.Gateway.Models;
using JD.AI.Workflows;
using JD.AI.Workflows.Steps;

namespace JD.AI.Gateway.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/workflows").WithTags("Workflows");

        group.MapGet("/", async (IWorkflowCatalog catalog, CancellationToken ct) =>
        {
            var workflows = await catalog.ListAsync(ct);
            return Results.Ok(workflows);
        })
        .WithName("ListWorkflows")
        .WithDescription("List all workflow definitions from the catalog.");

        group.MapGet("/{name}", async (string name, IWorkflowCatalog catalog, CancellationToken ct) =>
        {
            var workflow = await catalog.GetAsync(name, ct: ct);
            return workflow is null
                ? Results.NotFound(new { Error = $"Workflow '{name}' not found." })
                : Results.Ok(workflow);
        })
        .WithName("GetWorkflow")
        .WithDescription("Get a specific workflow definition by name (latest version).");

        group.MapPost("/", async (AgentWorkflowDefinition definition, IWorkflowCatalog catalog, CancellationToken ct) =>
        {
            await catalog.SaveAsync(definition, ct);
            return Results.Created($"/api/v1/workflows/{definition.Name}", definition);
        })
        .WithName("CreateWorkflow")
        .WithDescription("Create or update a workflow definition in the catalog.");

        group.MapDelete("/{name}", async (string name, IWorkflowCatalog catalog, CancellationToken ct) =>
        {
            var deleted = await catalog.DeleteAsync(name, ct: ct);
            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { Error = $"Workflow '{name}' not found." });
        })
        .WithName("DeleteWorkflow")
        .WithDescription("Delete a workflow definition by name.");

        group.MapPost("/run", async (WorkflowRunRequest request, IWorkflowCatalog catalog, IWorkflowBridge bridge, CancellationToken ct) =>
        {
            var definition = await catalog.GetAsync(request.WorkflowName, request.Version, ct);
            if (definition is null)
                return Results.NotFound(new { Error = $"Workflow '{request.WorkflowName}' not found." });

            var data = new AgentWorkflowData { Prompt = request.Prompt };

            try
            {
                var result = await bridge.ExecuteAsync(definition, data, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Kernel not set"))
            {
                return Results.UnprocessableEntity(new
                {
                    Error = "Workflow requires an LLM kernel which is not available in the Gateway context.",
                    WorkflowName = definition.Name,
                    Version = definition.Version,
                    StepCount = definition.Steps.Count,
                });
            }
        })
        .WithName("RunWorkflow")
        .WithDescription("Execute a workflow by name. Returns an error if the workflow requires LLM steps without an active agent session.");

        group.MapPost("/classify", (WorkflowClassifyRequest request, IPromptIntentClassifier classifier) =>
        {
            var classification = classifier.Classify(request.Prompt);
            return Results.Ok(classification);
        })
        .WithName("ClassifyPrompt")
        .WithDescription("Classify a prompt as workflow or conversation using TF-IDF intent classification.");
    }
}
