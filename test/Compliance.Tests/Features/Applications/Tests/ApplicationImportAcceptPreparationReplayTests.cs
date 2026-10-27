using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Applications.Commands;
using Bdgrz.Compliance.Features.Applications.Handlers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Applications.Tests;

public sealed class ApplicationImportAcceptPreparationReplayTests
{
    [Fact]
    public async Task ShouldReplayEqualAcceptanceGivenLosingPlanWriteUnavailableAfterConcurrentSeal()
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        var batch = await host.StageBatchAsync();
        var row = Assert.Single(await host.RowsAsync(batch));
        Assert.True((await host.CorrelateAsync(batch.Id, row.RowId, 1, "create_new")).IsSuccess);
        var command = new AcceptApplicationImport(Uuid.CreateVersion4(), host.TenantId, batch.Id, 2);
        OperationAcceptedResponse? winner = null;
        var store = new LosingPlanStore(host.Content, async () =>
        {
            var accepted = await host.AcceptAsync(batch.Id, 2, command.CommandId);
            Assert.True(accepted.IsSuccess, accepted.Error?.Message);
            winner = accepted.Value;
        });
        var native = new NativeOperationExecutor(host.Reader, host.Services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = ActivatorUtilities.CreateInstance<AcceptApplicationImportHandler>(host.Services, native, store);
        Result<OperationAcceptedResponse> replay = default;

        // Act
        var error = await Record.ExceptionAsync(async () => replay = await handler.HandleAsync(host.Context(command), CancellationToken.None));
        var saved = await host.BatchAsync(batch.Id);

        // Assert
        Assert.Null(error);
        Assert.True(replay.IsSuccess, replay.Error?.Message);
        Assert.Equal(winner, replay.Value);
        Assert.NotNull(saved.Plan);
        Assert.Equal(3, saved.Revision);
        Assert.Equal(1, store.CompetingSeals);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldPreserveRequestedCancellationGivenInitialNativeReadIntegrityFailure(bool unavailable)
    {
        // Arrange
        await using var host = new ApplicationImportHost();
        using var cancellation = new CancellationTokenSource();
        var reader = new CancelingReader(cancellation, unavailable);
        var handler = new StageApplicationImportHandler(host.Services.GetRequiredService<NativeOperationExecutor>(),
            reader, host.Content, host.Clock);
        var request = new StageApplicationImport(host.TenantId, Uuid.CreateVersion4(), "manual", "applications", "partial",
            [new("record-1", "Payroll", "Pay staff", null)]);

        // Act
        var error = await Record.ExceptionAsync(async () =>
            await handler.HandleAsync(host.Context(request), cancellation.Token));

        // Assert
        Assert.Equal(cancellation.Token, Assert.IsAssignableFrom<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(1, reader.Reads);
    }

    sealed class CancelingReader(CancellationTokenSource cancellation, bool unavailable) : IAggregateReader
    {
        public int Reads { get; private set; }
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            Reads++;
            cancellation.Cancel();
            throw unavailable
                ? new ImmutableContentUnavailableException("Required initial native bytes are unavailable.")
                : new ImmutableContentIntegrityException("Required initial native bytes are corrupt.");
        }
    }

    sealed class LosingPlanStore(IImmutableContentStore inner, Func<Task> seal) : IImmutableContentStore
    {
        bool _armed = true;
        public int CompetingSeals { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (_armed && schema == ImportFrozenPlanIntent.Schema)
            {
                _armed = false;
                await seal();
                CompetingSeals++;
                throw new ImmutableContentUnavailableException("The losing frozen plan write is temporarily unavailable.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
