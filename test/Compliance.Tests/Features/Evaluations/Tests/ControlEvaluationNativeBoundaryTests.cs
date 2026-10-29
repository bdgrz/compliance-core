using System.Collections;
using System.Reflection;
using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.Evaluations.Handlers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Queries;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

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
