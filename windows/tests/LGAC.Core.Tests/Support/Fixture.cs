using System.Text.Json.Nodes;

namespace LGAC.Core.Tests.Support;

internal static class Fixture
{
    public static JsonNode Json(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "shared")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new DirectoryNotFoundException("Pasta shared não encontrada");
        }

        var path = Path.Combine(directory.FullName, "shared", "fixtures", name + ".json");
        return JsonNode.Parse(File.ReadAllText(path))!;
    }
}
