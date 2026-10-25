using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Seeds operating plan lines and occurrences straight onto their owner streams as complete native facts, with
///     verified immutable content and a frozen unit first response. It is a test seed for state that other features
///     read; the production path always admits these facts through the public commands.
/// </summary>
static class ControlOperatingSeed
{
    public static async Task<ControlOperatingPlanView> ProposeAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid controlVersionId, IReadOnlyList<string> expectedEvidence,
        Uuid planVersionId, OperatingHolder owner,
        OperatingHolder? backupOwner, Uuid reviewerMemberId, ControlCadence cadence, DateOnly effectiveFrom,
        string rationale, Uuid proposerMemberId, string proposerDisplay, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var plan = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOperatingPlan(tenantId, programId, controlId));
        var clean = ControlCadenceSchedule.Clean(cadence);
        var view = new ControlOperatingPlanView(tenantId, programId, controlId, planVersionId, plan.Revision + 1,
            ControlOperationStates.PendingApproval, controlVersionId, owner, backupOwner, reviewerMemberId, clean,
            ControlCadenceSchedule.Describe(clean), expectedEvidence.ToArray(),
            effectiveFrom, null, rationale, proposerMemberId,
            ActorReference.ForMember(proposerMemberId, proposerDisplay), at);
        var planContent = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOperatingContent.PlanSchema, view, CancellationToken.None);
        var (first, result) = await UnitAsync(content, tenantId);
        var provenance = ControlOperationsSource.Provenance(planVersionId, "ProposeControlOperatingPlan", plan.Stream,
            new OperationsActor(Uuid.Empty, proposerMemberId, proposerDisplay), plan.Revision, new string('a', 64), at);
        await CommitAsync(services, new ControlOperatingPlan(tenantId, programId, controlId, planVersionId), owner =>
            owner.Propose(provenance, planContent, first, result));
        return view;
    }

    public static async Task<ControlOperatingPlanView> ApproveAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid planVersionId, Uuid approverMemberId, string approverDisplay,
        DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var plan = await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOperatingPlan(tenantId, programId, controlId));
        var (first, result) = await UnitAsync(content, tenantId);
        var moves = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOperatingContent.ReassignmentsSchema, (IReadOnlyList<OccurrenceReassignmentView>)[],
            CancellationToken.None);
        var commandId = Uuid.CreateVersion4();
        var provenance = ControlOperationsSource.Provenance(commandId, "ApproveControlOperatingPlan", plan.Stream,
            new OperationsActor(Uuid.Empty, approverMemberId, approverDisplay), plan.Revision, new string('b', 64), at);
        await CommitAsync(services, new ControlOperatingPlan(tenantId, programId, controlId, commandId), owner =>
            owner.Approve(provenance, planVersionId, approverMemberId,
                ActorReference.ForMember(approverMemberId, approverDisplay), at, "Independently approved.", null,
                moves, first, result));
        var line = await ControlOperatingPlanLine.ReadAsync(await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOperatingPlan(tenantId, programId, controlId)), services.GetRequiredService<IDomainEventReader>(), content, tenantId,
            CancellationToken.None);
        return line.Find(planVersionId)!;
    }

    /// <summary>The approved plan in force on the control's line.</summary>
    public static async Task<ControlOperatingPlanView> CurrentPlanAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid controlId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var line = await ControlOperatingPlanLine.ReadAsync(await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOperatingPlan(tenantId, programId, controlId)),
            services.GetRequiredService<IDomainEventReader>(), services.GetRequiredService<IImmutableContentStore>(), tenantId, CancellationToken.None);
        return line.Current ?? throw new InvalidOperationException("The control has no approved plan.");
    }

    /// <summary>Opens an event-driven or ad hoc occurrence of the plan on its own window and returns its view.</summary>
    public static async Task<ControlOccurrenceView> OpenAsync(IServiceProvider provider, Uuid tenantId,
        Uuid programId, Uuid controlId, ControlOperatingPlanView plan, ControlBusinessWindow window, string trigger,
        DateOnly occurredOn, Uuid actorMemberId, string actorDisplay, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var occurrenceId = ControlOccurrenceIdentity.Of(tenantId, programId, controlId, window);
        var occurrence = new ControlOccurrence(tenantId, programId, controlId, occurrenceId);
        var planStream = new ControlOperatingPlan(tenantId, programId, controlId).Stream;
        var line = await ControlOperatingPlanLine.ReadAsync(await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOperatingPlan(tenantId, programId, controlId)), services.GetRequiredService<IDomainEventReader>(), content, tenantId,
            CancellationToken.None);
        var commandId = Uuid.CreateVersion4();
        var actor = new OperationsActor(Uuid.Empty, actorMemberId, actorDisplay);
        var provenance = ControlOperationsSource.Provenance(commandId, "OpenControlOccurrence", occurrence.Stream,
            actor, 0, new string('c', 64), at);
        var intent = new ControlOccurrenceCreationIntent(new DurableIntentHeader(commandId, occurrence.Stream,
                occurrence.Stream, occurrence.Stream, provenance.Actor, provenance.Episode, 0,
                provenance.AuthoredIntentSha256, at), tenantId, programId, controlId, occurrenceId, window,
            plan.PlanVersionId, NativeFactReference.Of(planStream, line.Approvals[plan.PlanVersionId]), plan.Owner,
            plan.BackupOwner, plan.ReviewerMemberId, trigger, at);
        var intentContent = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOccurrenceCreationIntent.Schema, intent, CancellationToken.None);
        var (first, result) = await UnitAsync(content, tenantId);
        var terms = new ControlOccurrenceTerms(plan.Cadence.Kind, plan.ControlVersionId, plan.PlanVersionId, occurredOn,
            occurredOn, occurredOn.AddDays(plan.Cadence.DueWithinDays ?? 0), trigger, plan.Owner, provenance.Actor, at,
            window);
        await CommitAsync(services, new ControlOccurrence(tenantId, programId, controlId, occurrenceId, commandId),
            owner => owner.Open(provenance, terms, intentContent, first, result));
        return (await ControlOccurrenceFold.ReadAsync(await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOccurrence(tenantId, programId, controlId, occurrenceId)), services.GetRequiredService<IDomainEventReader>(), content, tenantId,
            CancellationToken.None))!;
    }

    /// <summary>Records an attestation on a materialized occurrence and returns the occurrence view.</summary>
    public static async Task<ControlOccurrenceView> AttestAsync(IServiceProvider provider, ControlOccurrenceView current,
        ControlOperatingPlanView plan, AttestationInput input, Uuid recorderMemberId, string recorderDisplay,
        DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var occurrence = new ControlOccurrence(current.TenantId, current.ProgramId, current.ControlId,
            current.OccurrenceId);
        var attestation = ControlOperationRules.BuildAttestation(current.OccurrenceId, current.Attestations.Count + 1,
            input, plan, current.PeriodStart, current.PeriodEnd, null, null, recorderMemberId, recorderDisplay, at);
        var attestationContent = await ControlOperatingContent.WriteAsync(content, current.TenantId,
            ControlOperatingContent.AttestationSchema, attestation, CancellationToken.None);
        var (first, result) = await UnitAsync(content, current.TenantId);
        var commandId = Uuid.CreateVersion4();
        var provenance = ControlOperationsSource.Provenance(commandId, "AttestControlOccurrence", occurrence.Stream,
            new OperationsActor(Uuid.Empty, recorderMemberId, recorderDisplay), current.Revision, new string('d', 64),
            at);
        await CommitAsync(services, new ControlOccurrence(current.TenantId, current.ProgramId, current.ControlId,
            current.OccurrenceId, commandId), owner => owner.Attest(provenance, attestationContent, first, result, null));
        return (await ControlOccurrenceFold.ReadAsync(await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOccurrence(current.TenantId, current.ProgramId, current.ControlId,
                current.OccurrenceId)), services.GetRequiredService<IDomainEventReader>(), content, current.TenantId, CancellationToken.None))!;
    }

    /// <summary>Records a review decision on the latest attestation and returns the occurrence view.</summary>
    public static async Task<ControlOccurrenceView> ReviewAsync(IServiceProvider provider, ControlOccurrenceView current,
        string outcome, IReadOnlyList<string> actions, Uuid reviewerMemberId, string reviewerDisplay, DateTimeOffset at)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var content = services.GetRequiredService<IImmutableContentStore>();
        var occurrence = new ControlOccurrence(current.TenantId, current.ProgramId, current.ControlId,
            current.OccurrenceId);
        var latest = current.Attestations[^1];
        var commandId = Uuid.CreateVersion4();
        var review = new ControlOccurrenceReviewView(commandId, latest.AttestationId, latest.Version, outcome,
            "Checked independently.", actions, reviewerMemberId, ActorReference.ForMember(reviewerMemberId,
                reviewerDisplay), at);
        var reviewContent = await ControlOperatingContent.WriteAsync(content, current.TenantId,
            ControlOperatingContent.ReviewSchema, review, CancellationToken.None);
        var (first, result) = await UnitAsync(content, current.TenantId);
        var provenance = ControlOperationsSource.Provenance(commandId, "ReviewControlOccurrence", occurrence.Stream,
            new OperationsActor(Uuid.Empty, reviewerMemberId, reviewerDisplay), current.Revision, new string('e', 64),
            at);
        await CommitAsync(services, new ControlOccurrence(current.TenantId, current.ProgramId, current.ControlId,
            current.OccurrenceId, commandId), owner => owner.Review(provenance, reviewContent, first, result));
        return (await ControlOccurrenceFold.ReadAsync(await services.GetRequiredService<IAggregateReader>()
            .HydrateAsync(new ControlOccurrence(current.TenantId, current.ProgramId, current.ControlId,
                current.OccurrenceId)), services.GetRequiredService<IDomainEventReader>(), content, current.TenantId, CancellationToken.None))!;
    }

    static async Task<(ImmutableContentRef First, ImmutableContentRef Result)> UnitAsync(IImmutableContentStore content,
        Uuid tenantId)
    {
        var unit = await ControlOperatingContent.UnitResponseAsync(content, tenantId, CancellationToken.None);
        var body = ((StandardResponseBody)unit.Body).Content;
        return (await FirstResponses.FreezeAsync(content, tenantId.ToString(), unit),
            await ControlOperatingContent.WriteResultAsync(content, tenantId, UnitResult.Schema, body,
                CancellationToken.None));
    }

    static async Task CommitAsync<T>(IServiceProvider services, T aggregate, Func<T, Result> operation)
        where T : Aggregate
    {
        var committed = await services.GetRequiredService<IAggregateExecutor>().ExecuteAsync(aggregate,
            owner => AggregateOutcome.Commit(operation(owner)), new RequestDispatchContext(RequestActor.System),
            CancellationToken.None);
        Assert.True(committed.IsSuccess, committed.Error?.Message);
    }
}
