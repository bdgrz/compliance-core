using Bdgrz.Compliance.Features.Risks;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskDraftCreationIdentityTests
{
    [Fact]
    public void ShouldRetainOriginalRegistrationGivenTypedCreationRetryAfterRevision()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = RiskDraft.IdFor(tenant, program, "R-01");
        var actor = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var request = new CreateRiskDraft(id, tenant, program, "r-01", "Original title", "Scenario", "Effect");
        var risk = new RiskDraft(tenant, id);
        Assert.True(risk.Create(request, actor, "Author", now).IsSuccess);
        Assert.Null(risk.Revise(program, 1, new("Later title", "Later scenario", "Effect", null), actor, "Editor", now));
        var facts = new AggregateScenario<RiskDraft>(risk).PendingEvents.ToArray();
        var reloaded = new AggregateScenario<RiskDraft>(new(tenant, id)).Given(facts);

        // Act
        var original = reloaded.Aggregate.Create(request, actor, "Updated profile", now.AddDays(1));
        var changedActor = reloaded.Aggregate.Create(request, Uuid.CreateVersion4(), "Other author", now);
        var changedIntent = reloaded.Aggregate.Create(request with { Title = "Different creation" }, actor, "Author", now);

        // Assert
        Assert.True(original.IsSuccess, original.Error?.Message);
        Assert.Equal(new RiskRegistration(id, "R-01", 1), original.Value);
        Assert.Equal(RequestErrorKind.Conflict, changedActor.Error?.Kind);
        Assert.Equal(RequestErrorKind.Conflict, changedIntent.Error?.Kind);
        Assert.Equal(2, reloaded.Aggregate.Revision);
        Assert.Empty(reloaded.PendingEvents);
    }

    [Fact]
    public void ShouldRejectWithoutAppendGivenAuthoredIdentityDiffersFromNormalizedIdentifierIdentity()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var wrong = Uuid.CreateVersion4();
        var risk = new RiskDraft(tenant, wrong);
        var request = new CreateRiskDraft(wrong, tenant, program, "R-01", "Title", "Scenario", "Effect");

        // Act
        var result = risk.Create(request, Uuid.CreateVersion4(), "Author", DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Equal(0, risk.Revision);
        Assert.Empty(new AggregateScenario<RiskDraft>(risk).PendingEvents);
    }
}
