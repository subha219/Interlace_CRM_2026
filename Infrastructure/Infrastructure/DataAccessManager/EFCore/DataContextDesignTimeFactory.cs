using System.Text.Json;
using Infrastructure.DataAccessManager.EFCore.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.DataAccessManager.EFCore;

/// <summary>
/// Used only by "dotnet ef". Connection string order: CRM_CONNECTION environment variable,
/// then appsettings.Development.json / appsettings.json of the InterlaceUI project.
/// </summary>
public class DataContextDesignTimeFactory : IDesignTimeDbContextFactory<DataContext>
{
    public DataContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CRM_CONNECTION") ?? FindConnectionString();

        var options = new DbContextOptionsBuilder<DataContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new DataContext(options);
    }

    private static string FindConnectionString()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(dir.FullName, "Presentation", "InterlaceUI"),
                Path.Combine(dir.FullName, "InterlaceUI"),
                dir.FullName
            })
            {
                foreach (var file in new[] { "appsettings.Development.json", "appsettings.json" })
                {
                    var path = Path.Combine(candidate, file);
                    if (!File.Exists(path)) continue;

                    using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
                    if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                        && cs.TryGetProperty("DefaultConnection", out var value)
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!;
                    }
                }
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No connection string found. Set the CRM_CONNECTION environment variable.");
    }
}
