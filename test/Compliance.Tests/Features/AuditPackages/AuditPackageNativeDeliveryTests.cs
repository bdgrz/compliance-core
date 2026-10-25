using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.Artifacts;
using Bdgrz.Compliance.Features.AuditPackages;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.AuditPackages;

public sealed class AuditPackageNativeDeliveryTests
{
    static readonly DateTimeOffset HandoffAt = new(2026, 10, 17, 18, 4, 0, TimeSpan.Zero);

    [Fact]
    public async Task ShouldFreezeManifestAtAdmissionGivenDeliveryChildNotMaterialized()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var (packageId, _, _) = await AdmitAsync(fixture);
        var owner = await OwnerAsync(fixture, packageId);
        var admitted = Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, owner)).Facts.OfType<AuditPackageDeliveryAdmitted>());
        var child = await fixture.Reader.HydrateAsync(new AuditPackageDelivery(fixture.TenantId, packageId, 1,
            admitted.Operation.LogicalId));

        // Act
        var result = await fixture.RequestBuildHandler().HandleAsync(AuditPackageCommandHandlerTests.Fixture.Context(
            fixture.LeadUserId, new RequestAuditPackageBuild(fixture.TenantId, fixture.ProgramId,
                fixture.ExaminationId, packageId, "type_i", "after-delivery-admission", owner.Revision, 1)),
            CancellationToken.None);

        // Assert
        Assert.True(owner.IsFrozen(1));
        Assert.False(child.IsMaterialized);
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, await OwnerAsync(fixture, packageId))).Facts.OfType<AuditPackageBuildRequested>());
    }

    [Fact]
    public async Task ShouldMaterializeHistoricalHandoffAndReplayGivenLaterManifestVersion()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var (packageId, request, acknowledgment) = await AdmitAsync(fixture);
        var owner = await OwnerAsync(fixture, packageId);
        var admitted = Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, owner)).Facts.OfType<AuditPackageDeliveryAdmitted>());
        var rules = await fixture.ReadPackageAsync(packageId);
        var prior = rules.Current!.Versions[0];
        Assert.Null(rules.Amend(rules.Revision, "A later exact manifest version", prior.SourceSnapshotId,
            prior.SourceSnapshotSha256, prior.SelectedContent.Take(1).ToArray(), prior.CreatedByMemberId,
            prior.CreatedByDisplay, HandoffAt.AddMinutes(1)));
        await PackageSeeds.AppendAsync(fixture.Reader, fixture.Writer, fixture.PeriodEvents, fixture.ContentStore, rules.Current!);
        var command = Materialize(fixture, packageId, admitted, owner.Stream);
        var handler = Handler(fixture);

        // Act
        var applied = await handler.HandleAsync(System(command), CancellationToken.None);
        var replayed = await handler.HandleAsync(System(command), CancellationToken.None);
        var rootReplay = await fixture.RecordDeliveryHandler().HandleAsync(AuditPackageCommandHandlerTests.Fixture.Context(
            fixture.ApproverUserId, request), CancellationToken.None);
        var saved = await fixture.ReadPackageAsync(packageId);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(applied.Value);
        Assert.IsType<ReceiverOutcome.Replayed>(replayed.Value);
        Assert.Equal(acknowledgment, rootReplay.Value);
        Assert.Equal(2, saved.Current!.CurrentVersion);
        Assert.Empty(saved.Current.Versions[1].Deliveries);
        var delivery = Assert.Single(saved.Current.Versions[0].Deliveries);
        Assert.Equal(HandoffAt, delivery.UpdatedAt);
        Assert.Equal(request.DeliveryId, delivery.AuditPackageDeliveryId);
        Assert.Equal("external-receipt", delivery.DeliveryReference);
        var child = await fixture.Reader.HydrateAsync(new AuditPackageDelivery(fixture.TenantId, packageId, 1,
            request.DeliveryId));
        Assert.Equal(1, child.Revision);
        Assert.NotNull(child.Delivered!.OperationResult);
        var terminal = await fixture.ContentStore.ReadJsonAsync<MutationResultContent>(fixture.TenantId.ToString(),
            child.Delivered.OperationResult);
        Assert.Equal(UnitResult.Schema, terminal.Schema);
        var body = Assert.IsType<StandardResponseBody>(terminal.Value);
        Assert.IsType<UnitResult>(await fixture.ContentStore.ReadJsonAsync<UnitResult>(fixture.TenantId.ToString(), body.Content));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShouldWaitOnlyForUnavailableContentGivenHistoricalDeliveryProofRead(bool unavailable)
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var (packageId, _, _) = await AdmitAsync(fixture);
        var owner = await OwnerAsync(fixture, packageId);
        var admitted = Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, owner)).Facts.OfType<AuditPackageDeliveryAdmitted>());
        var command = Materialize(fixture, packageId, admitted, owner.Stream);
        var broken = new UnreadableIntent(fixture.ContentStore, admitted.Intent.ContentId, unavailable);
        var handler = new MaterializeRecordedAuditPackageDeliveryHandler(
            new AuditPackageSourceVerifier(fixture.Reader, broken, fixture.PeriodEvents), fixture.Reader,
            new ReceiverAppend(fixture.Executor), fixture.ContentStore, TimeProvider.System, fixture.PeriodEvents);

        // Act
        var result = await handler.HandleAsync(System(command), CancellationToken.None);
        var child = await fixture.Reader.HydrateAsync(new AuditPackageDelivery(fixture.TenantId, packageId, 1,
            admitted.Operation.LogicalId));

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(unavailable, result.Error!.IsTransient);
        Assert.False(child.IsMaterialized);
    }

    [Fact]
    public async Task ShouldRefuseDifferentAdmissionProofGivenDeliveryEffect()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var (packageId, _, _) = await AdmitAsync(fixture);
        var owner = await OwnerAsync(fixture, packageId);
        var admitted = Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, owner)).Facts.OfType<AuditPackageDeliveryAdmitted>());
        var command = Materialize(fixture, packageId, admitted, owner.Stream) with
        {
            Source = new NativeFactReference(owner.Stream, admitted.Metadata.EventId,
                admitted.Metadata.AggregateVersion + 1)
        };

        // Act
        var result = await Handler(fixture).HandleAsync(System(command), CancellationToken.None);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.False(result.Error!.IsTransient);
    }

    [Fact]
    public async Task ShouldPreserveAcceptedHistoryGivenArchiveUnavailableAfterDeliveryAdmission()
    {
        // Arrange
        await using var fixture = new AuditPackageCommandHandlerTests.Fixture();
        var (packageId, _, _) = await AdmitAsync(fixture);
        var owner = await OwnerAsync(fixture, packageId);
        var admitted = Assert.Single((await AuditPackageManifestHistory.ReadAsync(fixture.PeriodEvents, owner)).Facts.OfType<AuditPackageDeliveryAdmitted>());
        var intent = await fixture.ContentStore.ReadJsonAsync<PackageDeliveryRecordingIntent>(fixture.TenantId.ToString(),
            admitted.Intent);
        var package = await fixture.ReadPackageAsync(packageId);
        var build = package.Current!.Versions[0].BuildAttempts[^1];
        await fixture.ArtifactStore.QuarantineAsync(new ArtifactContentReference(fixture.TenantId,
            intent.ArchiveSha256, build.ArchiveBytes!.Value));

        // Act
        var result = await Handler(fixture).HandleAsync(System(Materialize(fixture, packageId, admitted, owner.Stream)),
            CancellationToken.None);

        // Assert
        Assert.IsType<ReceiverOutcome.Applied>(result.Value);
        var saved = await fixture.ReadPackageAsync(packageId);
        Assert.Equal(intent.ArchiveSha256, Assert.Single(saved.Current!.Versions[0].Deliveries).ArchiveSha256);
    }

    static async Task<(Uuid PackageId, RecordAuditPackageDelivery Request, OperationAcceptedResponse Acknowledgment)>
        AdmitAsync(AuditPackageCommandHandlerTests.Fixture fixture)
    {
        var packageId = await fixture.SeedBuiltAndValidatedPackageAsync();
        const string recipient = "auditor@example.test";
        var approval = new ApproveAuditPackageDisclosure(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
            packageId, 1, await fixture.ManifestRevisionAsync(packageId), recipient, "Type I examination", "Exact archive");
        var approved = await fixture.ApproveDisclosureHandler().HandleAsync(AuditPackageCommandHandlerTests.Fixture.Context(
            fixture.ApproverUserId, approval), CancellationToken.None);
        Assert.True(approved.IsSuccess, approved.Error?.Message);
        var id = AuditPackageCommandIdentity.Delivery(packageId, 1, recipient, "external-receipt");
        var request = new RecordAuditPackageDelivery(fixture.TenantId, fixture.ProgramId, packageId, 1, recipient,
            HandoffAt, "external-receipt", id, await fixture.ManifestRevisionAsync(packageId));
        var admitted = await fixture.RecordDeliveryHandler().HandleAsync(AuditPackageCommandHandlerTests.Fixture.Context(
            fixture.ApproverUserId, request), CancellationToken.None);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        return (packageId, request, admitted.Value!);
    }

    static ValueTask<AuditPackageManifest> OwnerAsync(AuditPackageCommandHandlerTests.Fixture fixture, Uuid packageId) =>
        fixture.Reader.HydrateAsync(new AuditPackageManifest(fixture.TenantId, packageId));
    static MaterializeRecordedAuditPackageDelivery Materialize(AuditPackageCommandHandlerTests.Fixture fixture,
        Uuid packageId, AuditPackageDeliveryAdmitted admitted, EventStreamAddress owner) =>
        new(EffectIdentity.CommandId(admitted.Metadata.EventId, $"{AuditPackageDeliveryRecordingProcess.Name}:{nameof(MaterializeRecordedAuditPackageDelivery)}", new AuditPackageDelivery(fixture.TenantId, packageId, 1, admitted.Operation.LogicalId).Stream.ToString(), admitted.Operation.Episode), NativeFactReference.Of(owner, admitted),
            new AuditPackageDelivery(fixture.TenantId, packageId, 1, admitted.Operation.LogicalId).Stream,
            admitted.Operation.LogicalId, 1, null, admitted.Intent, packageId, admitted.Operation.LogicalId, 1);
    static MaterializeRecordedAuditPackageDeliveryHandler Handler(AuditPackageCommandHandlerTests.Fixture fixture) =>
        new(new AuditPackageSourceVerifier(fixture.Reader, fixture.ContentStore, fixture.PeriodEvents), fixture.Reader,
            new ReceiverAppend(fixture.Executor), fixture.ContentStore, TimeProvider.System, fixture.PeriodEvents);
    static Cntryl.Portia.RequestContext<T> System<T>(T request) where T : IRequestBase =>
        new(request, new RequestDispatchContext(RequestActor.System));

    sealed class UnreadableIntent(IImmutableContentStore inner, Uuid contentId, bool unavailable) : IImmutableContentStore
    {
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid id, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, id, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => reference.ContentId != contentId
            ? inner.ReadVerifiedAsync(realm, reference, ct) : throw (unavailable
                ? new ImmutableContentUnavailableException("The immutable handoff is temporarily absent.")
                : new ImmutableContentIntegrityException("The immutable handoff digest differs."));
    }
}
