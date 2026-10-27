using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

/// <summary>Derives the held disposition proofs and drain checks of a resolution from the authoritative child owners.</summary>
static class AuditorRequestResolutionProofs
{
    /// <summary>The frozen admitted-child manifest of a resolution.</summary>
    public static async ValueTask<IReadOnlyList<AdmittedRequestChild>> ChildrenAsync(IImmutableContentStore content,
        Uuid tenant, ClosureIntent intent, CancellationToken ct) =>
        await ImmutableSelectionManifests.ReadAsync<AdmittedRequestChild>(content, tenant.ToString(),
            intent.AdmittedChildren, ct).ConfigureAwait(false);

    /// <summary>
    ///     Confirms that every admitted comment and sample selection of the frozen manifest is materialized: a resolution
    ///     drains these before it acquires its responses, and a later close never discards them. An unmaterialized child
    ///     is a wait, not a failure.
    /// </summary>
    public static async ValueTask<Result> DrainedAsync(IAggregateReader reader, IReadOnlyList<AdmittedRequestChild> children,
        Uuid tenant, Uuid programId, Uuid requestId, CancellationToken ct)
    {
        foreach (var child in children)
        {
            switch (child.Kind)
            {
                case AdmittedRequestChildKind.Comment:
                    var thread = await reader.HydrateAsync(new AuditorFeedbackThread(tenant, programId, requestId,
                        child.ChildId), ct).ConfigureAwait(false);
                    if (!thread.IsMaterialized)
                        return Waiting("An admitted comment is not materialized yet.");
                    break;
                case AdmittedRequestChildKind.SampleSelection:
                    var selection = await reader.HydrateAsync(new AuditorSampleSelection(tenant, programId, requestId,
                        child.ChildId), ct).ConfigureAwait(false);
                    if (!selection.IsMaterialized)
                        return Waiting("An admitted sample selection is not materialized yet.");
                    break;
            }
        }

        return Result.Success;
    }

    /// <summary>
    ///     The held accepted disposition of every admitted response in canonical key order, or the reason one is not held.
    ///     A grant that is not visible yet is transient; a response that is held without an accepted disposition for its
    ///     delivered version is permanent.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<ResponseDispositionProof>>> HeldAsync(IAggregateReader reader,
        IReadOnlyList<AdmittedRequestChild> children, ClosureIntent intent, AuditorResolutionDecision decision,
        Uuid tenant, Uuid requestId, Uuid commandId, CancellationToken ct)
    {
        var proofs = new List<ResponseDispositionProof>();
        foreach (var child in children.Where(static child => child.Kind == AdmittedRequestChildKind.Response)
                     .OrderBy(child => AuditorRequestResolutionEffects.ResponseKey(tenant, decision.ProgramId, requestId,
                         child.ChildId), Bdgrz.Compliance.Features.Kernel.Services.StreamKeyOrder.Instance))
        {
            var response = await reader.HydrateAsync(new AuditorResponse(tenant, decision.ProgramId, requestId,
                child.ChildId, commandId).ForSourceUse(candidate => AuditorRequestResolutionEffects.UseKey(intent, candidate.Stream)), ct).ConfigureAwait(false);
            if (response.Uses.Find(AuditorRequestResolutionEffects.UseKey(intent, response.Stream)) is not
                { GrantFact: { } grant, IsActive: true })
                return Failure("A response is not held by this resolution yet.", true);
            if (response.Disposition is not { Outcome: "accepted" } disposition || response.Delivery is null)
                return Failure("A held response has no accepted external disposition.", false);
            proofs.Add(new ResponseDispositionProof(child.ChildId, (int)disposition.Version,
                NativeFactReference.Of(response.Stream, disposition), NativeFactReference.Of(response.Stream, grant)));
        }

        return Result<IReadOnlyList<ResponseDispositionProof>>.Success(proofs);
    }

    static Result Waiting(string message) =>
        Result.Failure(new RequestError(RequestErrorKind.Conflict, message, true));

    static Result<IReadOnlyList<ResponseDispositionProof>> Failure(string message, bool transient) =>
        Result<IReadOnlyList<ResponseDispositionProof>>.Failure(
            new RequestError(RequestErrorKind.Conflict, message, transient));
}
