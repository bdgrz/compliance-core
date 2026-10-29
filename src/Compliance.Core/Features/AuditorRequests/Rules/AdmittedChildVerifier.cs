using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.History;
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
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AuditorRequests.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.AuditorRequests.Rules;

/// <summary>
///     Finds the admission a materializing receiver was built from on the authoritative request. The source reference
///     only names the fact; nothing a command states is trusted until the request confirms it. An admission that is not
///     visible yet is transient and a differing one is a permanent conflict.
/// </summary>
static class AdmittedChildVerifier
{
    public static async ValueTask<Result<TFact>> VerifyAsync<TFact>(IAggregateReader reader, IDomainEventReader events,
        IImmutableContentStore content, Uuid tenantId, Uuid programId,
        Uuid requestId, NativeFactReference source, ImmutableContentRef intent, Uuid operationId, Uuid logicalId,
        Func<TFact, ImmutableContentRef> intentOf, CancellationToken ct) where TFact : DomainEvent, INativeOperationFact
    {
        try
        {
            var parent = await reader.HydrateAsync(new AuditorRequest(tenantId, programId, requestId, logicalId), ct)
                .ConfigureAwait(false);
            var scoped = await AuditorCommands.TermsAsync(content, tenantId, parent, ct).ConfigureAwait(false);
            if (!scoped.IsSuccess)
                return Result<TFact>.Failure(scoped.Error!);
            var history = await AuditorRequestHistory.ReadAsync(events, parent, ct).ConfigureAwait(false);
            if (history.FactById(source.EventId) is not TFact admitted)
                return Fail<TFact>("The admission is not visible on its request yet.", true);
            if (source.Stream != parent.Stream || admitted.Metadata.AggregateVersion != source.AggregateVersion || intentOf(admitted) != intent ||
                admitted.Operation.LogicalId != logicalId || admitted.Operation.RootOperationId != operationId)
                return Fail<TFact>("The source differs from the admission of this child.", false);
            return Result<TFact>.Success(admitted);
        }
        catch (ImmutableContentIntegrityException error)
        {
            return Fail<TFact>($"The exact admitted child history cannot be verified: {error.Message}",
                error is ImmutableContentUnavailableException);
        }
    }

    internal static bool HeaderMatches(OperationProvenance operation, DurableIntentHeader header) =>
        header.RootOperationId == operation.RootOperationId && header.FirstOwner == operation.Owner &&
        header.AffectedOwner == operation.AffectedOwner && header.ExpectedRevisionOwner == operation.ExpectedRevisionOwner &&
        header.OriginalActor == operation.Actor && header.Episode == operation.Episode &&
        header.OriginalExpectedRevision == operation.OriginalExpectedRevision &&
        header.AuthoredIntentSha256 == operation.AuthoredIntentSha256 && header.AcceptedAt == operation.AcceptedAt;

    static Result<TFact> Fail<TFact>(string message, bool transient) where TFact : DomainEvent, INativeOperationFact =>
        Result<TFact>.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
