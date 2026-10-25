using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

public sealed partial class EvidenceRedactionCompositionTests
{
    [Fact]
    public async Task ShouldAllowIndependentCurrentApprovalAfterLifetimePreparationsGivenNativeHistoryBoundary()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        var id = Uuid.CreateVersion4();
        EvidenceRedactionView? last = null;
        for (var revision = 0; revision < 100; revision++)
        {
            var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId,
                id, revision, fixture.ArtifactId, derived, "Manual lineage", $"Preparation reason {revision}"));
            Assert.True(prepared.IsSuccess, prepared.Error?.Message);
            last = prepared.Value;
        }
        var current = last!.Preparations[^1];

        // Act
        var approved = await DispatchAsync(fixture, lead, new ApproveEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, id,
            100, current.PreparationId, current.Revision));
        var history = await DispatchAsync(fixture, lead, new GetEvidenceRedaction(fixture.TenantId, id));

        // Assert
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        Assert.True(history.IsSuccess, history.Error?.Message);
        Assert.Equal(101, approved.Value.Revision);
        Assert.Equal(100, history.Value.Preparations.Count);
        Assert.Equal(current.PreparationId, Assert.Single(history.Value.Approvals).PreparationId);
        Assert.True(history.Value.ApprovalCurrent);
        Assert.Equal("Preparation reason 0", history.Value.Preparations[0].Reason);
    }

    [Fact]
    public async Task ShouldKeepCommandOwnerBoundedWithCompleteHistoryGivenNativeHistoryBoundary()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var id = Uuid.CreateVersion4();
        for (var revision = 0; revision < 24; revision++)
        {
            var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId,
                id, revision, fixture.ArtifactId, derived, "Manual lineage", $"Retained reason {revision}"));
            Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        }

        // Act
        var current = await ProgramManagementServices.HydrateAsync(fixture.Provider, new EvidenceRedaction(fixture.TenantId, id));
        var history = await DispatchAsync(fixture, fixture.UserId, new GetEvidenceRedaction(fixture.TenantId, id));

        // Assert
        Assert.True(history.IsSuccess, history.Error?.Message);
        Assert.Equal(24, history.Value.Preparations.Count);
        Assert.Equal(24, current.Revision);
        Assert.Equal(history.Value.Preparations[^1].PreparationId, current.CurrentPreparation!.PreparationId);
        Assert.Equal("Retained reason 0", history.Value.Preparations[0].Reason);
        Assert.All(typeof(EvidenceRedaction).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly), field =>
            Assert.False(field.GetValue(current) is IEnumerable retained and not string && retained.Cast<object>().Skip(2).Any(),
                $"The command owner retains lifetime redaction history in {field.Name}."));
    }

    [Fact]
    public async Task ShouldReplayOriginalPreparationViewAfterLaterPreparationGivenNativeHistoryBoundary()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = await SeedDerivedAsync(fixture);
        await GrantPairAsync(fixture, fixture.UserId, derived);
        var id = Uuid.CreateVersion4();
        var request = new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, id, 0, fixture.ArtifactId, derived, "Manual lineage", "First reason");
        var metadata = new RequestMetadata(request.CommandId, request.CommandId);
        var first = await DispatchAsync(fixture, fixture.UserId, request, metadata: metadata);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True((await DispatchAsync(fixture, fixture.UserId, request with { CommandId = Uuid.CreateVersion4(), ExpectedRevision = 1, Reason = "Later reason" })).IsSuccess);

        // Act
        var replayed = await DispatchAsync(fixture, fixture.UserId, request, metadata: metadata);
        var current = await DispatchAsync(fixture, fixture.UserId, new GetEvidenceRedaction(fixture.TenantId, id));

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(RedactionWire(first.Value), RedactionWire(replayed.Value));
        Assert.Equal(1, replayed.Value.Revision);
        Assert.Equal(2, current.Value.Revision);
        Assert.Equal(2, current.Value.Preparations.Count);
    }

    [Fact]
    public async Task ShouldReplayOriginalApprovalViewAfterLaterPreparationGivenNativeHistoryBoundary()
    {
        // Arrange
        await using var fixture = await ArtifactMetadataAccessTests.Fixture.CreateAsync();
        await fixture.AllowAsync();
        var derived = await SeedDerivedAsync(fixture);
        var lead = await SeedMemberAsync(fixture, BuiltInRbac.PowerUsersTeamId(fixture.TenantId));
        await GrantPairAsync(fixture, fixture.UserId, derived);
        await GrantPairAsync(fixture, lead, derived);
        var id = Uuid.CreateVersion4();
        var prepared = await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId,
            id, 0, fixture.ArtifactId, derived, "Manual lineage", "First reason"));
        Assert.True(prepared.IsSuccess, prepared.Error?.Message);
        var preparation = prepared.Value.Preparations[0];
        var request = new ApproveEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId, id, 1, preparation.PreparationId, preparation.Revision);
        var metadata = new RequestMetadata(request.CommandId, request.CommandId);
        var first = await DispatchAsync(fixture, lead, request, metadata: metadata);
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True((await DispatchAsync(fixture, fixture.UserId, new PrepareEvidenceRedaction(Uuid.CreateVersion4(), fixture.TenantId,
            id, 2, fixture.ArtifactId, derived, "Manual lineage", "Later reason"))).IsSuccess);

        // Act
        var replayed = await DispatchAsync(fixture, lead, request, metadata: metadata);
        var current = await DispatchAsync(fixture, lead, new GetEvidenceRedaction(fixture.TenantId, id));

        // Assert
        Assert.True(replayed.IsSuccess, replayed.Error?.Message);
        Assert.Equal(RedactionWire(first.Value), RedactionWire(replayed.Value));
        Assert.True(replayed.Value.ApprovalCurrent);
        Assert.Equal(2, replayed.Value.Revision);
        Assert.False(current.Value.ApprovalCurrent);
        Assert.Equal(3, current.Value.Revision);
    }

    static string RedactionWire(EvidenceRedactionView value) => JsonSerializer.Serialize(value,
        ComplianceCoreJsonContext.Default.EvidenceRedactionView);
}
