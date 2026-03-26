using MeshBoard.ProductMigrationTool;

var arguments = ParseArguments(args);

if (!arguments.TryGetValue("--sqlite-path", out var sqlitePath) ||
    string.IsNullOrWhiteSpace(sqlitePath) ||
    !arguments.TryGetValue("--output-path", out var outputPath) ||
    string.IsNullOrWhiteSpace(outputPath))
{
    Console.Error.WriteLine("Usage: MeshBoard.ProductMigrationTool --sqlite-path <legacy.db> --output-path <backfill.sql>");
    return 1;
}

var generator = new ProductPreferenceBackfillScriptGenerator();
var script = await generator.GenerateFromSqliteAsync(sqlitePath);

var outputDirectory = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrWhiteSpace(outputDirectory))
{
    Directory.CreateDirectory(outputDirectory);
}

await File.WriteAllTextAsync(outputPath, script);
Console.WriteLine($"Wrote product preference backfill script to '{outputPath}'.");

return 0;

static Dictionary<string, string> ParseArguments(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);

    for (var index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length)
        {
            break;
        }

        result[args[index]] = args[index + 1];
    }

    return result;
}
