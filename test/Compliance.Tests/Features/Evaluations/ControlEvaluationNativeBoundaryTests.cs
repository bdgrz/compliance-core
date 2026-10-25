using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Evaluations;

public sealed class ControlEvaluationNativeBoundaryTests
{
    [Fact]
    public async Task ShouldKeepBoundedAuthorityGivenRepeatedStepCorrections()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var current = await ControlEvaluationTests.StartAsync(fixture);
        for (var index = 0; index < 48; index++)
            current = await fixture.AsAsync(fixture.OwnerUserId, ControlEvaluationTests.Record(fixture, current, 0));

        // Act
        var owner = await ProgramManagementServices.HydrateAsync(fixture.Provider,
            new ControlEvaluation(fixture.TenantId, current.EvaluationId));
        var retained = typeof(ControlEvaluation).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 1 }).ToArray();

        // Assert
        Assert.Empty(retained);
        Assert.Equal(49, owner.Revision);
        var view = await fixture.AsAsync(fixture.OwnerUserId, new GetControlEvaluation(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, current.EvaluationId));
        Assert.Equal(current.Revision, view.Revision);
        Assert.NotNull(view.Steps[0].Result);
    }

    [Fact]
    public async Task ShouldReplayEqualStepWinnerGivenLosingResultWriteUnavailable()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var current = await ControlEvaluationTests.StartAsync(fixture);
        var request = ControlEvaluationTests.Record(fixture, current, 0);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        ControlEvaluationView? winner = null;
        var store = new LosingStore(fixture.Content, async () =>
            winner = await fixture.AsAsync(fixture.OwnerUserId, request));
        var reader = services.GetRequiredService<IAggregateReader>();
        var evaluations = new ControlEvaluationReader(reader, services.GetRequiredService<IDomainEventReader>(), store);
        var operations = new NativeOperationExecutor(reader, services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = new RecordControlEvaluationStepHandler(operations, evaluations, store, TimeProvider.System);
        var context = new InventoryRequestContext<RecordControlEvaluationStep>(request,
            ProgramManagementServices.Actor(fixture.OwnerUserId));
        Result<ControlEvaluationView> result = default;

        // Act
        var error = await Record.ExceptionAsync(async () => result = await handler.HandleAsync(context, CancellationToken.None));

        // Assert
        Assert.True(store.Failed);
        Assert.Null(error);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(JsonSerializer.Serialize(winner, ComplianceCoreJsonContext.Default.ControlEvaluationView),
            JsonSerializer.Serialize(result.Value, ComplianceCoreJsonContext.Default.ControlEvaluationView));
    }

    [Fact]
    public async Task ShouldReplayOriginalStepGivenLaterResultBodyUnavailable()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var current = await ControlEvaluationTests.StartAsync(fixture);
        var request = ControlEvaluationTests.Record(fixture, current, 0);
        var first = await fixture.AsAsync(fixture.OwnerUserId, request);
        var successor = ControlEvaluationTests.Record(fixture, first, 1);
        await fixture.AsAsync(fixture.OwnerUserId, successor);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var events = services.GetRequiredService<IDomainEventReader>();
        ImmutableContentRef? unavailable = null;
        var stream = new ControlEvaluation(fixture.TenantId, current.EvaluationId).Stream;
        await foreach (var record in events.ReadAsync(stream, 0, CancellationToken.None))
            if (record.Event is ControlEvaluationStepRecorded step && step.Operation.LogicalId == successor.CommandId)
                unavailable = step.ResultContent;
        Assert.NotNull(unavailable);
        var store = new MissingResultStore(fixture.Content, unavailable);
        var evaluations = new ControlEvaluationReader(reader, events, store);
        var native = new NativeOperationExecutor(reader, services.GetRequiredService<IAggregateExecutor>(), store);
        var handler = new RecordControlEvaluationStepHandler(native, evaluations, store, TimeProvider.System);
        var context = new InventoryRequestContext<RecordControlEvaluationStep>(request,
            ProgramManagementServices.Actor(fixture.OwnerUserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(WireJson.Of(first), WireJson.Of(result.Value));
        Assert.Equal(0, store.BlockedReads);
    }

    sealed class MissingResultStore(IImmutableContentStore inner, ImmutableContentRef unavailable) : IImmutableContentStore
    {
        public int BlockedReads { get; private set; }
        public Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default) =>
            inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default)
        {
            if (reference == unavailable)
            {
                BlockedReads++;
                throw new ImmutableContentUnavailableException("A later independent step result is temporarily missing.");
            }
            return inner.ReadVerifiedAsync(realm, reference, ct);
        }
    }

    sealed class LosingStore(IImmutableContentStore inner, Func<Task> admit) : IImmutableContentStore
    {
        public bool Failed { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed && schema == "ControlEvaluationStepRecorded.ResultContent.v1")
            {
                Failed = true;
                await admit();
                throw new ImmutableContentUnavailableException("The losing step candidate is unavailable after the equal winner.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
