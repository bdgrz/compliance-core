using Bdgrz.Compliance.Features.Applications;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Applications;

public sealed class ApplicationImportPreparationSafetyTests
{
    [Theory]
    [InlineData("reserve")]
    [InlineData("apply")]
    public async Task ShouldRejectChangedRowReferenceGivenPreviouslyAcceptedTargetGroup(string receiver)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await ApplicationImportTargetPreparationTests.SeedAsync(host, receiver);
        var first = await host.SendAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = command switch
        {
            ReserveApplicationImportTarget value => (IRequest<ReceiverOutcome>)(value with { GroupedRows = value.Intent }),
            ApplyApplicationImportEffect value => value with { RowGroup = value.Intent },
            _ => throw new ArgumentException("The receiver has no extra row reference.", nameof(receiver)),
        };

        // Act
        var refused = await host.SendAsync(changed);

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.False(refused.Error!.IsTransient);
    }

    [Theory]
    [InlineData("reserve")]
    [InlineData("apply")]
    [InlineData("complete-cancel")]
    public async Task ShouldWaitForRequiredExtraTermBindingGivenAcceptedReceiverAndUnavailableBindingBytes(string receiver)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await ApplicationImportTargetPreparationTests.SeedAsync(host, receiver);
        var first = await host.SendAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var failedReference = command switch
        {
            ReserveApplicationImportTarget value => value.Intent,
            ApplyApplicationImportEffect value => value.Intent,
            CompleteApplicationImportCancellation value => value.TargetFenceManifest,
            _ => throw new ArgumentException("The receiver has no required extra binding.", nameof(receiver)),
        };
        var fault = new TypedFaultStore(host.Content, failedReference.Schema,
            new ImmutableContentUnavailableException("The required extra term cannot be verified."));

        // Act
        var result = await ApplicationImportTargetPreparationTests.HandleAsync(
            ApplicationImportReceiverPreparationTests.Receivers(host, fault), command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.Error!.IsTransient);
        Assert.Equal(1, fault.Reads);
    }

    [Theory]
    [InlineData("claim", "unavailable")]
    [InlineData("claim", "corrupt")]
    [InlineData("claim", "programmer")]
    [InlineData("source-release", "unavailable")]
    [InlineData("source-release", "corrupt")]
    [InlineData("source-release", "programmer")]
    [InlineData("target-release", "unavailable")]
    [InlineData("target-release", "corrupt")]
    [InlineData("target-release", "programmer")]
    [InlineData("reserve", "unavailable")]
    [InlineData("reserve", "corrupt")]
    [InlineData("reserve", "programmer")]
    [InlineData("apply", "unavailable")]
    [InlineData("apply", "corrupt")]
    [InlineData("apply", "programmer")]
    [InlineData("commit", "unavailable")]
    [InlineData("commit", "corrupt")]
    [InlineData("commit", "programmer")]
    [InlineData("fence", "unavailable")]
    [InlineData("fence", "corrupt")]
    [InlineData("fence", "programmer")]
    [InlineData("complete-cancel", "unavailable")]
    [InlineData("complete-cancel", "corrupt")]
    [InlineData("complete-cancel", "programmer")]
    public async Task ShouldPreserveFailureKindAndProgrammerFaultGivenNoNativeReceiptAndRequiredPlanReadFails(
        string receiver, string kind)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = receiver is "claim" or "source-release" or "target-release"
            ? await ApplicationImportReceiverPreparationTests.SeedAsync(host, receiver)
            : await ApplicationImportTargetPreparationTests.SeedAsync(host, receiver);
        var expected = kind switch
        {
            "unavailable" => (Exception)new ImmutableContentUnavailableException("Required captured plan unavailable."),
            "corrupt" => new ImmutableContentIntegrityException("Required captured plan corrupt."),
            _ => new InvalidOperationException("Unexpected preparation fault."),
        };
        var fault = new TypedFaultStore(host.Content, ImportFrozenPlanIntent.Schema, expected);
        var receivers = ApplicationImportReceiverPreparationTests.Receivers(host, fault);
        var before = (await host.EventsAsync<DomainEvent>(TargetOf(command))).Count;
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = command is ClaimApplicationImportSource or
            ReleaseApplicationImportSource or ReleaseApplicationImportTarget
                ? await ApplicationImportReceiverPreparationTests.HandleAsync(receivers, command, CancellationToken.None)
                : await ApplicationImportTargetPreparationTests.HandleAsync(receivers, command, CancellationToken.None));

        // Assert
        if (kind == "programmer")
            Assert.Same(expected, failure);
        else
        {
            Assert.Null(failure);
            Assert.False(result.IsSuccess);
            Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
            Assert.Equal(kind == "unavailable", result.Error!.IsTransient);
        }
        Assert.Equal(1, fault.Reads);
        Assert.Equal(before, (await host.EventsAsync<DomainEvent>(TargetOf(command))).Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(1L)]
    [InlineData(-1L)]
    public async Task ShouldBindExpectedSourceRevisionToOriginalNativeAdmissionGivenAcceptedClaim(long? changedRevision)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = Assert.IsType<ClaimApplicationImportSource>(
            await ApplicationImportReceiverPreparationTests.SeedAsync(host, "claim"));
        Assert.Equal(0L, command.ExpectedRevision);
        var first = await host.SendAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var source = await host.SourceAsync(selectedBatchId: command.BatchId);
        Assert.Equal(1L, source.Find(command.BatchId)!.Admitted!.Revision);

        // Act
        var refused = await host.SendAsync(command with { ExpectedRevision = changedRevision });

        // Assert
        Assert.False(refused.IsSuccess);
        Assert.False(refused.Error!.IsTransient);
        Assert.Single(await host.EventsAsync<ApplicationImportSourceAdmissionRecorded>(command.Target));
    }

    [Theory]
    [InlineData("commit")]
    [InlineData("complete-cancel")]
    public async Task ShouldReturnOriginalIrreversibleReceiptGivenAnotherLegacyProofClaim(string receiver)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await ApplicationImportTargetPreparationTests.SeedAsync(host, receiver);
        var first = await host.SendAsync(command);
        Assert.True(first.IsSuccess, first.Error?.Message);
        var changed = command switch
        {
            CommitApplicationImportBatch value => (IRequest<ReceiverOutcome>)(value with { TargetProofManifest = value.Intent }),
            CompleteApplicationImportCancellation value => value with
            {
                TargetFenceManifest = await host.Content.WriteJsonAsync<IReadOnlyList<ApplicationImportFenceProof>>(
                    host.TenantId.ToString(), Uuid.CreateVersion4(), "import-fence-manifest.v1", []),
            },
            _ => throw new ArgumentException("The receiver has no irreversible proof-set receipt.", nameof(receiver)),
        };

        // Act
        var replay = await host.SendAsync(changed);

        // Assert
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(Assert.IsType<ReceiverOutcome.Applied>(first.Value).Fact,
            Assert.IsType<ReceiverOutcome.Replayed>(replay.Value).Fact);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireCurrentTenantGivenPreviouslyAcceptedExecutionOrTargetApply(bool execute)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        if (execute)
        {
            var (batch, process, _) = await ApplicationImportReceiverPreparationTests.SealedAsync(host);
            var claim = (await host.StepAsync(process, "claim", nameof(ClaimApplicationImportSource), process.Targets[0]))!;
            Assert.True((await host.SendAsync(claim)).IsSuccess);
            var command = new ExecuteApplicationImport(ApplicationImportExecutionProcess.ExecuteCommandId(process), host.TenantId, batch.Id);
            var handler = host.Services.GetRequiredService<ExecuteApplicationImportHandler>();
            var first = await handler.HandleAsync(ApplicationImportReceiverPreparationTests.Context(command), CancellationToken.None);
            Assert.True(first.IsSuccess, first.Error?.Message);
            host.Activity.Active = false;

            // Act
            var refused = await handler.HandleAsync(ApplicationImportReceiverPreparationTests.Context(command), CancellationToken.None);

            // Assert
            Assert.False(refused.IsSuccess);
            Assert.True(refused.Error!.IsTransient);
            Assert.Single(await host.EventsAsync<ApplicationImportExecutionAuthorized>(batch.Stream));
        }
        else
        {
            var command = await ApplicationImportTargetPreparationTests.SeedAsync(host, "apply");
            var first = await host.SendAsync(command);
            Assert.True(first.IsSuccess, first.Error?.Message);
            host.Activity.Active = false;

            // Act
            var refused = await host.SendAsync(command);

            // Assert
            Assert.False(refused.IsSuccess);
            Assert.True(refused.Error!.IsTransient);
        }
    }

    [Theory]
    [InlineData("reserve", false)]
    [InlineData("reserve", true)]
    [InlineData("commit", false)]
    [InlineData("commit", true)]
    [InlineData("complete-cancel", false)]
    [InlineData("complete-cancel", true)]
    public async Task ShouldRecoverOriginalReceiptOrRequestedCancellationGivenWinnerBeforeReceiverContentWriteFailure(
        string receiver, bool cancel)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var command = await ApplicationImportTargetPreparationTests.SeedAsync(host, receiver);
        var healthy = ApplicationImportReceiverPreparationTests.Receivers(host, host.Content);
        using var cancellation = new CancellationTokenSource();
        var winner = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "No winner yet."));
        var fault = new WriteFaultStore(host.Content, async () =>
        {
            winner = await ApplicationImportTargetPreparationTests.HandleAsync(healthy, command, CancellationToken.None);
            Assert.True(winner.IsSuccess, winner.Error?.Message);
            if (cancel)
                cancellation.Cancel();
        });
        var result = Result<ReceiverOutcome>.Failure(new RequestError(RequestErrorKind.Conflict, "Not executed yet."));

        // Act
        var failure = await Record.ExceptionAsync(async () => result = await ApplicationImportTargetPreparationTests.HandleAsync(
            ApplicationImportReceiverPreparationTests.Receivers(host, fault), command, cancellation.Token));

        // Assert
        Assert.True(winner.IsSuccess);
        var original = Assert.IsType<ReceiverOutcome.Applied>(winner.Value).Fact;
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(failure).CancellationToken);
        else
        {
            Assert.Null(failure);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal(original, Assert.IsType<ReceiverOutcome.Replayed>(result.Value).Fact);
        }
        Assert.Equal(1, fault.Writes);
    }

    internal static EventStreamAddress TargetOf(IRequest<ReceiverOutcome> command) => command switch
    {
        ClaimApplicationImportSource value => value.Target,
        ReleaseApplicationImportSource value => value.Target,
        ReleaseApplicationImportTarget value => value.Target,
        ReserveApplicationImportTarget value => value.Target,
        ApplyApplicationImportEffect value => value.Target,
        CommitApplicationImportBatch value => value.Target,
        FenceApplicationImportTarget value => value.Target,
        CompleteApplicationImportCancellation value => value.Target,
        _ => throw new ArgumentException("The fixture has no target for this receiver.", nameof(command)),
    };

    sealed class WriteFaultStore(IImmutableContentStore inner, Func<Task> beforeFailure) : IImmutableContentStore
    {
        public int Writes { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (schema is ImportTargetGroupIntent.Schema or UnitResult.Schema)
            {
                Writes++;
                await beforeFailure();
                throw new ImmutableContentUnavailableException("The actual receiver content write is unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }

    sealed class TypedFaultStore(IImmutableContentStore inner, string failedSchema, Exception failure) : IImmutableContentStore
    {
        public int Reads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema, string mediaType,
            ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference.Schema != failedSchema)
                return inner.ReadVerifiedAsync(realm, reference, ct);
            Reads++;
            throw failure;
        }
    }
}
