using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Criteria.Commands;
using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>
///     The licensed text overlay of one criterion of one edition in one tenant: a sequential revision line. Its
///     identity derives from the tenant, the edition and the catalog's canonical criterion identifier only, so
///     different criteria never share a stream and edition text is never touched. Every revision references the
///     complete supplied content as verified immutable content; the revision counts the facts of this line.
/// </summary>
public sealed class CriteriaTextOverlay : Aggregate
{
    /// <summary>Stream area of every criteria text overlay (the realm is the tenant).</summary>
    public const string Area = "criteria-text-overlays";

    /// <summary>Schema of the supplied content a revision references.</summary>
    public const string ContentSchema = "CriteriaTextOverlayEntryRevised.OverlayContent.v1";

    /// <summary>Schema of the registration a revision returns.</summary>
    public const string RegistrationSchema = "CriteriaTextOverlay.Registration.v1";

    readonly Uuid _tenantId;
    readonly Uuid _editionId;
    readonly string _identifier;
    CriteriaTextOverlayEntryRevised? _latest;

    /// <summary>Opens the overlay line of one criterion, selecting the logical operation to resolve.</summary>
    public CriteriaTextOverlay(Uuid tenantId, Uuid editionId, string identifier, Uuid selectedLogicalId = default)
        : base(IdentityOf(tenantId, editionId, identifier),
            new EventStreamAddress(tenantId.ToString(), Area, IdentityOf(tenantId, editionId, identifier).ToString()))
    {
        (_tenantId, _editionId, _identifier) = (tenantId, editionId, identifier);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<CriteriaTextOverlayEntryRevised>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The revision of the latest fact; 0 before the first set.</summary>
    public long Revision => _latest?.Revision ?? 0;

    /// <summary>The latest revision fact, once set.</summary>
    public CriteriaTextOverlayEntryRevised? Latest => _latest;

    /// <summary>The identity of the overlay of one criterion.</summary>
    public static Uuid IdentityOf(Uuid tenantId, Uuid editionId, string identifier) =>
        AggregateIdentity.Create("CriteriaTextOverlay", tenantId, editionId, identifier);

    /// <summary>Checks the revision guard without raising anything.</summary>
    public Result PrepareSet(long expectedRevision) => expectedRevision == Revision
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            $"Reload the overlay before revising. Current revision: {Revision}."));

    /// <summary>Appends the next revision as the root native fact of its operation.</summary>
    public Result Set(OperationProvenance operation, ImmutableContentRef overlayContent,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(overlayContent);
        ArgumentNullException.ThrowIfNull(firstResponse);
        ArgumentNullException.ThrowIfNull(operationResult);
        if (operation.OriginalExpectedRevision is not { } expected ||
            !NativeOwnerHistory.IsOwnRoot(this, operation, nameof(SetCriteriaTextOverlay), expected))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An overlay revision is a root operation of its own overlay with an expected revision."));
        var prepared = PrepareSet((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new CriteriaTextOverlayEntryRevised(_tenantId, _editionId, _identifier, Revision + 1,
            overlayContent, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(CriteriaTextOverlayEntryRevised ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.EditionId != _editionId ||
            !string.Equals(ev.Identifier, _identifier, StringComparison.Ordinal) || ev.Revision != Revision + 1 ||
            ev.FirstResponse is null || ev.OperationResult is null ||
            !NativeOwnerHistory.IsOwnRoot(this, ev.Operation, nameof(SetCriteriaTextOverlay), (ulong)Revision))
            throw new InvalidOperationException(
                "An overlay revision must follow its owning tenant, edition, criterion and predecessor.");
        _latest = ev;
    }
}
