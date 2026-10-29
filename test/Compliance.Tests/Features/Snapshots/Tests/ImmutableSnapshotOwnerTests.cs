using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Snapshots.Aggregates;
using Bdgrz.Compliance.Features.Snapshots.Events;
using Bdgrz.Compliance.Features.Snapshots.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Snapshots.Tests;

/// <summary>Owner-level decisions for one immutable program scope snapshot.</summary>
public sealed class ImmutableSnapshotOwnerTests
{
    [Theory]
    [InlineData("tenant")]
    [InlineData("predecessor")]
    [InlineData("program_revision")]
    [InlineData("program_hash")]
    [InlineData("boundary")]
    [InlineData("boundary_version")]
    [InlineData("boundary_hash")]
    public void ShouldRefuseWithoutFactGivenIncompleteSnapshotIdentityOrSource(string defect)
    {
        // Arrange
        var tenant = defect == "tenant" ? Uuid.Empty : Uuid.CreateVersion4();
        var snapshot = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var root = Uuid.CreateVersion4();
        Uuid? prior = defect == "predecessor" ? Uuid.Empty : root;
        var manifest = Manifest(tenant, program);
        manifest = defect switch
        {
            "program_revision" => manifest with { ProgramRevision = 0 },
            "program_hash" => manifest with { ProgramContentSha256 = "invalid" },
            "boundary" => manifest with { BoundaryId = Uuid.Empty },
            "boundary_version" => manifest with { ApprovedBoundaryVersionId = Uuid.Empty },
            "boundary_hash" => manifest with { BoundaryContentSha256 = "invalid" },
            _ => manifest,
        };
        var (json, hash) = SnapshotContentIdentity.Manifest(manifest);
        var owner = new ImmutableSnapshot(tenant, snapshot);
        var scenario = new AggregateScenario<ImmutableSnapshot>(owner);

        // Act
        var result = owner.Freeze(root, prior, program, manifest, json, hash, "Correction",
            Uuid.CreateVersion4(), "Lead", NativeFacts.Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.False(owner.IsFrozen);
    }

    [Fact]
    public void ShouldRefuseConstructionGivenEmptySnapshotIdentity()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();

        // Act
        var construct = () => new ImmutableSnapshot(tenant, Uuid.Empty);

        // Assert
        Assert.Throws<ArgumentException>(construct);
    }

    [Fact]
    public void ShouldRetainOriginalAndRefuseChangedIntentGivenValidReplay()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var snapshot = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var manifest = Manifest(tenant, program);
        var (json, hash) = SnapshotContentIdentity.Manifest(manifest);
        var seed = new ImmutableSnapshot(tenant, snapshot);
        Assert.True(seed.Freeze(snapshot, null, program, manifest, json, hash, null, actor, "Lead", NativeFacts.Now).IsSuccess);
        var owner = NativeFacts.Replay(new ImmutableSnapshot(tenant, snapshot), NativeFacts.Raised(seed));
        var scenario = new AggregateScenario<ImmutableSnapshot>(owner);
        var changedManifest = manifest with { ProgramRevision = 2 };
        var (changedJson, changedHash) = SnapshotContentIdentity.Manifest(changedManifest);

        // Act
        var retry = owner.Freeze(snapshot, null, program, manifest, json, hash, null, actor, "Lead", NativeFacts.Now);
        var changed = owner.Freeze(snapshot, null, program, changedManifest, changedJson, changedHash, null,
            actor, "Lead", NativeFacts.Now);

        // Assert
        Assert.True(retry.IsSuccess);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(hash, owner.ContentSha256);
        Assert.Equal(manifest, owner.Manifest);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("snapshot")]
    [InlineData("duplicate")]
    [InlineData("kind")]
    [InlineData("manifest")]
    public void ShouldRefuseRetryWithoutFactGivenInconsistentFrozenHistory(string defect)
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var snapshot = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var actor = Uuid.CreateVersion4();
        var manifest = Manifest(tenant, program);
        var (json, hash) = SnapshotContentIdentity.Manifest(manifest);
        var owner = new ImmutableSnapshot(tenant, snapshot);
        var scenario = new AggregateScenario<ImmutableSnapshot>(owner);
        var fact = new SnapshotFrozen(defect == "tenant" ? Uuid.CreateVersion4() : tenant,
            defect == "snapshot" ? Uuid.CreateVersion4() : snapshot, snapshot, null, program,
            defect == "kind" ? "other" : "program_scope",
            defect == "manifest" ? manifest with { ProgramRevision = 2 } : manifest,
            json, hash, null, actor, "Lead", NativeFacts.Now);
        var duplicate = fact with { };
        var first = DomainEventSeed.Attach(fact, snapshot, 1);
        if (defect == "duplicate")
            scenario.Given(first, DomainEventSeed.Attach(duplicate, snapshot, 2));
        else
            scenario.Given(first);

        // Act
        var retry = owner.Freeze(snapshot, null, program, manifest, json, hash, null, actor, "Lead", NativeFacts.Now);

        // Assert
        Assert.False(retry.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, retry.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
    }

    static ProgramScopeManifest Manifest(Uuid tenant, Uuid program) =>
        new(1, tenant, program, 1, new string('a', 64), Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            new string('b', 64));
}
