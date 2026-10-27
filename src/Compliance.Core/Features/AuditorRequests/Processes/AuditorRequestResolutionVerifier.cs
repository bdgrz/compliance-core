using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.History;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
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
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Processes;

/// <summary>
///     Verifies a receiver's proven source: the frozen resolution intent and authored decision are read from verified
///     immutable content, and the accepted resolution fact (or the terminal fact that ended it) is found on the
///     authoritative request. The source reference only names the fact; nothing the caller states is trusted until the
///     request confirms it.
/// </summary>
sealed class AuditorRequestResolutionVerifier(IAggregateReader reader, IDomainEventReader events,
    IImmutableContentStore content)
{
    /// <summary>Hydrates an owner from the authoritative streams.</summary>
    public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct)
        where TAggregate : Aggregate => reader.HydrateAsync(aggregate, ct);

    /// <summary>Verifies the accepted resolution named by <paramref name="source" />.</summary>
    public async ValueTask<AuditorRequestResolution> VerifyAcceptedAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var resolution = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return resolution;
        var request = resolution.Request!;
        if (request.AcceptedResolution(operationId) is not { } accepted)
            return AuditorRequestResolution.Failed(Transient("The accepted resolution is not visible yet."));
        return source.Stream != request.Stream || accepted.Metadata.EventId != source.EventId ||
               accepted.Metadata.AggregateVersion != source.AggregateVersion || accepted.Intent != intentReference ||
               accepted.Generation != (long)resolution.Intent!.ParentGeneration
            ? AuditorRequestResolution.Failed(Permanent("The source is not the accepted resolution of this request."))
            : resolution;
    }

    /// <summary>Verifies the terminal fact (resolved or canceled) named by <paramref name="source" />.</summary>
    public async ValueTask<AuditorRequestResolution> VerifyTerminalAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        var resolution = await ReadAsync(source, intentReference, operationId, commandId, ct).ConfigureAwait(false);
        if (resolution.Error is not null)
            return resolution;
        var request = resolution.Request!;
        if (request.TerminalResolution(operationId) is not { } terminal)
            return AuditorRequestResolution.Failed(Transient("Neither the resolution nor its cancellation is visible yet."));
        var intent = terminal switch
        {
            AuditorRequestResolved resolved => resolved.Change,
            AuditorRequestResolutionCanceled canceled => canceled.Intent,
            _ => null,
        };
        return source.Stream != request.Stream || terminal.Metadata.EventId != source.EventId ||
               terminal.Metadata.AggregateVersion != source.AggregateVersion || intent != intentReference
            ? AuditorRequestResolution.Failed(Permanent("The source is not the terminal fact of this resolution."))
            : resolution;
    }

    async ValueTask<AuditorRequestResolution> ReadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid commandId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (!Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenant))
            return AuditorRequestResolution.Failed(Permanent("The resolution source is not a tenant request stream."));
        ClosureIntent intent;
        AuditorResolutionDecision decision;
        try
        {
            intent = await AuditorContent.ReadAsync<ClosureIntent>(content, tenant, intentReference, ct)
                .ConfigureAwait(false);
            decision = await AuditorContent.ReadAsync<AuditorResolutionDecision>(content, tenant, intent.AuthoredDecision,
                ct).ConfigureAwait(false);
        }
        catch (ImmutableContentUnavailableException exception)
        {
            return AuditorRequestResolution.Failed(Transient($"The frozen resolution cannot be verified: {exception.Message}"));
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return AuditorRequestResolution.Failed(Permanent($"The frozen resolution cannot be verified: {exception.Message}"));
        }

        if (intent.Header.RootOperationId != operationId || intent.ParentKind != ClosureParentKind.AuditorRequest ||
            intent.ParentId == Uuid.Empty || decision.ProgramId == Uuid.Empty || decision.DecisionId != operationId)
            return AuditorRequestResolution.Failed(Permanent("The command differs from the frozen resolution intent."));
        var request = await reader.HydrateAsync(new AuditorRequest(tenant, decision.ProgramId, intent.ParentId, commandId, operationId),
            ct).ConfigureAwait(false);
        try
        {
            var history = await AuditorRequestHistory.ReadAsync(events, request, ct).ConfigureAwait(false);
            var scoped = await AuditorCommands.TermsAsync(content, tenant, request, ct).ConfigureAwait(false);
            if (!scoped.IsSuccess)
                return AuditorRequestResolution.Failed(scoped.Error!);
            if (source.Stream != request.Stream || intent.Header.FirstOwner != request.Stream ||
                intent.Header.AffectedOwner != request.Stream || intent.Header.ExpectedRevisionOwner != request.Stream)
                return AuditorRequestResolution.Failed(Permanent("The resolution differs from its exact native first owner."));
            if (history.AcceptedResolution(operationId) is { } admission &&
                (admission.Intent != intentReference || admission.Generation != (long)intent.ParentGeneration ||
                intent.Header.OriginalActor != admission.Operation.Actor ||
                intent.Header.AuthoredIntentSha256 != admission.Operation.AuthoredIntentSha256 ||
                intent.Header.OriginalExpectedRevision != admission.Operation.OriginalExpectedRevision))
                return AuditorRequestResolution.Failed(Permanent("The resolution intent differs from its original native admission."));
            if (history.AcceptedResolution(operationId) is { } accepted)
            {
                var expected = history.Facts.TakeWhile(fact => fact.Metadata.EventId != accepted.Metadata.EventId)
                    .Select(fact => fact switch
                    {
                        AuditorResponseAdmitted response => new AdmittedRequestChild(response.Operation.LogicalId,
                            AdmittedRequestChildKind.Response, NativeFactReference.Of(request.Stream, response)),
                        AuditorCommentAdmitted comment => new AdmittedRequestChild(comment.Operation.LogicalId,
                            AdmittedRequestChildKind.Comment, NativeFactReference.Of(request.Stream, comment)),
                        AuditorSampleSelectionAdmitted selection => new AdmittedRequestChild(selection.Operation.LogicalId,
                            AdmittedRequestChildKind.SampleSelection, NativeFactReference.Of(request.Stream, selection)),
                        _ => null,
                    }).OfType<AdmittedRequestChild>().ToArray();
                var actual = await AuditorRequestResolutionProofs.ChildrenAsync(content, tenant, intent, ct).ConfigureAwait(false);
                var expectedResponses = expected.Where(static child => child.Kind == AdmittedRequestChildKind.Response)
                    .Select(static child => child.ChildId).ToHashSet();
                var uses = await ImmutableSelectionManifests.ReadAsync<SourceUseRequirement>(content, tenant.ToString(),
                    intent.SourceUses, ct).ConfigureAwait(false);
                if (actual.Count != expected.Length || !actual.ToHashSet().SetEquals(expected) ||
                    expectedResponses.Count == 0 || decision.ResponseIds.Count != expectedResponses.Count ||
                    !expectedResponses.SetEquals(decision.ResponseIds) || uses.Count != expectedResponses.Count ||
                    uses.Select(static use => use.SourceOwner).Distinct().Count() != uses.Count ||
                    uses.Any(use => use.Purpose != SourceUsePurpose.RequestResolution || use.ResourceRevision != 0 ||
                        expected.All(child => child.Kind != AdmittedRequestChildKind.Response ||
                            new AuditorResponse(tenant, decision.ProgramId, request.Id, child.ChildId).Stream != use.SourceOwner ||
                            child.Admission != use.ContentEvent)))
                    return AuditorRequestResolution.Failed(Permanent("The frozen resolution omits or substitutes an exact admitted child or response use."));
            }
            return new AuditorRequestResolution(null, request, intent, decision, tenant, history);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return AuditorRequestResolution.Failed(new RequestError(RequestErrorKind.Conflict,
                $"The exact request history cannot be verified: {error.Message}", error is ImmutableContentUnavailableException));
        }
    }

    static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}
