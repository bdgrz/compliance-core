using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Commands;
using Bdgrz.Compliance.Features.Operations.Events;
namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Builds complete native operating facts over verified immutable content without appending them, for tests of
///     reactors and projections that consume facts. The frozen response references repeat the content reference.
/// </summary>
static class ControlOperatingEvents
{
    public static async Task<ControlOperatingPlanProposed> ProposedAsync(IImmutableContentStore content,
        ControlOperatingPlanView plan)
    {
        var reference = await ControlOperatingContent.WriteAsync(content, plan.TenantId,
            ControlOperatingContent.PlanSchema, plan, CancellationToken.None);
        return new ControlOperatingPlanProposed(plan.TenantId, plan.ProgramId, plan.ControlId, plan.Revision,
            reference, Provenance(plan.TenantId, plan.ProgramId, plan.ControlId, plan.PlanVersionId,
                "ProposeControlOperatingPlan", plan.ProposedBy), reference, null);
    }

    public static async Task<ControlOperatingPlanApproved> ApprovedAsync(IImmutableContentStore content,
        ControlOperatingPlanView plan, long revision, Uuid approverMemberId, DateTimeOffset at,
        IReadOnlyList<OccurrenceReassignmentView> reassignments)
    {
        var moves = await ControlOperatingContent.WriteAsync(content, plan.TenantId,
            ControlOperatingContent.ReassignmentsSchema, reassignments, CancellationToken.None);
        var approver = ActorReference.ForMember(approverMemberId, "Approver");
        return new ControlOperatingPlanApproved(plan.TenantId, plan.ProgramId, plan.ControlId, revision,
            plan.PlanVersionId, approverMemberId, approver, at, "Independently approved.", null, moves,
            Provenance(plan.TenantId, plan.ProgramId, plan.ControlId, Uuid.CreateVersion4(),
                "ApproveControlOperatingPlan", approver), moves, null);
    }

    public static async Task<ControlOccurrenceOpened> OpenedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid controlId, ControlBusinessWindow window, ControlOperatingPlanView plan,
        string kind, DateOnly? periodStart, DateOnly? periodEnd, DateOnly? dueOn, OperatingHolder assignee,
        ActorReference openedBy, DateTimeOffset openedAt)
    {
        var occurrenceId = ControlOccurrenceIdentity.Of(tenantId, programId, controlId, window);
        var operation = OccurrenceProvenance(tenantId, programId, controlId, occurrenceId, "OpenControlOccurrence",
            openedBy);
        var intent = new ControlOccurrenceCreationIntent(new DurableIntentHeader(operation.RootOperationId,
                operation.Owner, operation.Owner, operation.Owner, openedBy, "initial", null,
                operation.AuthoredIntentSha256, openedAt), tenantId, programId, controlId, occurrenceId, window,
            plan.PlanVersionId, new NativeFactReference(new ControlOperatingPlan(tenantId, programId, controlId).Stream,
                Uuid.CreateVersion4(), 1), plan.Owner, plan.BackupOwner, plan.ReviewerMemberId, "test", openedAt);
        var reference = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOccurrenceCreationIntent.Schema, intent, CancellationToken.None);
        return new ControlOccurrenceOpened(tenantId, programId, controlId, occurrenceId, 1, kind,
            plan.ControlVersionId, plan.PlanVersionId, periodStart, periodEnd, dueOn, null, assignee, openedBy,
            openedAt, operation, window, reference, reference, reference, null);
    }

    public static async Task<ControlOccurrenceAttested> AttestedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid occurrenceId, long revision, ControlAttestationView attestation)
    {
        var reference = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOperatingContent.AttestationSchema, attestation, CancellationToken.None);
        return new ControlOccurrenceAttested(tenantId, programId, controlId, occurrenceId, revision, reference,
            OccurrenceProvenance(tenantId, programId, controlId, occurrenceId, "AttestControlOccurrence",
                attestation.RecordedBy), reference, null);
    }

    public static async Task<ControlOccurrenceReviewed> ReviewedAsync(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid controlId, Uuid occurrenceId, long revision, ControlOccurrenceReviewView review)
    {
        var reference = await ControlOperatingContent.WriteAsync(content, tenantId,
            ControlOperatingContent.ReviewSchema, review, CancellationToken.None);
        return new ControlOccurrenceReviewed(tenantId, programId, controlId, occurrenceId, revision, reference,
            OccurrenceProvenance(tenantId, programId, controlId, occurrenceId, "ReviewControlOccurrence",
                review.ReviewedBy), reference, null);
    }

    static OperationProvenance Provenance(Uuid tenantId, Uuid programId, Uuid controlId, Uuid logicalId,
        string kind, ActorReference actor)
    {
        var stream = new ControlOperatingPlan(tenantId, programId, controlId).Stream;
        return new OperationProvenance(logicalId, logicalId, null, kind, actor, stream, "initial", null,
            new string('a', 64), DateTimeOffset.UtcNow, stream, stream);
    }

    static OperationProvenance OccurrenceProvenance(Uuid tenantId, Uuid programId, Uuid controlId,
        Uuid occurrenceId, string kind, ActorReference actor)
    {
        var stream = new ControlOccurrence(tenantId, programId, controlId, occurrenceId).Stream;
        var logicalId = Uuid.CreateVersion4();
        return new OperationProvenance(logicalId, logicalId, null, kind, actor, stream, "initial", null,
            new string('a', 64), DateTimeOffset.UtcNow, stream, stream);
    }
}
