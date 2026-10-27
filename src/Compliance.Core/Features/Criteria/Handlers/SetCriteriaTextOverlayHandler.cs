using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Criteria.Contracts;
using Bdgrz.Compliance.Features.Criteria.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Criteria.Aggregates;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Criteria.Handlers;

/// <summary>
///     Sets or revises one criterion's licensed text overlay at its own revision. The complete supplied content is
///     written as immutable content before one revision reference is appended, and the registration is frozen as the
///     first response, so an exact retry returns the original revision even after later edits and a changed intent
///     under the same command id conflicts.
/// </summary>
public sealed class SetCriteriaTextOverlayHandler(NativeOperationExecutor operations, ICriteriaCatalog catalog, IImmutableContentStore content, TimeProvider clock)
    : IRequestHandler<SetCriteriaTextOverlay, CriteriaTextOverlayRegistration>
{
    const string CommandKind = "SetCriteriaTextOverlay";

    public async ValueTask<Result<CriteriaTextOverlayRegistration>> HandleAsync(
        IRequestContext<SetCriteriaTextOverlay> context, CancellationToken ct)
    {
        var request = context.Request;
        if (request.CommandId == Uuid.Empty || request.ExpectedRevision < 0)
            return Failure(RequestErrorKind.Validation,
                "An overlay requires a command id and a non-negative expected revision.");
        if (catalog.GetEntry(request.EditionId, request.Identifier) is not { } entry)
            return Failure(RequestErrorKind.NotFound, "The criterion was not found.");

        var actor = OperationsActor.From(context.Actor, request.TenantId);
        var now = clock.GetUtcNow();
        var stream = new CriteriaTextOverlay(request.TenantId, request.EditionId, entry.Identifier).Stream;
        var provenance = RootOperation.Provenance(request.CommandId, CommandKind, stream,
            ActorReference.ForMember(actor.MemberId, actor.Display), request.ExpectedRevision,
            AuthoredIntentDigest.Compute(request), now) with
        { FirstOwner = stream };
        var binding = AcceptedOperationBinding.Of(provenance);
        ImmutableContentRef? overlay = null;
        ImmutableContentRef? result = null;

        async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(CriteriaTextOverlay overlayLine,
            CancellationToken token)
        {
            if (CriteriaTextOverlayRules.Clean(request.Content) is not { } cleaned)
                return Result<FirstResponseDescriptor>.Failure(new RequestError(RequestErrorKind.Validation,
                    "An overlay requires bounded supplied text, supplier, license and member attribution."));
            var prepared = overlayLine.PrepareSet(request.ExpectedRevision);
            if (!prepared.IsSuccess)
                return Result<FirstResponseDescriptor>.Failure(prepared.Error!);
            overlay = await NativeContent.WriteAsync(content, request.TenantId, CriteriaTextOverlay.ContentSchema,
                cleaned, token).ConfigureAwait(false);
            var frozen = await NativeContent.FreezeAsync(content, request.TenantId,
                CriteriaTextOverlay.RegistrationSchema, new CriteriaTextOverlayRegistration(request.EditionId,
                    request.Identifier, request.ExpectedRevision + 1), token).ConfigureAwait(false);
            result = frozen.Result;
            return Result<FirstResponseDescriptor>.Success(frozen.Response);
        }

        var outcome = await operations.ExecuteAsync(new NativeOperation<CriteriaTextOverlay>(binding,
            () => new CriteriaTextOverlay(request.TenantId, request.EditionId, entry.Identifier, request.CommandId),
            overlayLine => overlayLine.Acceptance, PrepareAsync,
            (CriteriaTextOverlay overlayLine, ImmutableContentRef frozen) => overlayLine.Set(provenance, overlay!,
                frozen, result!)), context, ct).ConfigureAwait(false);
        return await NativeContent.ResponseAsync<CriteriaTextOverlayRegistration>(content, request.TenantId, outcome,
            ct).ConfigureAwait(false);
    }

    static Result<CriteriaTextOverlayRegistration> Failure(RequestErrorKind kind, string message) =>
        Result<CriteriaTextOverlayRegistration>.Failure(new RequestError(kind, message));
}
