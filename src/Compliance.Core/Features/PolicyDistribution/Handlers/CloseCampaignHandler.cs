using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Policies.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.PolicyDistribution.Handlers;

/// <summary>
///     Accepts the closure of a campaign. The acceptance is the root native fact of the campaign owner: it seals result
///     admission on the open generation and freezes the complete admitted participant results and the exact
///     participant uses to hold, before the fixed 202 acknowledgment is frozen. <c>PolicyCampaignClosureProcess</c>
///     then drains the admitted results, holds the participants, freezes the totals and settles the uses. An exact
///     retry replays the original acknowledgment even after the closure completed.
/// </summary>
sealed class CloseCampaignHandler(NativeOperationExecutor operations, CampaignSource campaigns,
    IImmutableContentStore content, TimeProvider clock) : IRequestHandler<CloseCampaign, OperationAcceptedResponse>
{
    const string CommandKind = "CloseCampaign";

    public ValueTask<Result<OperationAcceptedResponse>> HandleAsync(IRequestContext<CloseCampaign> context,
        CancellationToken ct) =>
        PolicyCampaignFailures.ClassifyAsync(() => HandleCoreAsync(context, ct), ct);

    async ValueTask<Result<OperationAcceptedResponse>> HandleCoreAsync(IRequestContext<CloseCampaign> context,
        CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "A closure requires a command id and a non-negative expected revision.");
        var actor = PolicyActor.From(context, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new PolicyDistributionCampaign(request.TenantId, request.CampaignId).Stream;
        var provenance = PolicyCampaignContent.Provenance(request.CommandId, CommandKind, stream,
            stream, actor.Reference, request.ExpectedRevision, AuthoredIntentDigest.Compute(request), now);
        var binding = AcceptedOperationBinding.Of(provenance);

        ImmutableContentRef? intent = null;
        PolicyCampaignPreparedCut? preparedCut = null;
        var outcome = await operations.ExecuteAsync(new NativeOperation<PolicyDistributionCampaign>(binding,
            () => new PolicyDistributionCampaign(request.TenantId, request.CampaignId, request.CommandId),
            owner => owner.Acceptance, async (owner, token) =>
            {
                preparedCut = PolicyCampaignPreparedCut.Capture(owner);
                if (!owner.IsLaunched || owner.ProgramId != request.ProgramId)
                    return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.NotFound, "The campaign was not found."));
                var source = await campaigns.StateOfAsync(owner, token).ConfigureAwait(false);
                if (!source.IsSuccess)
                    return Result<FirstResponseDescriptor>.Failure(source.Error!);
                if (CampaignRules.CheckClosure(source.Value, request) is { } failure)
                    return Result<FirstResponseDescriptor>.Failure(failure.ToRequestError());
                intent = await FreezeAsync(source.Value, request, provenance, now, token).ConfigureAwait(false);
                var response = await PolicyCampaignContent.AcceptedResponseAsync(content, request.TenantId, request.CommandId,
                    now, token).ConfigureAwait(false);
                var prepared = owner.PrepareOpenAdmission(request.ExpectedRevision);
                return prepared.IsSuccess ? Result<FirstResponseDescriptor>.Success(response)
                    : Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            },
            (PolicyDistributionCampaign owner, ImmutableContentRef frozen) =>
            {
                var guarded = owner.PrepareOpenAdmission(request.ExpectedRevision);
                if (!guarded.IsSuccess)
                    return guarded;
                return preparedCut is null || !preparedCut.Matches(owner)
                    ? PolicyCampaignPreparedCut.Changed()
                    : owner.AcceptClosure(provenance, intent!, frozen);
            }), context, ct).ConfigureAwait(false);
        return await PolicyCampaignContent.AcknowledgmentAsync(content, request.TenantId, outcome, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    ///     Freezes the complete admitted results and, per participant that has any, the exact use to hold: the number of
    ///     results the participant must have applied and the admission of the last one.
    /// </summary>
    internal async Task<ImmutableContentRef> FreezeAsync(CampaignState state, CloseCampaign request,
        OperationProvenance provenance, DateTimeOffset now, CancellationToken ct)
    {
        var campaign = state.Campaign;
        var tenantId = campaign.TenantId;
        var streams = state.Roster.Order.ToDictionary(
            member => PolicyCampaignParticipant.StreamOf(tenantId, campaign.Id, member.PersonId),
            member => member.PersonId);
        var admitted = campaign.Admitted.Select(fact => new CampaignAdmittedResult(fact.Operation.RootOperationId,
            streams[fact.Operation.AffectedOwner], PolicyDistributionCampaign.KindOf(fact),
            NativeFactReference.Of(campaign.Stream, fact))).ToArray();
        var requirements = new List<SourceUseRequirement>();
        foreach (var group in admitted.GroupBy(static result => result.PersonId))
        {
            var stream = PolicyCampaignParticipant.StreamOf(tenantId, campaign.Id, group.Key);
            var results = group.ToArray();
            var required = await PolicyCampaignContent.WriteAsync(content, tenantId,
                PolicyCampaignContent.AdmittedResultsSchema, (IReadOnlyList<CampaignAdmittedResult>)results, ct)
                .ConfigureAwait(false);
            requirements.Add(new SourceUseRequirement(stream, results[^1].Admission, (ulong)results.Length,
                SourceUsePurpose.CampaignClosure, required));
        }

        requirements.Sort(static (left, right) => StreamKeyOrder.Instance.Compare(left.SourceOwner.Resource,
            right.SourceOwner.Resource));
        var children = await PolicyCampaignContent.WriteManifestAsync(content, tenantId,
            PolicyCampaignContent.AdmittedResultSchema, admitted, SelectionOrder.Authored, ct).ConfigureAwait(false);
        var uses = await PolicyCampaignContent.WriteManifestAsync(content, tenantId, SourceUseRequirement.Schema,
            requirements, SelectionOrder.CanonicalKey, ct).ConfigureAwait(false);
        var decision = await PolicyCampaignContent.WriteAsync(content, tenantId,
            PolicyCampaignContent.CloseCommandSchema, request, ct).ConfigureAwait(false);
        var header = PolicyCampaignContent.HeaderOf(provenance, campaign.Stream, campaign.Stream, now);
        return await PolicyCampaignContent.WriteAsync(content, tenantId, ClosureIntent.Schema,
            new ClosureIntent(header, ClosureParentKind.PolicyCampaign, campaign.Id, (ulong)campaign.Generation,
                children, decision, uses, null), ct).ConfigureAwait(false);
    }

    static Result<OperationAcceptedResponse> Failure(RequestErrorKind kind, string message) =>
        Result<OperationAcceptedResponse>.Failure(new RequestError(kind, message));
}
