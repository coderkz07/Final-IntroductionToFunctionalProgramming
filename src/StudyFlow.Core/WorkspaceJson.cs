using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyFlow.Core;

public static class WorkspaceJson
{
    public const int MaxBytes = 1_048_576;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 12,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Write(Workspace workspace) => JsonSerializer.Serialize(workspace, Options);

    public static Workspace Read(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new PlannerRuleException("The backup is larger than 1 MB.");

        try
        {
            var workspace = JsonSerializer.Deserialize<Workspace>(json, Options)
                ?? throw new PlannerRuleException("The backup is empty.");
            WorkspaceRules.Validate(workspace);
            return workspace;
        }
        catch (JsonException)
        {
            throw new PlannerRuleException("Choose a valid StudyFlow JSON backup.");
        }
    }
}
