using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Authorizers;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Contracts;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
namespace Bdgrz.Compliance.Features.Remediation.Handlers;

/// <summary>Records a named analysis revision for the current owner, Compliance Lead or Contributor.</summary>
public sealed class ReviseFindingAnalysisHandler(NativeOperationExecutor operations, IAggregateReader reader,
    FindingReader findings, IPermissionAuthorizer permissions, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<ReviseFindingAnalysis, FindingView>
{
    public async ValueTask<Result<FindingView>> HandleAsync(IRequestContext<ReviseFindingAnalysis> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.TenantId == Uuid.Empty || request.ProgramId == Uuid.Empty ||
            request.FindingId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(new RequestError(RequestErrorKind.Validation, "Analysis requires exact finding identities and a non-negative revision."));
        if (FindingRules.ValidateAnalysis(request.Analysis, request.Severity, request.AffectedRecords) is { } invalid)
            return Failure(invalid.ToRequestError());
        var now = clock.GetUtcNow();
        var acting = await FindingActors.RequireCurrentAsync(context.Actor, request.TenantId, reader, now, ct).ConfigureAwait(false);
        if (!acting.IsSuccess)
            return Failure(acting.Error!);
        var actor = acting.Value;
        var authority = await reader.HydrateAsync(new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId), ct)
            .ConfigureAwait(false);
        if (!authority.IsRecorded)
            return Failure(new RequestError(RequestErrorKind.NotFound, "The finding was not found."));
        if (authority.OwnerMemberId != actor.MemberId && !await permissions.IsAllowedAsync(request.TenantId, actor.UserId,
                actor.MemberId, FindingAnalysisPermissions.Revise, ct).ConfigureAwait(false))
            return Failure(new RequestError(RequestErrorKind.Forbidden, "Only the owner, a Compliance Lead or Contributor may revise finding analysis."));
        var revisedBy = ActorReference.ForMember(actor.MemberId, actor.Display);
        var analysis = request.Analysis.Trim();
        var affected = FindingRules.Trimmed(request.AffectedRecords);
        var snapshot = new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId);
        var provenance = FindingContent.Provenance(request.CommandId, nameof(ReviseFindingAnalysis), snapshot.Stream,
            revisedBy, (ulong)request.ExpectedRevision, FindingCommands.Digest(request), now) with { FirstOwner = snapshot.Stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? result = null;
        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(FindingAggregate owner, CancellationToken token)
        {
            var prepared = owner.PrepareAnalysis(request.ExpectedRevision, analysis, request.Severity, affected);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            var current = await findings.ComposeAsync(owner, now, token).ConfigureAwait(false);
            var after = FindingViews.WithAnalysis(current!, analysis, request.Severity, affected, owner.Revision + 1, revisedBy, now);
            var frozen = await FindingContent.FreezeAsync(content, request.TenantId, FindingContent.ViewSchema, after, token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }
        var outcome = await operations.ExecuteAsync(new NativeOperation<FindingAggregate>(binding,
            () => new FindingAggregate(request.TenantId, request.ProgramId, request.FindingId, request.CommandId),
            owner => owner.Acceptance, PrepareAsync,
            (FindingAggregate owner, ImmutableContentRef first) => owner.ReviseAnalysis(provenance, analysis, request.Severity,
                affected, first, result!)), context, ct).ConfigureAwait(false);
        return await FindingContent.ResponseAsync<FindingView>(content, request.TenantId, outcome, ct).ConfigureAwait(false);
    }

    static Result<FindingView> Failure(RequestError error) => Result<FindingView>.Failure(error);
}
