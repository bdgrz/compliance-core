using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Readers;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Workforce.Tests;

public sealed class ServiceIdentityNamedProjectionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Identity = Uuid.CreateVersion4();
    static readonly Uuid Owner = Uuid.CreateVersion4();
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");
    static readonly DateOnly Today = new(2026, 11, 4);
    static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("review")]
    [InlineData("disable")]
    [InlineData("enable")]
    [InlineData("retire")]
    public async Task ShouldProjectExactNamedChangeGivenRecordedIdentity(string operation)
    {
        // Arrange
        var directory = new FitzServiceIdentityDirectory(new InMemoryKvClient());
        var initial = operation == "enable" ? "disabled" : "active";
        await ApplyAsync(directory, new ServiceIdentityRecorded(Tenant, Identity,
            new ServiceIdentityTerms("Bot", "bot", "Approved purpose", "production", initial,
                "person", Owner, Today.AddMonths(6)), Author, Now));
        var newOwner = Uuid.CreateVersion4();
        DomainEvent change = operation switch
        {
            "owner" => new ServiceIdentityOwnerChanged(Tenant, Identity, 2, "person", Owner, "team", newOwner, Author, Now),
            "purpose" => new ServiceIdentityPurposeChanged(Tenant, Identity, 2, "New approved purpose", Author, Now),
            "review" => new ServiceIdentityReviewed(Tenant, Identity, 2, Author, "Annual review", Today.AddYears(1), Now),
            "disable" => new ServiceIdentityDisabled(Tenant, Identity, 2, "Paused", Author, Now),
            "enable" => new ServiceIdentityEnabled(Tenant, Identity, 2, Author, Now),
            "retire" => new ServiceIdentityRetired(Tenant, Identity, 2, "Replaced", Author, Now),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        // Act
        await ApplyAsync(directory, change);
        var view = await directory.GetAsync(Tenant, Identity);

        // Assert
        Assert.NotNull(view);
        Assert.Equal(Tenant, view.TenantId);
        Assert.Equal(Identity, view.ServiceIdentityId);
        Assert.Equal(2, view.Revision);
        Assert.Equal(Author, view.LastChangedBy);
        Assert.Equal(Now, view.LastChangedAt);
        Assert.Equal(operation == "owner" ? "team" : "person", view.OwnerKind);
        Assert.Equal(operation == "owner" ? newOwner : Owner, view.OwnerId);
        Assert.Equal(operation == "purpose" ? "New approved purpose" : "Approved purpose", view.Purpose);
        Assert.Equal(operation == "review" ? Today.AddYears(1) : Today.AddMonths(6), view.ReviewBy);
        Assert.Equal(operation == "disable" ? "disabled" : operation == "retire" ? "retired" : "active", view.LifecycleStatus);
        if (operation == "review")
        {
            Assert.Equal(Today, view.LastReviewedOn);
            Assert.Equal(Author, view.LastReviewer);
            Assert.Equal("Annual review", view.ReviewNotes);
        }
    }

    [Fact]
    public async Task ShouldPreserveSharedJustificationGivenSharedAccountProjection()
    {
        // Arrange
        var directory = new FitzServiceIdentityDirectory(new InMemoryKvClient());
        var recorded = new ServiceIdentityRecorded(Tenant, Identity,
            new ServiceIdentityTerms("Emergency admin", "shared_account", "Break glass", null, "active",
                "person", Owner, Today.AddMonths(6), SharedJustification: "Emergency administration"), Author, Now);

        // Act
        await ApplyAsync(directory, recorded);
        var view = await directory.GetAsync(Tenant, Identity);

        // Assert
        Assert.NotNull(view);
        Assert.Equal("shared_account", view.IdentityKind);
        Assert.Equal("Emergency administration", view.SharedJustification);
    }

    static async Task ApplyAsync(FitzServiceIdentityDirectory directory, DomainEvent fact)
    {
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(
            new CheckpointIdentity("ServiceIdentityDirectoryV1",
                EventStreamPattern.ForPattern(Tenant.ToString(), "service-identities")), ProjectionCheckpoint.Start));
        await directory.ApplyAsync(fact);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }
}
