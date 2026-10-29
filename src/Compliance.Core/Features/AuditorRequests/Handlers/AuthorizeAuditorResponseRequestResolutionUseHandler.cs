using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Handlers;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     The auditor response pair of the generic acquire receiver: it holds the exact response a resolution pinned. A
///     response that is not materialized yet is a transient wait, so an accepted but unmaterialized response blocks the
///     resolution without ever failing it. A response without an accepted external disposition for its delivered version,
///     or a resolution that ended, aborts the resolution and fences every use.
/// </summary>
sealed class AuthorizeAuditorResponseRequestResolutionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, AuditorRequestResolutionVerifier verifier,
    AuditorRequestResolutionFences fences)
    : AuthorizeSourceUseHandler<AuditorResponse, AuthorizeAuditorResponseRequestResolutionUse>(reader, executor, content,
        clock)
{
    protected override AuditorResponse Open(AuthorizeAuditorResponseRequestResolutionUse request, Uuid selectedCommandId) =>
        new(Tenant(request.Target), request.ProgramId, request.ReceivingOwnerId, request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        AuthorizeAuditorResponseRequestResolutionUse request, CancellationToken ct)
    {
        var resolution = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return Result<SourceUseSource>.Failure(resolution.Error);
        var (auditorRequest, intent, decision, tenant) = (resolution.Request!, resolution.Intent!, resolution.Decision!,
            resolution.TenantId);
        IReadOnlyList<AdmittedRequestChild> children;
        try
        {
            children = await AuditorRequestResolutionProofs.ChildrenAsync(Content, tenant, intent, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Fail($"The admitted child manifest cannot be verified: {exception.Message}", true);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The admitted child manifest cannot be verified: {exception.Message}", false);
        }

        if (request.ReceivingOwnerId != intent.ParentId || request.ProgramId != decision.ProgramId ||
            request.Generation != (long)intent.ParentGeneration || children.All(child =>
                child.Kind != AdmittedRequestChildKind.Response || child.ChildId != request.SourceOwnerId))
            return Fail("The authorization differs from the frozen resolution response set.", false);

        var terminal = auditorRequest.TerminalResolution(request.OperationId);
        var superseded = terminal is AuditorRequestResolved ? "The resolution already resolved the request." : null;
        var response = await verifier.HydrateAsync(Open(request, request.CommandId), ct).ConfigureAwait(false);
        var key = AuditorRequestResolutionEffects.UseKey(intent, response.Stream);
        if (superseded is not null)
            return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key,
                Placeholder(response, intent), nameof(AuthorizeAuditorResponseRequestResolutionUse), null, superseded));
        if (!response.IsMaterialized)
            return Fail("The auditor response is not materialized yet.", true);
        if (response.CurrentContentFactById(request.SourceContentEventId) is not { } pinned)
            return Fail("The pinned auditor response fact does not exist.", false);
        if (resolution.History!.Responses.All(fact => fact.Operation.LogicalId != request.SourceOwnerId))
            return Fail("The response was not admitted by this request.", false);

        var disposition = response.Disposition;
        var refusal = terminal is AuditorRequestResolutionCanceled ? "The resolution was canceled."
            : response.Delivery is null || disposition is not { Outcome: "accepted" }
                ? "The response has no accepted external disposition for its delivered version."
                : null;
        var facts = new ResponseResolutionFacts(request.SourceOwnerId, (int)(response.Delivery?.Version ?? 0),
            disposition?.Outcome ?? string.Empty,
            disposition is null ? null : NativeFactReference.Of(response.Stream, disposition), refusal);
        var factsReference = await AuditorContent.WriteDeterministicAsync(Content, tenant, request.CommandId,
            AuditorContent.ResponseFactsSchema, facts, ct).ConfigureAwait(false);
        var requirement = new SourceUseRequirement(response.Stream, NativeFactReference.Of(response.Stream, pinned),
            request.ExpectedContentRevision, SourceUsePurpose.RequestResolution, factsReference);
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent, key, requirement,
            nameof(AuthorizeAuditorResponseRequestResolutionUse), null, null));
    }

    protected override async ValueTask<Result> VerifyPinnedAsync(AuditorResponse owner, SourceUseRequirement requirement,
        CancellationToken ct)
    {
        if (!owner.IsMaterialized)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The auditor response is not materialized yet.", true));
        if (!IsPinned(owner, requirement))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The auditor response changed after the resolution pinned it.", false));
        ResponseResolutionFacts facts;
        try
        {
            facts = await AuditorContent.ReadAsync<ResponseResolutionFacts>(Content,
                Uuid.Parse(owner.Stream.Realm, CultureInfo.InvariantCulture), requirement.RequiredFacts, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned response facts cannot be verified: {exception.Message}", true));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                $"The pinned response facts cannot be verified: {exception.Message}", false));
        }

        return facts.Refusal is null
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict, facts.Refusal, false));
    }

    protected override bool IsPinned(AuditorResponse owner, SourceUseRequirement requirement) =>
        owner.Latest is { } latest && latest.Metadata.EventId == requirement.ContentEvent.EventId &&
        (ulong)owner.Revision == requirement.ResourceRevision;

    protected override Func<AuditorResponse, OperationProvenance, ImmutableContentRef, long, Result> GrantOperation() =>
        (AuditorResponse response, OperationProvenance provenance, ImmutableContentRef intent, long generation) =>
            response.AuthorizeResolutionUse(provenance, intent, generation);

    protected override async ValueTask<Result<ReceiverOutcome>> RefuseAsync(
        AuthorizeAuditorResponseRequestResolutionUse request, SourceUseSource source, string reason,
        IExecutionContext context, CancellationToken ct)
    {
        var resolution = await verifier.VerifyAcceptedAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        return resolution.Error is not null
            ? Result<ReceiverOutcome>.Failure(resolution.Error)
            : await fences.AbortAsync(resolution, request.Source, request.Intent, reason, request.SourceOwnerId, context,
                ct).ConfigureAwait(false);
    }

    /// <summary>A superseded acquisition is a no-op, so its requirement is never pinned or granted.</summary>
    static SourceUseRequirement Placeholder(AuditorResponse response, ClosureIntent intent) =>
        new(response.Stream, new NativeFactReference(response.Stream, Uuid.Empty, 0), 0,
            SourceUsePurpose.RequestResolution, intent.AuthoredDecision);

    static Uuid Tenant(EventStreamAddress stream) => Uuid.Parse(stream.Realm, CultureInfo.InvariantCulture);

    static Result<SourceUseSource> Fail(string message, bool transient) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
