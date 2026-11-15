using System.Text.Json;
using System.Text.Json.Serialization;
using CutCraft.Core.Models;

namespace CutCraft.Core.Serialization;

public static class ProjectSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string Serialize(Project project)
    {
        project.ModifiedAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(project, JsonOptions);
    }

    public static Project Deserialize(string json)
    {
        var project = JsonSerializer.Deserialize<Project>(json, JsonOptions);
        if (project == null)
        {
            throw new InvalidOperationException("Failed to parse project JSON; root is null.");
        }
        return project;
    }

    public static void SaveToFile(Project project, string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = Serialize(project);
        File.WriteAllText(filePath, json);
    }

    public static Project LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Project file not found: {filePath}");
        }

        var json = File.ReadAllText(filePath);
        return Deserialize(json);
    }
}
