using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.History;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Processes;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Seeds real native facts, canonical authored bytes and exact selected owner cuts for read fixtures.</summary>
static class AdvisorSeed
{
    public static async Task HandoffAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        IReadOnlyList<AdvisorRecordVersion> selection, ActorReference lead, DateTimeOffset createdAt,
        DateOnly expiresOn, bool ready = true, string purpose = "Review", Uuid? comparedTo = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var request = new CreateAdvisorHandoff(tenantId, programId, handoffId, selection, purpose, expiresOn, comparedTo);
        var owner = new AdvisorHandoff(tenantId, programId, handoffId, handoffId);
        var operation = Root(owner, handoffId, nameof(CreateAdvisorHandoff), lead, null, request, createdAt);
        var manifest = await AdvisorContent.WriteSelectionAsync(content, tenantId, AdvisorContent.CreatedSelectionSchema,
            selection, CancellationToken.None);
        var authored = await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None);
        var (response, result) = await UnitAsync(content, tenantId);
        Assert.True(owner.Create(operation, manifest, authored, expiresOn, lead, createdAt, comparedTo, response, result).IsSuccess);
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        if (ready)
            await ReadyAsync(provider, tenantId, programId, handoffId, lead, createdAt.AddMinutes(1));
    }

    public static async Task ReadyAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        ActorReference lead, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var id = Uuid.CreateVersion4();
        var owner = await SelectedAsync(services, new AdvisorHandoff(tenantId, programId, handoffId, id));
        var request = new MarkAdvisorHandoffReadyForReview(id, tenantId, programId, handoffId, owner.Revision);
        var operation = Root(owner, id, nameof(MarkAdvisorHandoffReadyForReview), lead, (ulong)owner.Revision, request, at);
        var (response, result) = await UnitAsync(services.GetRequiredService<IImmutableContentStore>(), tenantId);
        Assert.True(owner.MarkReady(operation, lead, at, response, result).IsSuccess);
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
    }

    public static async Task EndAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        ActorReference lead, DateTimeOffset at, bool revoke = false)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var id = Uuid.CreateVersion4();
        var owner = await SelectedAsync(services, new AdvisorHandoff(tenantId, programId, handoffId, id));
        var (response, result) = await UnitAsync(content, tenantId);
        Result applied;
        if (revoke)
        {
            var request = new RevokeAdvisorHandoff(id, tenantId, programId, handoffId, "Access removed", owner.Revision);
            var operation = Root(owner, id, nameof(RevokeAdvisorHandoff), lead, (ulong)owner.Revision, request, at);
            applied = owner.Revoke(operation, await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None),
                lead, at, response, result);
        }
        else
        {
            var request = new CloseAdvisorHandoff(id, tenantId, programId, handoffId, "Review complete", owner.Revision);
            var operation = Root(owner, id, nameof(CloseAdvisorHandoff), lead, (ulong)owner.Revision, request, at);
            applied = owner.Close(operation, await AdvisorAuthored.WriteAsync(content, tenantId, request, CancellationToken.None),
                lead, at, response, result);
        }
        Assert.True(applied.IsSuccess, applied.Error?.Message);
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
    }

    public static async Task FeedbackAsync(IServiceProvider provider, Uuid tenantId, Uuid programId, Uuid handoffId,
        Uuid feedbackId, AdvisorRecordVersion source, string kind, string body, ActorReference author,
        DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var owner = await SelectedAsync(services, new AdvisorHandoff(tenantId, programId, handoffId, feedbackId));
        var authored = new RecordAdvisorFeedback(tenantId, programId, handoffId, feedbackId, source.RecordId,
            source.VersionId, kind, body, owner.Revision)
        { SelectedSource = source.IsSupported ? source : null };
        var operation = Root(owner, feedbackId, nameof(RecordAdvisorFeedback), author, (ulong)owner.Revision, authored, at);
        var header = AdvisorContent.Header(operation, at);
        var authoredRef = await AdvisorAuthored.WriteAsync(content, tenantId, authored, CancellationToken.None);
        var proofs = await AdvisorContent.EmptyProofsAsync(content, tenantId, feedbackId, CancellationToken.None);
        var intent = await AdvisorContent.WriteAsync(content, tenantId, ChildAdmissionIntent.Schema,
            new ChildAdmissionIntent(header, handoffId, (ulong)owner.Generation, AdmittedChildKind.AdvisorFeedback,
                feedbackId, authoredRef, proofs, (ulong)owner.Revision + 1), CancellationToken.None);
        var response = await AdvisorContent.AcceptedResponseAsync(content, tenantId, feedbackId,
            "/api/v1/operations/{operation_id}", at, CancellationToken.None);
        var first = await FirstResponses.FreezeAsync(content, tenantId.ToString(), response);
        Assert.True(owner.AdmitFeedback(operation, intent, DateOnly.FromDateTime(at.UtcDateTime), first).IsSuccess);
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(owner, new RequestDispatchContext(RequestActor.System));
        var saved = await SelectedAsync(services, new AdvisorHandoff(tenantId, programId, handoffId, feedbackId));
        var admitted = Assert.IsType<AdvisorFeedbackAdmitted>(saved.Acceptance.SelectedFact);
        var commandId = Uuid.CreateVersion4();
        var thread = new AdvisorFeedbackThread(tenantId, programId, handoffId, feedbackId, commandId);
        var materialization = SourceUseReceiverSupport.Child(commandId, nameof(MaterializeAdvisorFeedback),
            admitted.Metadata.EventId, header, thread.Stream, at);
        Assert.True(thread.Materialize(materialization, authored, author, at, intent).IsSuccess);
        await services.GetRequiredService<IAggregateWriter>().SaveAsync(thread, new RequestDispatchContext(RequestActor.System));
    }

    static async Task<T> SelectedAsync<T>(IServiceProvider services, T owner) where T : Aggregate
    {
        var current = await services.GetRequiredService<IAggregateReader>().HydrateAsync(owner);
        _ = await AdvisorNativeHistory.ReadAsync(current, services.GetRequiredService<IDomainEventReader>(), CancellationToken.None);
        return current;
    }

    static OperationProvenance Root<T>(Aggregate owner, Uuid id, string kind, ActorReference actor, ulong? expected,
        T request, DateTimeOffset at) where T : notnull => AdvisorContent.Provenance(id, kind, owner.Stream, actor,
        expected, AuthoredIntentDigest.Compute(request), at);

    static async Task<(ImmutableContentRef Response, ImmutableContentRef Result)> UnitAsync(IImmutableContentStore content,
        Uuid tenantId)
    {
        var (response, result) = await AdvisorContent.FreezeUnitAsync(content, tenantId, CancellationToken.None);
        return (await FirstResponses.FreezeAsync(content, tenantId.ToString(), response), result);
    }
}
