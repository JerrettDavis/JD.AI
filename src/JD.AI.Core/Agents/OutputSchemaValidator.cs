using System.Text.Json;
using System.Text.Json.Nodes;

namespace JD.AI.Core.Agents;

/// <summary>
/// Validates agent output against a user-provided JSON schema.
/// Used with the <c>--json-schema</c> CLI flag.
/// </summary>
public static class OutputSchemaValidator
{
    /// <summary>Exit code when the output fails schema validation.</summary>
    public const int SchemaValidationExitCode = 3;

    /// <summary>
    /// Validates that <paramref name="output"/> is valid JSON conforming to basic
    /// structural constraints from <paramref name="schema"/>.
    /// Returns a list of validation errors (empty = valid).
    /// </summary>
    public static IReadOnlyList<string> Validate(string output, string schema)
    {
        var errors = new List<string>();

        JsonNode? outputNode;
        try
        {
            outputNode = JsonNode.Parse(output);
        }
        catch (JsonException ex)
        {
            errors.Add($"Output is not valid JSON: {ex.Message}");
            return errors;
        }

        JsonNode? schemaNode;
        try
        {
            schemaNode = JsonNode.Parse(schema);
        }
        catch (JsonException ex)
        {
            errors.Add($"Schema is not valid JSON: {ex.Message}");
            return errors;
        }

        if (schemaNode is not JsonObject schemaObj)
        {
            errors.Add("Schema must be a JSON object.");
            return errors;
        }

        ValidateNode(outputNode, schemaObj, "$", errors);
        return errors;
    }

    /// <summary>
    /// Loads a schema from a file path or inline JSON string.
    /// </summary>
    public static string LoadSchema(string schemaPathOrJson)
    {
        // If it starts with '{' or '[', treat as inline JSON
        if (schemaPathOrJson.TrimStart().StartsWith('{') ||
            schemaPathOrJson.TrimStart().StartsWith('['))
        {
            return schemaPathOrJson;
        }

        // Otherwise treat as file path
        if (!File.Exists(schemaPathOrJson))
            throw new FileNotFoundException($"JSON schema file not found: {schemaPathOrJson}");

        return File.ReadAllText(schemaPathOrJson);
    }

    /// <summary>
    /// Generates a feedback prompt to help the agent fix its output.
    /// </summary>
    public static string GenerateRetryPrompt(IReadOnlyList<string> errors, string schema)
    {
        return $"""
            Your previous output did not conform to the required JSON schema.

            Errors:
            {string.Join(Environment.NewLine, errors.Select(e => $"  - {e}"))}

            Required schema:
            {schema}

            Please produce output that is ONLY valid JSON matching this schema, with no additional text or markdown fencing.
            """;
    }

    private static void ValidateNode(JsonNode? node, JsonObject schema, string path, List<string> errors)
    {
        var typeStr = schema["type"]?.GetValue<string>();

        if (node is null)
        {
            errors.Add($"{path}: expected {typeStr ?? "value"} but got null");
            return;
        }

        switch (typeStr)
        {
            case "object":
                ValidateObject(node, schema, path, errors);
                break;
            case "array":
                ValidateArray(node, schema, path, errors);
                break;
            case "string":
                if (node is not JsonValue || node.GetValueKind() != JsonValueKind.String)
                    errors.Add($"{path}: expected string");
                break;
            case "number" or "integer":
                if (node is not JsonValue || node.GetValueKind() != JsonValueKind.Number)
                    errors.Add($"{path}: expected {typeStr}");
                break;
            case "boolean":
                if (node is not JsonValue || (node.GetValueKind() != JsonValueKind.True && node.GetValueKind() != JsonValueKind.False))
                    errors.Add($"{path}: expected boolean");
                break;
        }
    }

    private static void ValidateObject(JsonNode node, JsonObject schema, string path, List<string> errors)
    {
        if (node is not JsonObject obj)
        {
            errors.Add($"{path}: expected object but got {node.GetValueKind()}");
            return;
        }

        // Check required properties
        if (schema["required"] is JsonArray requiredArr)
        {
            foreach (var req in requiredArr)
            {
                var propName = req?.GetValue<string>();
                if (propName is not null && !obj.ContainsKey(propName))
                    errors.Add($"{path}: missing required property '{propName}'");
            }
        }

        // Validate property types
        if (schema["properties"] is JsonObject props)
        {
            foreach (var (propName, propSchema) in props)
            {
                if (propSchema is JsonObject propSchemaObj && obj.ContainsKey(propName))
                {
                    ValidateNode(obj[propName], propSchemaObj, $"{path}.{propName}", errors);
                }
            }
        }
    }

    private static void ValidateArray(JsonNode node, JsonObject schema, string path, List<string> errors)
    {
        if (node is not JsonArray arr)
        {
            errors.Add($"{path}: expected array but got {node.GetValueKind()}");
            return;
        }

        // Validate items against item schema
        if (schema["items"] is JsonObject itemSchema)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                ValidateNode(arr[i], itemSchema, $"{path}[{i}]", errors);
            }
        }
    }
}
