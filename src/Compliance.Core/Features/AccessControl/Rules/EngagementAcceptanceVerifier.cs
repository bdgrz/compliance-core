using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
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
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Contracts;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Rules;

/// <summary>
///     Verifies a receiver's proven source for an acceptance or staff assignment: the frozen intent is read from
///     verified immutable content and the root admission, the irreversible authorization or the terminal fact is found
///     on the authoritative engagement owner. The source reference only names the fact; nothing the caller states is
///     trusted until the owner confirms it.
/// </summary>
sealed class EngagementAcceptanceVerifier(IAggregateReader reader, IImmutableContentStore content)
{
    /// <summary>
    ///     Loads and verifies the admitted operation named by a receiver command. <paramref name="sourceKind" /> states
    ///     which fact of the engagement owner the command's source must be.
    /// </summary>
    public async ValueTask<LoadedOperation> LoadAsync(NativeFactReference source, ImmutableContentRef intentReference,
        Uuid operationId, Uuid engagementId, Uuid selectedLogicalId, CancellationToken ct,
        OperationSource sourceKind = OperationSource.Admission)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intentReference);
        if (operationId == Uuid.Empty || !Uuid.TryParse(source.Stream.Realm, CultureInfo.InvariantCulture, out var tenantId) ||
            tenantId == Uuid.Empty || source.Stream.Area != ServiceEngagementIndependence.Area ||
            !Uuid.TryParse(source.Stream.Resource, CultureInfo.InvariantCulture, out var streamEngagement) ||
            engagementId != Uuid.Empty && engagementId != streamEngagement)
            return LoadedOperation.Failed(Permanent("The receiver names another operation, engagement or tenant."));
        var owner = await reader.HydrateAsync(new ServiceEngagementIndependence(tenantId, streamEngagement,
            selectedLogicalId, operationId), ct).ConfigureAwait(false);
        if (owner.OperationOf(operationId) is not { } operation)
            return LoadedOperation.Failed(Transient("The admitted acceptance is not visible yet."));
        var expected = sourceKind switch
        {
            OperationSource.Admission => (DomainEvent?)operation.Requested,
            OperationSource.Authorization => operation.Authorized,
            OperationSource.Terminal => operation.Terminal,
            _ => null,
        };
        if (expected is null)
            return LoadedOperation.Failed(Transient("The fact the receiver relies on is not visible yet."));
        if (source.Stream != owner.Stream || expected.Metadata.EventId != source.EventId ||
            expected.Metadata.AggregateVersion != source.AggregateVersion)
            return LoadedOperation.Failed(Permanent("The source is not the expected fact of this operation."));
        if (operation.Intent != intentReference)
            return LoadedOperation.Failed(Permanent("The command differs from the frozen acceptance intent."));

        EngagementIndependenceIntent intent;
        IReadOnlyList<SourceUseRequirement> requirements;
        IReadOnlyList<PracticeSubjectIntent> subjects;
        var realm = IndependenceContent.Realm(tenantId);
        try
        {
            intent = await content.ReadJsonAsync<EngagementIndependenceIntent>(realm, intentReference, ct)
                .ConfigureAwait(false);
            if (intent.Header is null || intent.SourceUses is null || intent.Subjects is null ||
                intent.CompleteServiceHistory is null || intent.Evidence is null)
                return LoadedOperation.Failed(Permanent("The frozen acceptance is missing required immutable terms."));
            requirements = await content.ReadSelectionAsync<SourceUseRequirement>(realm, intent.SourceUses, ct)
                .ConfigureAwait(false);
            subjects = await content.ReadSelectionAsync<PracticeSubjectIntent>(realm, intent.Subjects, ct)
                .ConfigureAwait(false);
        }
        catch (ImmutableContentIntegrityException exception)
        {
            return LoadedOperation.Failed(exception is ImmutableContentUnavailableException
                ? Transient($"The frozen acceptance is unavailable: {exception.Message}")
                : Permanent($"The frozen acceptance cannot be verified: {exception.Message}"));
        }

        if (intentReference.Schema != EngagementIndependenceIntent.Schema ||
            !EngagementIntentBinding.Matches(intent.Header, operation.RequestedOperation.Operation) ||
            intent.EngagementRevision != operation.RequestedOperation.Operation.OriginalExpectedRevision ||
            intent.Header.RootOperationId != operationId || intent.EngagementId != streamEngagement ||
            intent.Header.FirstOwner != owner.Stream || intent.OperationKind != operation.Kind ||
            intent.Header.AuthoredIntentSha256 != operation.RequestedOperation.Operation.AuthoredIntentSha256)
            return LoadedOperation.Failed(Permanent("The command differs from the frozen acceptance intent."));
        return new LoadedOperation(owner, operation, intent, requirements, subjects, null);
    }

    /// <summary>Derives the verified source of an acquire receiver, or the closed reason it is already moot.</summary>
    public async ValueTask<Result<SourceUseSource>> AuthorizeAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid engagementId, Uuid commandId,
        EventStreamAddress target, long generation, string commandKind, PinnedUse pinned, CancellationToken ct)
    {
        var loaded = await LoadAsync(source, intentReference, operationId, engagementId, commandId, ct)
            .ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<SourceUseSource>.Failure(loaded.Error);
        var requirement = await RequirementOfAsync(loaded, target, ct).ConfigureAwait(false);
        if (requirement is null || generation != EngagementAcceptanceEffects.Generation ||
            requirement.Purpose != EngagementAcceptanceEffects.PurposeOf(target.Area) ||
            pinned.ContentEventId is { } eventId && requirement.ContentEvent.EventId != eventId ||
            pinned.ResourceRevision is { } revision && requirement.ResourceRevision != revision)
            return Result<SourceUseSource>.Failure(Permanent(
                "The authorization differs from the frozen source use requirement."));
        var (owner, operation, intent) = (loaded.Owner!, loaded.Operation!, loaded.Intent!);
        var superseded = operation.Rejected is not null ? "The acceptance was rejected."
            : operation.Authorized is not null ? "The acceptance is already authorized."
            : null;
        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, intentReference,
            EngagementAcceptanceEffects.UseKey(operationId, owner.Stream, target), requirement, commandKind, null,
            superseded)
        {
            RequiresAbort = operation.Rejected is not null,
        });
    }

    /// <summary>Derives the verified terminal source of a settle receiver and the disposition it requires.</summary>
    public async ValueTask<Result<SourceUseSource>> SettleAsync(NativeFactReference source,
        ImmutableContentRef intentReference, Uuid operationId, Uuid engagementId, Uuid commandId,
        EventStreamAddress target, long generation, string commandKind, CancellationToken ct)
    {
        var loaded = await LoadAsync(source, intentReference, operationId, engagementId, commandId, ct,
            OperationSource.Terminal).ConfigureAwait(false);
        if (loaded.Error is not null)
            return Result<SourceUseSource>.Failure(loaded.Error);
        var requirement = await RequirementOfAsync(loaded, target, ct).ConfigureAwait(false);
        if (requirement is null || generation != EngagementAcceptanceEffects.Generation)
            return Result<SourceUseSource>.Failure(Permanent(
                "The settlement differs from the frozen source use requirement."));
        var (owner, operation, intent) = (loaded.Owner!, loaded.Operation!, loaded.Intent!);
        var disposition = operation.Completed is not null
            ? SourceUseSettlementDisposition.IrreversibleDecision
            : SourceUseSettlementDisposition.Canceled;
        if (disposition == SourceUseSettlementDisposition.IrreversibleDecision &&
            target.Area == PracticeIndependenceCommitment.Area && PracticeSubjects.Parse(target.Resource) is { } parsed)
        {
            var key = EngagementAcceptanceEffects.UseKey(operationId, owner.Stream, target);
            var subject = await reader.HydrateAsync(new PracticeIndependenceCommitment(
                EngagementIndependenceIntent.TenantOf(intent.Header), parsed.Kind, parsed.SubjectId,
                selectedReservation: key), ct)
                .ConfigureAwait(false);
            if (subject.CommitmentOf(key) is null)
                return Result<SourceUseSource>.Failure(Transient(
                    "The permanent commitment of a completed operation is not visible yet."));
        }

        return Result<SourceUseSource>.Success(new SourceUseSource(intent.Header, intentReference,
            EngagementAcceptanceEffects.UseKey(operationId, owner.Stream, target), requirement, commandKind,
            disposition, null));
    }

    /// <summary>
    ///     The requirement that names one owner stream. A source use is a frozen requirement; a practice subject has no
    ///     pinned content, so its requirement is derived from the frozen subject and its content is written under a
    ///     deterministic identity so every receiver of the operation reads the identical bytes.
    /// </summary>
    public async ValueTask<SourceUseRequirement?> RequirementOfAsync(LoadedOperation loaded, EventStreamAddress target,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.Requirements!.SingleOrDefault(requirement => requirement.SourceOwner == target) is { } found)
            return found;
        if (loaded.Subjects!.SingleOrDefault(subject => subject.Stream == target) is not { } subject)
            return null;
        var intent = loaded.Intent!;
        var reference = await content.WriteJsonAsync(IndependenceContent.Realm(EngagementIndependenceIntent.TenantOf(intent.Header)),
            EngagementAcceptanceEffects.ContentId(intent.Header.FirstOwner, intent.Header.RootOperationId, $"practice-subject:{subject.Key}"),
            PracticeSubjectIntent.Schema, subject, ct).ConfigureAwait(false);
        return new SourceUseRequirement(subject.Stream, new NativeFactReference(subject.Stream, Uuid.Empty, 0), 0,
            SourceUsePurpose.ActualStaffAssignment, reference);
    }

    internal static RequestError Permanent(string message) => new(RequestErrorKind.Conflict, message, false);

    internal static RequestError Transient(string message) => new(RequestErrorKind.Conflict, message, true);
}

/// <summary>The exact pinned values a use receiver names, when its owner pins any; null values are not compared.</summary>
sealed record PinnedUse(Uuid? ContentEventId, ulong? ResourceRevision)
{
    /// <summary>A use that pins nothing, as a practice subject.</summary>
    public static PinnedUse None { get; } = new(null, null);
}

/// <summary>Which fact of the engagement owner the source of a receiver command must be.</summary>
enum OperationSource
{
    /// <summary>The root admission on the engagement owner.</summary>
    Admission,

    /// <summary>The irreversible authorization on the engagement owner.</summary>
    Authorization,

    /// <summary>The recorded or rejected terminal fact on the engagement owner.</summary>
    Terminal,
}

/// <summary>A verified acceptance or staff assignment of an engagement owner, or the reason it cannot be verified.</summary>
sealed record LoadedOperation(ServiceEngagementIndependence? Owner, EngagementAcceptanceRecord? Operation,
    EngagementIndependenceIntent? Intent, IReadOnlyList<SourceUseRequirement>? Requirements,
    IReadOnlyList<PracticeSubjectIntent>? Subjects, RequestError? Error)
{
    public static LoadedOperation Failed(RequestError error) => new(null, null, null, null, null, error);
}
