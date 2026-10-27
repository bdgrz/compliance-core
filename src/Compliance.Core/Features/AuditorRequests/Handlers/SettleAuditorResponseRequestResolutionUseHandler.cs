using Bdgrz.Compliance.Features.AuditorRequests;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Commands;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.SourceUse.Models;
using System.Globalization;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Processes;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests.Handlers;

/// <summary>
///     The auditor response pair of the generic settle receiver. The terminal fact is resolved from the request: a
///     resolved request requires <c>parent_closed</c>, which installs the resolved-generation fence, and a canceled
///     resolution requires <c>canceled</c>. A missing terminal fact is a transient wait, never an inferred cancellation.
///     The settled requirement is the one the resolution froze for this response at admission.
/// </summary>
sealed class SettleAuditorResponseRequestResolutionUseHandler(IAggregateReader reader, IAggregateExecutor executor,
    IImmutableContentStore content, TimeProvider clock, AuditorRequestResolutionVerifier verifier)
    : SettleSourceUseHandler<AuditorResponse, SettleAuditorResponseRequestResolutionUse>(reader, executor, content, clock)
{
    protected override AuditorResponse Open(SettleAuditorResponseRequestResolutionUse request, Uuid selectedCommandId) =>
        new(Uuid.Parse(request.Target.Realm, CultureInfo.InvariantCulture), request.ProgramId, request.ReceivingOwnerId,
            request.SourceOwnerId, selectedCommandId);

    protected override async ValueTask<Result<SourceUseSource>> VerifyAsync(
        SettleAuditorResponseRequestResolutionUse request, CancellationToken ct)
    {
        var resolution = await verifier.VerifyTerminalAsync(request.Source, request.Intent, request.OperationId,
            request.CommandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return Result<SourceUseSource>.Failure(resolution.Error);
        var (auditorRequest, intent, decision, tenant) = (resolution.Request!, resolution.Intent!, resolution.Decision!,
            resolution.TenantId);
        if (request.ReceivingOwnerId != intent.ParentId || request.ProgramId != decision.ProgramId ||
            request.Generation != (long)intent.ParentGeneration)
            return Fail("The settlement differs from the frozen resolution intent.");
        IReadOnlyList<SourceUseRequirement> uses;
        try
        {
            uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(Content, tenant.ToString(),
                intent.SourceUses, ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return Fail($"The frozen resolution source uses cannot be verified: {exception.Message}", true);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return Fail($"The frozen resolution source uses cannot be verified: {exception.Message}");
        }

        var response = Open(request, request.CommandId);
        if (uses.FirstOrDefault(use => use.SourceOwner == response.Stream) is not { } requirement)
            return Fail("The response is not a frozen source use of this resolution.");
        var disposition = auditorRequest.TerminalResolution(request.OperationId) switch
        {
            AuditorRequestResolved => SourceUseSettlementDisposition.ParentClosed,
            AuditorRequestResolutionCanceled => SourceUseSettlementDisposition.Canceled,
            _ => (SourceUseSettlementDisposition?)null,
        };
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, request.Intent,
            AuditorRequestResolutionEffects.UseKey(intent, response.Stream), requirement,
            nameof(SettleAuditorResponseRequestResolutionUse), disposition, null));
    }

    protected override Func<AuditorResponse, OperationProvenance, ImmutableContentRef, long,
        SourceUseSettlementDisposition, Result> SettleOperationWithDisposition() =>
        (AuditorResponse response, OperationProvenance provenance, ImmutableContentRef intent, long generation,
            SourceUseSettlementDisposition disposition) =>
            response.SettleResolutionUse(provenance, intent, generation, disposition);

    static Result<SourceUseSource> Fail(string message, bool transient = false) =>
        Result<SourceUseSource>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
