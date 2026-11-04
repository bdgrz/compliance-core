using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class ArtifactHandlingClassChangeTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    static readonly ActorReference Actor = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");

    [Fact]
    public void ShouldChangeCurrentClassWithoutChangingContentIdentityGivenReasonedDecision()
    {
        // Arrange
        var owner = Registered();
        var original = owner.Registration!;
        var request = Request(owner);

        // Act
        var failure = owner.ChangeHandlingClass(request, Actor, Now.AddHours(1));

        // Assert
        Assert.Null(failure);
        Assert.Equal("restricted", owner.Content!.HandlingClass);
        Assert.Same(original, owner.Registration);
        Assert.Equal("internal", original.Content.HandlingClass);
        Assert.Equal(original.ContentSha256, owner.ContentSha256);
        Assert.Equal(original.ContentLength, owner.ContentLength);
        Assert.Equal(2, owner.Revision);
        var change = Assert.IsType<ArtifactHandlingClassChanged>(new AggregateScenario<EvidenceArtifact>(owner).PendingEvents.Last());
        Assert.Equal("internal", change.PreviousClass);
        Assert.Equal("Personal information", change.Rationale);
        Assert.Equal(Actor, change.ChangedBy);
    }

    [Fact]
    public void ShouldKeepHistoryUnchangedGivenSameClassNoOpOrExactRetry()
    {
        // Arrange
        var owner = Registered();
        var unchanged = Request(owner) with { HandlingClass = "internal" };
        var request = Request(owner);

        // Act
        Assert.Null(owner.ChangeHandlingClass(unchanged, Actor, Now));
        Assert.Null(owner.ChangeHandlingClass(request, Actor, Now.AddHours(1)));
        var retry = owner.ChangeHandlingClass(request, Actor with { Display = "Renamed Lead" }, Now.AddDays(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, new AggregateScenario<EvidenceArtifact>(owner).PendingEvents.Count);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("class")]
    [InlineData("revision")]
    [InlineData("actor")]
    public void ShouldRefuseChangedRetryWithoutFactGivenAcceptedClassification(string changed)
    {
        // Arrange
        var owner = Registered();
        var request = Request(owner);
        Assert.Null(owner.ChangeHandlingClass(request, Actor, Now));
        var candidate = changed switch
        {
            "reason" => request with { Rationale = "Other" },
            "class" => request with { HandlingClass = "public" },
            "revision" => request with { ExpectedRevision = 2 },
            _ => request,
        };

        // Act
        var failure = owner.ChangeHandlingClass(candidate,
            changed == "actor" ? ActorReference.ForMember(Uuid.CreateVersion4(), "Other") : Actor, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, new AggregateScenario<EvidenceArtifact>(owner).PendingEvents.Count);
    }

    [Theory]
    [InlineData("class")]
    [InlineData("reason")]
    [InlineData("tenant")]
    [InlineData("artifact")]
    [InlineData("time")]
    [InlineData("actor")]
    public void ShouldRefuseWithoutMutationGivenInvalidClassification(string invalid)
    {
        // Arrange
        var owner = Registered();
        var request = Request(owner);
        request = invalid switch
        {
            "class" => request with { HandlingClass = "secret" },
            "reason" => request with { Rationale = " " },
            "tenant" => request with { TenantId = Uuid.CreateVersion4() },
            "artifact" => request with { ArtifactId = Uuid.CreateVersion4() },
            _ => request,
        };

        // Act
        var failure = owner.ChangeHandlingClass(request,
            invalid == "actor" ? ActorReference.ForSystemProcess("scanner", "Scanner") : Actor,
            invalid == "time" ? Now.AddDays(-1) : Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("internal", owner.Content!.HandlingClass);
        Assert.Equal(1, owner.Revision);
        Assert.Single(new AggregateScenario<EvidenceArtifact>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseClassificationGivenPermanentlyRejectedArtifact()
    {
        // Arrange
        var owner = Registered();
        Assert.Null(owner.RecordInspection(EvidenceInspectionOutcome.SecretDetected, Now));
        var request = Request(owner) with { ExpectedRevision = owner.Revision };

        // Act
        var failure = owner.ChangeHandlingClass(request, Actor, Now);

        // Assert
        Assert.NotNull(failure);
        Assert.Equal("internal", owner.Content!.HandlingClass);
        Assert.Equal(2, owner.Revision);
    }

    static ChangeArtifactHandlingClass Request(EvidenceArtifact owner) => new(Uuid.CreateVersion4(),
        owner.Registration!.TenantId, owner.Id, 1, "restricted", "Personal information");

    static EvidenceArtifact Registered()
    {
        var owner = new EvidenceArtifact(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        Assert.True(owner.Register(new("Evidence", null, "file", "Manual", Now,
            new(2026, 1, 1), new(2026, 12, 31), "internal"), new string('a', 64), 10, Actor, Now).IsSuccess);
        return owner;
    }
}
