using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Programs.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Programs.Tests;

public sealed class ProductClientServiceNativeProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRollBackEntireBatchGivenMissingOrUnboundServiceValue(bool missing)
    {
        // Arrange
        var kv = new InMemoryKvClient();
        var content = new FitzKvImmutableContentStore(kv);
        var tenant = Uuid.CreateVersion4();
        var firstRules = Rules(tenant, "First");
        var secondRules = Rules(tenant, "Second");
        var first = Assert.IsType<ClientServiceCreated>(Assert.Single(await ProductClientServiceSeeds.FactsAsync(content, firstRules)));
        var second = Assert.IsType<ClientServiceCreated>(Assert.Single(await ProductClientServiceSeeds.FactsAsync(content, secondRules)));
        var wrong = missing ? second.Content with { ContentId = Uuid.CreateVersion4() } :
            await content.WriteJsonAsync(tenant.ToString(), Uuid.CreateVersion4(), ProductClientServiceContent.Schema,
                new ProductClientServiceContent("Another service", "Purpose", "Owner"));
        var malformed = second with { Content = wrong };
        var directory = new FitzClientServiceDirectory(kv, content);
        var identity = new CheckpointIdentity("ClientServiceDirectory", EventStreamPattern.ForPattern(tenant.ToString()));
        Exception? failure;

        // Act
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(identity, ProjectionCheckpoint.Start)))
        {
            await directory.ApplyAsync(first);
            failure = await Record.ExceptionAsync(() => directory.ApplyAsync(malformed).AsTask());
        }
        var firstView = await directory.GetAsync(tenant, firstRules.Id);
        var secondView = await directory.GetAsync(tenant, secondRules.Id);
        var firstHistory = await directory.ListRevisionsAsync(tenant, firstRules.Id, 200, null);

        // Assert
        Assert.IsAssignableFrom<ImmutableContentIntegrityException>(failure);
        Assert.Equal(missing, failure is ImmutableContentUnavailableException);
        Assert.Null(firstView);
        Assert.Null(secondView);
        Assert.Null(firstHistory);
    }

    static ProductClientServiceRules Rules(Uuid tenant, string name)
    {
        var rules = new ProductClientServiceRules(tenant, Uuid.CreateVersion4());
        Assert.True(rules.Create(Uuid.CreateVersion4(), name, "Purpose", "Owner", Uuid.CreateVersion4(), "Author",
            DateTimeOffset.UtcNow).IsSuccess);
        return rules;
    }
}
