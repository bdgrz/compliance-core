using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.MethodologyTemplates.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Events;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.Providers.Aggregates;

/// <summary>
///     One assurance report of one provider: its first record and every revision, independent of every other
///     report. The logical revision counts this owner's revisions only. The owner embeds
///     <see cref="SourceUseReservations" />: a held provider review does not advance the revision but refuses a
///     revision until the review is recorded or canceled, so a review always binds the exact revision it relied on.
/// </summary>
public sealed class ProviderAssuranceReport : Aggregate, ISourceUseOwner
{
    readonly Uuid _tenantId;
    readonly Uuid _providerId;
    readonly Uuid _reportId;
    DomainEvent? _latest;
    ImmutableContentRef? _content;
    long _revision;
    long _providerRevision;

    /// <summary>Opens the report of one provider, selecting the logical operation to resolve.</summary>
    public ProviderAssuranceReport(Uuid tenantId, Uuid providerId, Uuid reportId, Uuid selectedLogicalId = default)
        : base(ProviderAssuranceStreams.ReportIdentity(tenantId, providerId, reportId),
            ProviderAssuranceStreams.Report(tenantId, providerId, reportId))
    {
        _tenantId = tenantId;
        _providerId = providerId;
        _reportId = reportId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<AssuranceReportRecorded>(recorded =>
        {
            Acceptance.Observe(recorded);
            if (_revision != 0 || recorded.TenantId != _tenantId || recorded.ProviderId != _providerId ||
                recorded.ReportId != _reportId || recorded.Operation.Owner != Stream ||
                recorded.Operation.LogicalId != _reportId || recorded.RequestId != _reportId)
                throw new InvalidOperationException("A report is recorded once, by its own provider and logical identity.");
            _revision = 1;
            Latest(recorded, recorded.ContentContent, recorded.ProviderRevision);
        });
        On<AssuranceReportRevised>(revised =>
        {
            Acceptance.Observe(revised);
            if (_revision == 0 || revised.TenantId != _tenantId || revised.ProviderId != _providerId ||
                revised.ReportId != _reportId || revised.Revision != _revision + 1 ||
                revised.Operation.Owner != Stream || revised.Operation.LogicalId != revised.RequestId)
                throw new InvalidOperationException("A revision must follow its provider, report and previous revision.");
            _revision = revised.Revision;
            Latest(revised, revised.ContentContent, revised.ProviderRevision);
        });
        On<ProviderAssuranceReportProviderReviewUseAuthorized>(granted =>
        {
            Acceptance.Observe(granted);
            if (_revision == 0 || granted.Revision != _revision)
                throw new InvalidOperationException("A report use is granted on a recorded report at its held revision.");
            Uses.ApplyGranted(SourceUsePurpose.ProviderReview, granted);
        });
        On<ProviderAssuranceReportProviderReviewUseSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.ProviderReview, settled);
        });
    }

    /// <summary>The active grants, receiver generation and fences of this report's source uses.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The provider this report belongs to; it never changes.</summary>
    public Uuid ProviderId => _providerId;

    /// <summary>The report this owner holds.</summary>
    public Uuid ReportId => _reportId;

    /// <summary>True once the report was recorded.</summary>
    public bool Exists => _revision > 0;

    /// <summary>The logical revision: 1 for the first record, then one per revision of this report.</summary>
    public long Revision => _revision;

    /// <summary>The fact that carries the current content; its event id and version identify the held fact.</summary>
    public DomainEvent? LatestFact => _latest;

    /// <summary>The immutable content of the current revision.</summary>
    public ImmutableContentRef? LatestContent => _content;

    /// <summary>The provider revision observed when the current revision was recorded.</summary>
    public long LatestProviderRevision => _providerRevision;

    /// <summary>Checks the creation guard without raising anything.</summary>
    public Result PrepareRecord() => _revision == 0
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The assurance report identity already exists."));

    /// <summary>Accepts the report as the first native fact of its logical identity.</summary>
    public Result Record(OperationProvenance operation, ImmutableContentRef content, long providerRevision,
        ActorReference actor, DateTimeOffset recordedAt, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId != _reportId || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "An assurance report is the logical operation of its own report owner."));
        var prepared = PrepareRecord();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AssuranceReportRecorded(_tenantId, _reportId, _providerId, _reportId, content, providerRevision,
            actor, recordedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the revision guards without raising anything.</summary>
    public Result PrepareRevise(long expectedRevision)
    {
        if (_revision == 0)
            return Result.Failure(new RequestError(RequestErrorKind.NotFound, "The assurance report was not found."));
        if (expectedRevision != _revision)
            return Result.Failure(VersionedRecordRules.StaleRevision("assurance report", _revision).ToRequestError());
        return Uses.HasActiveUse
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A provider review holds this report revision; retry after it is recorded or canceled.", true))
            : Result.Success;
    }

    /// <summary>Accepts a revision as the native fact of its logical command.</summary>
    public Result Revise(OperationProvenance operation, long expectedRevision, ImmutableContentRef content,
        long providerRevision, ActorReference actor, DateTimeOffset recordedAt, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.LogicalId == Uuid.Empty || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A report revision requires its own logical operation."));
        var prepared = PrepareRevise(expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AssuranceReportRevised(_tenantId, _reportId, _providerId, operation.LogicalId, _revision + 1,
            content, providerRevision, actor, recordedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Holds this exact report revision for a provider review; the revision does not advance.</summary>
    public Result AuthorizeProviderReviewUse(OperationProvenance operation, ImmutableContentRef intent,
        long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (_revision == 0 || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only a recorded report can be held, on its own stream.", true));
        Uses.RequireForAppend(SourceUsePurpose.ProviderReview, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new ProviderAssuranceReportProviderReviewUseAuthorized(Stream, _revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Settles a provider review use, or fences its key when no grant exists yet.</summary>
    public Result SettleProviderReviewUse(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the held report stream."));
        Uses.RequireForAppend(SourceUsePurpose.ProviderReview, operation, generation, Version, CommittedStreamPosition);
        RaiseEvent(new ProviderAssuranceReportProviderReviewUseSettled(Stream, _revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>The provenance of an operation on this owner; the owner is its own first, affected and revision scope.</summary>
    public OperationProvenance Provenance(Uuid commandId, string commandKind, ActorReference actor, string episode,
        ulong? originalExpectedRevision, string authoredIntentSha256, DateTimeOffset acceptedAt) =>
        new(commandId, commandId, null, commandKind, actor, Stream, episode, originalExpectedRevision,
            authoredIntentSha256, acceptedAt, Stream, Stream);

    void Latest(DomainEvent fact, ImmutableContentRef content, long providerRevision)
    {
        _latest = fact;
        _content = content;
        _providerRevision = providerRevision;
    }
    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
