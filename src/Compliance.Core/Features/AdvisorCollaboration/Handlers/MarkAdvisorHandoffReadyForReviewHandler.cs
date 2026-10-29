using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Opens exact selected handoff versions to the assigned advisor or scoped guest at the expected handoff revision.
///     Every selected source must still be current. The expected revision is a fact of the handoff stream, so the
///     selection that was checked is exactly the selection that is shared.
/// </summary>
public sealed class MarkAdvisorHandoffReadyForReviewHandler(AdvisorUnitOperation operation,
    AdvisorHandoffReader handoffs, IAdvisorRecordVersionResolver recordVersions, TimeProvider clock)
    : IRequestHandler<MarkAdvisorHandoffReadyForReview>
{
    const string CommandKind = "MarkAdvisorHandoffReadyForReview";

    public ValueTask<Result> HandleAsync(IRequestContext<MarkAdvisorHandoffReadyForReview> context,
        CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result> HandleCoreAsync(IRequestContext<MarkAdvisorHandoffReadyForReview> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return AdvisorHandoffRules.Forbidden("Sharing an advisor handoff requires a personal HTTP actor.");
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return AdvisorHandoffRules.Invalid(
                "Sharing an advisor handoff requires command, program and handoff identities and a non-negative expected revision.");

        var now = clock.GetUtcNow();
        var owner = new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);

        return await operation.ExecuteAsync<AdvisorHandoff>(context, request.TenantId, provenance,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.CommandId),
            handoff => handoff.Acceptance,
            async (hydrated, token) =>
            {
                if (!AdvisorHandoffRules.InProgram(hydrated, request.ProgramId))
                    return AdvisorHandoffRules.NotFound();
                // A stale revision or another state is reported by the owner guard.
                if (hydrated.Revision != request.ExpectedRevision || !hydrated.IsOpen)
                    return Result.Success;
                var current = await handoffs.ReadAsync(hydrated, request.TenantId, token).ConfigureAwait(false);
                return await recordVersions.RequireCurrentAsync(request.TenantId, request.ProgramId, current.Selection,
                    token).ConfigureAwait(false);
            },
            handoff => AdvisorHandoffRules.InProgram(handoff, request.ProgramId)
                ? handoff.PrepareReady(request.ExpectedRevision, DateOnly.FromDateTime(now.UtcDateTime))
                : AdvisorHandoffRules.NotFound(),
            (handoff, frozen, result) => handoff.MarkReady(provenance, actor, now, frozen, result), ct)
            .ConfigureAwait(false);
    }
}
