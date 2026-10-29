using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessReviews.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessReviews.Events;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Handlers;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
namespace Bdgrz.Compliance.Features.AccessReviews.Aggregates;

/// <summary>
///     One manually attested access population for one exact system-instance revision (M0-D07). Facts are replaced
///     only while it is a draft, each replacement as complete verified immutable content; acceptance freezes the raw
///     facts as an immutable snapshot. Classification decisions are not part of this owner: each accepted principal
///     has its own <see cref="AccessPrincipalClassification" />. Its logical revision counts the facts of this
///     population only.
/// </summary>
public sealed class AccessPopulation : Aggregate
{
    /// <summary>Stream area of every access population (the realm is the tenant).</summary>
    public const string Area = "access-populations";

    /// <summary>The status of a population whose facts may still be replaced.</summary>
    public const string Draft = "draft";

    /// <summary>The status of an attested, frozen population.</summary>
    public const string Accepted = "accepted";

    readonly Uuid _tenantId;

    /// <summary>Opens the population with a known id, selecting the logical operation to resolve.</summary>
    public AccessPopulation(Uuid tenantId, Uuid populationId, Uuid selectedLogicalId = default)
        : base(populationId, new EventStreamAddress(tenantId.ToString(), Area, populationId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessPopulationOpened>(Apply);
        On<AccessPopulationFactsRecorded>(Apply);
        On<AccessPopulationAccepted>(Apply);
    }

    /// <summary>Native acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The opening fact, once recorded.</summary>
    public AccessPopulationOpened? Opened { get; private set; }

    /// <summary>The acceptance fact, once recorded.</summary>
    public AccessPopulationAccepted? AcceptedFact { get; private set; }

    /// <summary>The complete facts currently staged, once recorded; an empty draft has none.</summary>
    public ImmutableContentRef? FactsContent { get; private set; }

    /// <summary>The logical revision: the number of facts of this population.</summary>
    public long Revision { get; private set; }

    /// <summary>True once the population was opened.</summary>
    public bool IsOpened => Opened is not null;

    /// <summary>True once the population was accepted.</summary>
    public bool IsAccepted => AcceptedFact is not null;

    /// <summary>The population status, <see cref="Draft" /> until accepted.</summary>
    public string Status => IsAccepted ? Accepted : Draft;

    /// <summary>Checks the opening guard without raising anything.</summary>
    public Result PrepareOpen() => Opened is null
        ? Result.Success
        : AccessReviewOperation.Failure(RequestErrorKind.Conflict,
            "The population already exists with different content.");

    /// <summary>Opens the population as the root native fact of its operation.</summary>
    public Result Open(OperationProvenance operation, Uuid applicationId, Uuid systemInstanceId,
        long systemInstanceRevision, DateTimeOffset observedAt, string source, ActorReference actor,
        DateTimeOffset now, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(actor);
        if (operation.Owner != Stream || operation.LogicalId != Id)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "A population opening is a root operation of its own population stream.");
        var prepared = PrepareOpen();
        if (!prepared.IsSuccess)
            return prepared;
        if (applicationId == Uuid.Empty || systemInstanceId == Uuid.Empty || string.IsNullOrWhiteSpace(source))
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "A population names its application, system instance and source.");
        RaiseEvent(new AccessPopulationOpened(_tenantId, Id, applicationId, systemInstanceId, systemInstanceRevision,
            observedAt, AccessReviewVocabulary.ManualAttestation, source, actor, now, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Checks the facts replacement guards without raising anything.</summary>
    public Result PrepareRecord(long expectedRevision)
    {
        if (Opened is null)
            return AccessReviewOperation.Failure(RequestErrorKind.NotFound, "The population was not found.");
        if (IsAccepted)
            return AccessReviewOperation.Failure(RequestErrorKind.Conflict, "An accepted population is immutable.");
        return expectedRevision == Revision ? Result.Success : Stale();
    }

    /// <summary>Replaces the staged facts as the root native fact of its operation.</summary>
    public Result Record(OperationProvenance operation, ImmutableContentRef factsContent, ActorReference actor,
        DateTimeOffset now, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(factsContent);
        ArgumentNullException.ThrowIfNull(actor);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "A facts replacement is a root operation of its own population with an expected revision.");
        var prepared = PrepareRecord((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessPopulationFactsRecorded(_tenantId, Id, Revision + 1, factsContent, actor, now,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the acceptance guards without raising anything.</summary>
    public Result PrepareAccept(long expectedRevision)
    {
        if (Opened is null)
            return AccessReviewOperation.Failure(RequestErrorKind.NotFound, "The population was not found.");
        if (IsAccepted)
            return AccessReviewOperation.Failure(RequestErrorKind.Conflict, "The population was already accepted.");
        if (expectedRevision != Revision)
            return Stale();
        return FactsContent is null
            ? AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "Record the observed facts before acceptance.")
            : Result.Success;
    }

    /// <summary>Accepts the staged facts as the root native fact of its operation.</summary>
    public Result Accept(OperationProvenance operation, Uuid snapshotId, string contentSha256, Uuid calculationId,
        string attestation, ActorReference actor, DateTimeOffset now, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(actor);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "An acceptance is a root operation of its own population with an expected revision.");
        var prepared = PrepareAccept((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        if (snapshotId == Uuid.Empty || calculationId == Uuid.Empty || string.IsNullOrWhiteSpace(contentSha256) ||
            string.IsNullOrWhiteSpace(attestation))
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "An acceptance names its snapshot, content hash, calculation and attestation.");
        RaiseEvent(new AccessPopulationAccepted(_tenantId, Id, Revision + 1, snapshotId, contentSha256, calculationId,
            attestation, actor, now, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(AccessPopulationOpened ev)
    {
        Acceptance.Observe(ev);
        if (Opened is not null || ev.TenantId != _tenantId || ev.PopulationId != Id || ev.Operation.Owner != Stream ||
            ev.Operation.LogicalId != Id || ev.FirstResponse is null)
            throw new InvalidOperationException("A population is opened once, by its own root operation.");
        Opened = ev;
        Revision = 1;
    }

    void Apply(AccessPopulationFactsRecorded ev)
    {
        Acceptance.Observe(ev);
        if (Opened is null || IsAccepted || ev.TenantId != _tenantId || ev.PopulationId != Id ||
            ev.Revision != Revision + 1 || ev.Operation.Owner != Stream || ev.FirstResponse is null)
            throw new InvalidOperationException(
                "A facts replacement follows the previous revision of an open draft population.");
        FactsContent = ev.FactsContent;
        Revision = ev.Revision;
    }

    void Apply(AccessPopulationAccepted ev)
    {
        Acceptance.Observe(ev);
        if (Opened is null || IsAccepted || ev.TenantId != _tenantId || ev.PopulationId != Id ||
            ev.Revision != Revision + 1 || ev.Operation.Owner != Stream || ev.FirstResponse is null)
            throw new InvalidOperationException(
                "An acceptance follows the previous revision of an open draft population.");
        AcceptedFact = ev;
        Revision = ev.Revision;
    }

    Result Stale() => AccessReviewOperation.Stale("access population", Revision);
}
