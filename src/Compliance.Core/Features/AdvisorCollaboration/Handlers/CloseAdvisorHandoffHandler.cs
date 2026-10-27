using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Services;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AdvisorCollaboration.Handlers;

/// <summary>
///     Closes an open handoff at the expected handoff revision. Closure and feedback admission share the handoff
///     stream: feedback admitted before the closure stays admitted and is still materialized, and feedback racing the
///     closure is refused by the stream's revision.
/// </summary>
public sealed class CloseAdvisorHandoffHandler(AdvisorUnitOperation operation, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<CloseAdvisorHandoff>
{
    const string CommandKind = "CloseAdvisorHandoff";

    public ValueTask<Result> HandleAsync(IRequestContext<CloseAdvisorHandoff> context, CancellationToken ct) =>
        AdvisorFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    ValueTask<Result> HandleCoreAsync(IRequestContext<CloseAdvisorHandoff> context, CancellationToken ct)
    {
        var request = context.Request;
        if (!AdvisorHandoffCommandActor.TryGet(context, request.TenantId, out var actor))
            return ValueTask.FromResult(
                AdvisorHandoffRules.Forbidden("Closing an advisor handoff requires a personal HTTP actor."));
        if (request.CommandId == Uuid.Empty || request.ProgramId == Uuid.Empty || request.HandoffId == Uuid.Empty ||
            request.ExpectedRevision < 0)
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid(
                "Closing an advisor handoff requires command, program and handoff identities and a non-negative expected revision."));
        if (string.IsNullOrWhiteSpace(request.Rationale))
            return ValueTask.FromResult(AdvisorHandoffRules.Invalid("Ending a handoff requires a rationale and actor."));

        var now = clock.GetUtcNow();
        var canonical = request with { Rationale = request.Rationale.Trim() };
        var owner = new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId).Stream;
        var provenance = AdvisorContent.Provenance(request.CommandId, CommandKind, owner, actor,
            (ulong)request.ExpectedRevision, AuthoredIntentDigest.Compute(canonical), now);
        ImmutableContentRef? authored = null;
        return operation.ExecuteAsync<AdvisorHandoff>(context, request.TenantId, provenance,
            () => new AdvisorHandoff(request.TenantId, request.ProgramId, request.HandoffId, request.CommandId),
            handoff => handoff.Acceptance,
            async (_, token) =>
            {
                authored = await AdvisorAuthored.WriteAsync(content, request.TenantId, canonical, token).ConfigureAwait(false);
                return Result.Success;
            },
            handoff => AdvisorHandoffRules.InProgram(handoff, request.ProgramId)
                ? handoff.PrepareEnd(request.ExpectedRevision)
                : AdvisorHandoffRules.NotFound(),
            (handoff, frozen, result) => handoff.Close(provenance, authored!, actor, now, frozen, result), ct);
    }
}
