using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance;
using Bdgrz.Compliance.Features.Incidents.Commands;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.UserIdentities.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Processes.Tests;

public sealed class ProcessCatalogContractTests
{
    [Fact]
    public void ShouldMatchRuntimeContractGivenEveryRegisteredProcessDefinition()
    {
        // Arrange
        using var document = RuntimeProcessContracts.Read();
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
        var handlers = services.Where(static descriptor => descriptor.ServiceType == typeof(RequestHandlerRegistration))
            .Select(static descriptor => ((RequestHandlerRegistration)descriptor.ImplementationInstance!).RequestType.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.Equal(catalog.Keys.Order(StringComparer.Ordinal),
            registered.Select(definition => definition.Name).Order(StringComparer.Ordinal));
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
            Assert.All(definition.Stages.SelectMany(static stage => stage.Receivers).Concat(definition.Abort),
                receiver => Assert.Contains(receiver, handlers));
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
