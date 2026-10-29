using System.Text.Json;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Aggregates;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Handlers;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Evaluations.Tests;

public sealed class EvaluationPlanNativeRecoveryTests
{
    [Fact]
    public async Task ShouldRecoverOriginalAcceptedResultGivenEqualWinnerBeforePreparationContentFailure()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var request = new DefineControlEvaluationPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
            fixture.ControlVersionId, 0, "Review exact source",
            [new EvaluationProcedureStep("design", "inspection",
                [new EvaluationInspectedItem("control", fixture.ControlId.ToString(), fixture.ControlVersionId.ToString())],
                "The exact source is implemented.")], false);
        ControlEvaluationPlanVersionView? winner = null;
        var content = new CommitThenFailContent(fixture.Content, async () =>
        {
            winner = await fixture.AsAsync(fixture.LeadUserId, request);
        });
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var reader = services.GetRequiredService<IAggregateReader>();
        var operations = new NativeOperationExecutor(reader, services.GetRequiredService<IAggregateExecutor>(), content);
        var handler = new DefineControlEvaluationPlanHandler(operations, services.GetRequiredService<ControlReader>(),
            services.GetRequiredService<OperatingAuthority>(), content, TimeProvider.System);
        var context = new InventoryRequestContext<DefineControlEvaluationPlan>(request,
            ProgramManagementServices.Actor(fixture.LeadUserId));

        // Act
        var result = await handler.HandleAsync(context, CancellationToken.None);
        var owner = await reader.HydrateAsync(new ControlEvaluationPlan(fixture.TenantId, fixture.ProgramId, fixture.ControlId));

        // Assert
        Assert.True(content.Failed);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(winner);
        Assert.Equal(JsonSerializer.Serialize(winner, ComplianceCoreJsonContext.Default.ControlEvaluationPlanVersionView),
            JsonSerializer.Serialize(result.Value, ComplianceCoreJsonContext.Default.ControlEvaluationPlanVersionView));
        Assert.Equal(1, owner.Revision);
    }

    sealed class CommitThenFailContent(IImmutableContentStore inner, Func<Task> commitWinner) : IImmutableContentStore
    {
        public bool Failed { get; private set; }
        public async Task<ImmutableContentRef> WriteVerifiedAsync(string realm, Uuid contentId, string schema,
            string mediaType, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
        {
            if (!Failed)
            {
                Failed = true;
                await commitWinner();
                throw new ImmutableContentUnavailableException("Content became unavailable after the equal winner committed.");
            }
            return await inner.WriteVerifiedAsync(realm, contentId, schema, mediaType, bytes, ct);
        }
        public Task<ReadOnlyMemory<byte>> ReadVerifiedAsync(string realm, ImmutableContentRef reference,
            CancellationToken ct = default) => inner.ReadVerifiedAsync(realm, reference, ct);
    }
}
