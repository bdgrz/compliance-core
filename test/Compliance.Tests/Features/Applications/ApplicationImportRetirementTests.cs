using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportRetirementTests
{
    [Fact]
    public async Task ShouldRetireOmittedTargetOnlyAfterPublicationGivenSealedProposalAndCompleteImpact()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        var host = fixture.Host;

        // Act: the plan carries the retire row bound to the sealed proposal and the current impact.
        var accepted = await host.AcceptAsync(fixture.Batch.Id, 3);
        var sealedBatch = await host.BatchAsync(fixture.Batch.Id);
        var plan = await ApplicationImportContent.ReadAsync<ImportFrozenPlanIntent>(host.Content, host.TenantId,
            sealedBatch.Plan!.FrozenPlan);
        var rows = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportPlannedRow>(host.Content,
            host.TenantId, plan.Rows);
        var claims = await ApplicationImportContent.ReadSelectionAsync<ApplicationImportSourceClaimRecord>(
            host.Content, host.TenantId, plan.SourceClaims);

        // Assert
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal(["create_new", "retire"], rows.Select(row => row.Decision));
        var retire = rows[1];
        Assert.Equal(fixture.Omitted, retire.ApplicationId);
        Assert.Equal(1, retire.ExpectedApplicationRevision);
        Assert.Equal(sealedBatch.Proposal!.ProposalSha256, retire.RetirementProposalSha256);
        Assert.Equal(fixture.ImpactDigest, retire.RetirementImpactDigest);
        Assert.Equal("Reviewed omission", retire.RetirementReason);
        Assert.Equal(sealedBatch.Proposal.Proposal, plan.OmissionProposal);
        Assert.Equal("record-omitted", Assert.Single(claims).Observation.SourceRecordId);

        // Act: the retirement stays invisible until the batch publishes, then the claim is gone.
        var record = await host.StartProcessAsync(fixture.Batch.Id);
        Assert.True((await host.SendAsync((await host.StepAsync(record, "claim", nameof(ClaimApplicationImportSource),
            record.Targets[0]))!)).IsSuccess);
        foreach (var target in record.Targets.Skip(1))
        {
            Assert.True((await host.SendAsync((await host.StepAsync(record, "reserve",
                nameof(ReserveApplicationImportTarget), target))!)).IsSuccess);
            Assert.True((await host.SendAsync((await host.StepAsync(record, "apply",
                nameof(ApplyApplicationImportEffect), target))!)).IsSuccess);
        }

        var before = await host.Reader.HydrateApplicationAsync(host.TenantId, fixture.Omitted);
        var run = await host.RunProcessAsync(fixture.Batch.Id);
        var after = await host.ApplicationAsync(fixture.Omitted);
        var view = await ApplicationImportSourceView.LoadAsync(host.Reader, host.Content, host.Events, host.TenantId, "manual",
            "applications", ct: CancellationToken.None);

        // Assert
        Assert.False(before.IsRetired);
        Assert.Equal(ProcessState.Completed, run.State);
        Assert.True(after.IsRetired);
        Assert.Equal(2, after.Revision);
        Assert.Equal(["record-present"], view.Value!.Claims.Select(claim => claim.Observation.SourceRecordId));
    }

    [Fact]
    public async Task ShouldRefuseAcceptanceGivenOmissionWithoutCurrentProposal()
    {
        // Arrange: no proposal was frozen for the omitted claim.
        await using var fixture = await Fixture.CreateAsync(freeze: false);

        // Act
        var accepted = await fixture.Host.AcceptAsync(fixture.Batch.Id, 2);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, accepted.Error?.Kind);
        Assert.Null((await fixture.Host.BatchAsync(fixture.Batch.Id)).Plan);
    }

    [Fact]
    public async Task ShouldRefuseAcceptanceGivenImpactWithKnownDependentReference()
    {
        // Arrange
        await using var fixture = await Fixture.CreateAsync();
        fixture.Host.Impacts.ByApplication[fixture.Omitted] = fixture.Host.Impacts.ByApplication[fixture.Omitted] with
        {
            Complete = false,
        };

        // Act
        var incomplete = await fixture.Host.AcceptAsync(fixture.Batch.Id, 3);
        fixture.Host.Impacts.ByApplication.Remove(fixture.Omitted);
        var unavailable = await fixture.Host.AcceptAsync(fixture.Batch.Id, 3);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, incomplete.Error?.Kind);
        Assert.False(unavailable.IsSuccess);
        Assert.Null((await fixture.Host.BatchAsync(fixture.Batch.Id)).Plan);
    }

    [Fact]
    public async Task ShouldInvalidateProposalGivenLaterSourceLifecycleChange()
    {
        // Arrange: another batch publishes to the source after the proposal was sealed.
        await using var fixture = await Fixture.CreateAsync();
        var host = fixture.Host;
        var other = await host.StageBatchAsync(key: "manual", rows: new ApplicationImportInputRow("record-late",
            "Late", "Purpose", null));
        var otherRow = (await host.RowsAsync(other)).Single().RowId;
        Assert.True((await host.CorrelateAsync(other.Id, otherRow, 1, "create_new")).IsSuccess);
        Assert.True((await host.AcceptAsync(other.Id, 2)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(other.Id)).State);

        // Act
        var accepted = await host.AcceptAsync(fixture.Batch.Id, 3);

        // Assert: the proposal was bound to the source position it was sealed at.
        Assert.Equal(RequestErrorKind.Conflict, accepted.Error?.Kind);
        Assert.Null((await host.BatchAsync(fixture.Batch.Id)).Plan);
    }

    [Fact]
    public async Task ShouldRejectProposalGivenSharedSourceTargetAliasOrChangedTarget()
    {
        // Arrange: two claims share one target, so an omission of one cannot retire it.
        await using var host = new ApplicationImportHost();
        var shared = await host.StageBatchAsync(rows: [new("alias-1", "One", "Purpose", null),
            new("alias-2", "Two", "Purpose", null)]);
        var rows = await host.RowsAsync(shared);
        var target = await host.SeedApplicationAsync();
        Assert.True((await host.CorrelateAsync(shared.Id, rows[0].RowId, 1, "link_existing", target, 1)).IsSuccess);
        Assert.True((await host.CorrelateAsync(shared.Id, rows[1].RowId, 2, "link_existing", target, 1)).IsSuccess);
        Assert.True((await host.AcceptAsync(shared.Id, 3)).IsSuccess);
        Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(shared.Id)).State);
        var complete = await host.StageBatchAsync("declared_complete", rows: new ApplicationImportInputRow("alias-1",
            "One", "Purpose", null));
        var source = await host.SourceAsync();

        // Act
        var frozen = await host.FreezeHandler.HandleAsync(host.Context(new FreezeApplicationImportOmissionProposal(
            Uuid.CreateVersion4(), host.TenantId, complete.Id, 1, source.CommittedStreamPosition,
            complete.ContentDigest!, "Reason")), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, frozen.Error?.Kind);
        Assert.Null((await host.BatchAsync(complete.Id)).Proposal);
    }

    sealed class Fixture : IAsyncDisposable
    {
        Fixture(ApplicationImportHost host) => Host = host;

        public ApplicationImportHost Host { get; }

        public ApplicationImportBatch Batch { get; private set; } = null!;

        public Uuid Omitted { get; private set; }

        public string ImpactDigest { get; } = new string('d', 64);

        /// <summary>
        ///     A published batch claims record-omitted for a created application; a complete-source batch observes
        ///     only record-present, correlates it and (optionally) freezes the omission proposal.
        /// </summary>
        public static async Task<Fixture> CreateAsync(bool freeze = true)
        {
            var fixture = new Fixture(new ApplicationImportHost());
            var host = fixture.Host;
            var original = await host.StageBatchAsync("partial", "manual", "applications",
                new ApplicationImportInputRow("record-omitted", "Omitted", "Purpose", null));
            var originalRow = (await host.RowsAsync(original)).Single().RowId;
            Assert.True((await host.CorrelateAsync(original.Id, originalRow, 1, "create_new")).IsSuccess);
            Assert.True((await host.AcceptAsync(original.Id, 2)).IsSuccess);
            Assert.Equal(ProcessState.Completed, (await host.RunProcessAsync(original.Id)).State);
            fixture.Omitted = ApplicationImportEffects.CreatedApplicationId(original.Id, originalRow);
            host.Impacts.ByApplication[fixture.Omitted] = new ApplicationChangePreview(host.TenantId, fixture.Omitted, 1,
                "retire", [], [], [], true)
            {
                ImpactDigest = fixture.ImpactDigest,
            };
            var batch = await host.StageBatchAsync("declared_complete", "manual", "applications",
                new ApplicationImportInputRow("record-present", "Present", "Purpose", null));
            var row = (await host.RowsAsync(batch)).Single().RowId;
            Assert.True((await host.CorrelateAsync(batch.Id, row, 1, "create_new")).IsSuccess);
            if (freeze)
            {
                var source = await host.SourceAsync();
                var sealedProposal = await host.FreezeHandler.HandleAsync(host.Context(
                    new FreezeApplicationImportOmissionProposal(Uuid.CreateVersion4(), host.TenantId, batch.Id, 2,
                        source.CommittedStreamPosition, batch.ContentDigest!, "Reviewed omission")),
                    CancellationToken.None);
                Assert.True(sealedProposal.IsSuccess, sealedProposal.Error?.Message);
            }

            fixture.Batch = await host.BatchAsync(batch.Id);
            return fixture;
        }

        public ValueTask DisposeAsync() => Host.DisposeAsync();
    }
}
