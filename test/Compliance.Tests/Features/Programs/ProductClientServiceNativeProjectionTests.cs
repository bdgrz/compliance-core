using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Programs;

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
