using System.Text.Json;
using CutFlow.Models;

namespace CutFlow.Utilities;

internal static class ProjectDocumentCloner
{
    public static ProjectDocument Clone(ProjectDocument project, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return JsonSerializer.Deserialize<ProjectDocument>(JsonSerializer.Serialize(project, options), options)
            ?? throw new InvalidOperationException("The project copy could not be created.");
    }
}
