using System.Text.Json;
using Bdgrz.Compliance.Features.Processes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Processes;

public sealed class ProcessCatalogContractTests
{
    static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "docs", "product", "audits", "2026-10-23", "target-reactor-contracts.json");

    [Fact]
    public void ShouldMatchDesignCatalogGivenEveryRegisteredProcessDefinition()
    {
        // Arrange
        using var document = JsonDocument.Parse(File.ReadAllText(CatalogPath));
        var catalog = document.RootElement.GetProperty("processes").EnumerateArray()
            .ToDictionary(p => p.GetProperty("process").GetString()!, p => p);
        var services = new ServiceCollection();
        _ = services.AddCompliance(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Fitz:Endpoint"] = "ws://fitz:4090/ws",
                ["Fitz:ApplicationName"] = "compliance",
            }).Build());
        using var provider = services.BuildServiceProvider();

        // Act
        var registered = provider.GetRequiredService<ProcessCatalog>().Definitions;

        // Assert
        Assert.Equal(29, catalog.Count);
        foreach (var definition in registered)
        {
            var entry = Assert.Contains(definition.Name, catalog);
            Assert.Equal(entry.GetProperty("ordered").GetBoolean(), definition.Ordered);
            Assert.Equal(entry.GetProperty("rootSourceEvents").EnumerateArray().Select(e => e.GetString()),
                definition.RootSourceEvents);
            Assert.Equal(entry.GetProperty("stages").EnumerateArray().Select(s => s.GetProperty("stage").GetString()),
                definition.Stages.Select(s => s.Stage));
            Assert.Equal(entry.GetProperty("stages").EnumerateArray()
                    .SelectMany(s => s.GetProperty("receivers").EnumerateArray().Select(r => r.GetString())),
                definition.Stages.SelectMany(s => s.Receivers));
            Assert.Equal(entry.GetProperty("abort").EnumerateArray().Select(a => a.GetString()), definition.Abort);
        }
    }

    [Fact]
    public void ShouldRejectDuplicateRootGivenTwoDefinitions()
    {
        // Arrange
        var first = ProcessTestHost.Definition();
        var second = first with { Name = "Other" };

        // Act
        var failure = Record.Exception(() => new ProcessCatalog([first, second]));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }
}
