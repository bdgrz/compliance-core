using System.Globalization;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Contracts;
using Bdgrz.Compliance.Features.AuditPackages.History;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Services;
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
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Commands;
using Bdgrz.Compliance.Features.AuditPackages.Events;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.AuditPackages.Aggregates;

/// <summary>
/// Owns package identity, immutable manifest version references, local approvals, and build/delivery admissions.
/// Command state contains no selections, archive bytes, delivery children, worker attempts or full read snapshots.
/// </summary>
public sealed class AuditPackageManifest : Aggregate
{
    public const string Area = "audit-packages";
    readonly Uuid _tenantId;
    readonly long _selectedVersion;
    ImmutableContentRef? _currentContent;
    ImmutableContentRef? _selectedContent;
    bool _currentFrozen;
    bool _selectedFrozen;
    Uuid _programId;
    long _revision;
    long _currentVersion;

    public AuditPackageManifest(Uuid tenantId, Uuid packageId, Uuid selectedLogicalId = default,
        string? selectedCommandKind = null, long selectedVersion = 0)
        : base(packageId, new EventStreamAddress(tenantId.ToString(), Area, packageId.ToString()))
    {
        _tenantId = tenantId;
        _selectedVersion = selectedVersion;
        Acceptance = new NativeAcceptance(selectedLogicalId, selectedCommandKind);
        On<AuditPackageCreated>(ApplyFact);
        On<AuditPackageAmended>(ApplyFact);
        On<AuditPackageBuildRequested>(ApplyFact);
        On<AuditPackageBuilt>(ApplyFact);
        On<AuditPackageValidated>(ApplyFact);
        On<AuditPackageValidationOverrideApproved>(ApplyFact);
        On<AuditPackageDisclosureApproved>(ApplyFact);
        On<AuditPackageDeliveryAdmitted>(ApplyFact);
    }

    public NativeAcceptance Acceptance { get; }
    public bool IsCreated => _revision > 0;
    public long Revision => _revision;
    public long CurrentVersion => _currentVersion;
    public Uuid ProgramId => _programId;
    public Uuid TenantId => _tenantId;
    public DomainEvent? Latest { get; private set; }
    internal AuditPackageReplayCut ReplayCut { get; } = new();
    public bool HasVersion(long version) => version >= 1 && version <= _currentVersion;
    public ImmutableContentRef? CurrentContent => _currentContent;
    public ImmutableContentRef? SelectedContent => _selectedVersion == _currentVersion ? _currentContent : _selectedContent;
    public long SelectedVersion => _selectedVersion;
    public bool IsFrozen(long version) => KnownFrozen(version) ?? throw new InvalidOperationException(
        "Historical frozen authority requires explicit manifest-version selection at hydration.");
    bool? KnownFrozen(long version) => version == _currentVersion ? _currentFrozen :
        version == _selectedVersion ? _selectedFrozen : null;

    public Result PrepareCreate(Uuid programId) => IsCreated
        ? Fail(RequestErrorKind.Conflict, "The audit package identity already exists.")
        : programId == Uuid.Empty || _tenantId == Uuid.Empty || Id == Uuid.Empty
            ? Fail(RequestErrorKind.Validation, "A package requires tenant, program and package identities.")
            : Result.Success;

    public Result PrepareWrite(Uuid programId, long expectedRevision, long packageVersion,
        bool allowFrozen = false, bool allowHistorical = false)
    {
        if (!IsCreated || programId != _programId)
            return Fail(RequestErrorKind.NotFound, "The audit package was not found in this program.");
        if (expectedRevision != _revision)
            return Fail(RequestErrorKind.Conflict, "The audit package manifest revision is stale.");
        if (!HasVersion(packageVersion) || !allowHistorical && packageVersion != _currentVersion)
            return Fail(RequestErrorKind.Conflict, "The command must name the exact current manifest version.");
        if (!allowFrozen && IsFrozen(packageVersion))
            return Fail(RequestErrorKind.Conflict, "A delivery-admitted version is immutable; amend to create another version.");
        return Result.Success;
    }

    public Result Create(Uuid programId, OperationProvenance operation, ImmutableContentRef change,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareCreate(programId);
        if (!prepared.IsSuccess)
            return prepared;
        if (operation.LogicalId != Id || !Root(operation, nameof(CreateAuditPackage), null) || !Owns(operation, 1))
            return Fail(RequestErrorKind.Validation, "Creation must be the package's own root operation.");
        if (Require(operation) is { IsSuccess: false } unverified)
            return unverified;
        RaiseEvent(new AuditPackageCreated(_tenantId, Id, programId, 1, 1, change,
            operation.AcceptedAt, operation, firstResponse, operationResult, Member(operation)));
        return Result.Success;
    }

    public Result Amend(Uuid programId, long expectedRevision, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareWrite(programId, expectedRevision, _currentVersion, allowFrozen: true);
        if (!prepared.IsSuccess)
            return prepared;
        if (!Owns(operation, _currentVersion + 1) || !Root(operation, nameof(AmendAuditPackage), expectedRevision))
            return Fail(RequestErrorKind.Validation, "An amendment must name its new manifest version.");
        if (Require(operation) is { IsSuccess: false } unverified)
            return unverified;
        RaiseEvent(new AuditPackageAmended(_tenantId, Id, programId, _revision + 1, _currentVersion + 1,
            change, operation.AcceptedAt, operation, firstResponse, operationResult, Member(operation)));
        return Result.Success;
    }

    public Result AdmitBuild(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        var prepared = PrepareWrite(programId, expectedRevision, version);
        if (!prepared.IsSuccess)
            return prepared;
        if (!Owns(operation, version) || !Root(operation, nameof(RequestAuditPackageBuild), expectedRevision))
            return Fail(RequestErrorKind.Validation, "The build must be bound to this exact manifest version.");
        if (Require(operation) is { IsSuccess: false } unverified)
            return unverified;
        RaiseEvent(new AuditPackageBuildRequested(_tenantId, Id, programId, _revision + 1, version,
            intent, operation.AcceptedAt, operation, firstResponse, null, Member(operation)));
        return Result.Success;
    }

    internal Result RecordBuilt(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        NativeFactReference request, ImmutableContentRef verifiedOutput, ImmutableContentRef operationResult,
        AuditPackagePreparedProof proof)
    {
        var prepared = PrepareWrite(programId, expectedRevision, version, allowHistorical: true);
        if (!prepared.IsSuccess)
            return prepared;
        if (!proof.MatchesCut(this))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact package source changed after build proof verification; retry the callback.", true));
        if (!Owns(operation, version) || operation.CommandKind != nameof(RecordAuditPackageBuildResult) ||
            operation.CausationEventId != request.EventId || !proof.MatchesBuild(this, operation, request, version))
            return Fail(RequestErrorKind.Conflict, "The successful archive must follow its exact selected admitted build cut.");
        if (Require(operation) is { IsSuccess: false } unverified)
            return unverified;
        RaiseEvent(new AuditPackageBuilt(_tenantId, Id, programId, _revision + 1, version,
            verifiedOutput, operation.AcceptedAt, operation, null, operationResult, Member(operation)));
        return Result.Success;
    }

    public Result RecordValidation(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef firstResponse, ImmutableContentRef operationResult) =>
        AcceptDecision(programId, expectedRevision, version, operation, () =>
            new AuditPackageValidated(_tenantId, Id, programId, _revision + 1, version, change,
                operation.AcceptedAt, operation, firstResponse, operationResult, Member(operation)));

    public Result ApproveOverride(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef firstResponse, ImmutableContentRef operationResult) =>
        AcceptDecision(programId, expectedRevision, version, operation, () =>
            new AuditPackageValidationOverrideApproved(_tenantId, Id, programId, _revision + 1, version, change,
                operation.AcceptedAt, operation, firstResponse, operationResult, Member(operation)));

    public Result ApproveDisclosure(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        ImmutableContentRef change, ImmutableContentRef firstResponse, ImmutableContentRef operationResult) =>
        AcceptDecision(programId, expectedRevision, version, operation, () =>
            new AuditPackageDisclosureApproved(_tenantId, Id, programId, _revision + 1, version, change,
                operation.AcceptedAt, operation, firstResponse, operationResult, Member(operation)));

    internal Result AdmitDelivery(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        ImmutableContentRef intent, NativeFactReference build, NativeFactReference disclosure,
        IReadOnlyList<NativeFactReference> validations, ImmutableContentRef firstResponse, AuditPackagePreparedProof proof)
    {
        var prepared = PrepareWrite(programId, expectedRevision, version, allowFrozen: true);
        if (!prepared.IsSuccess)
            return prepared;
        if (!Owns(operation, version) || !Root(operation, nameof(RecordAuditPackageDelivery), expectedRevision) ||
            !proof.MatchesDelivery(this, version, build, disclosure, validations))
            return Fail(RequestErrorKind.Conflict, "Delivery requires this exact manifest cut's built archive and approval proofs.");
        if (Require(operation) is { IsSuccess: false } unverified)
            return unverified;
        RaiseEvent(new AuditPackageDeliveryAdmitted(Stream, _revision + 1, version, intent, null, operation,
            firstResponse, null, _tenantId, Id, programId, version, operation.AcceptedAt, Member(operation)));
        return Result.Success;
    }

    Result AcceptDecision(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        Func<DomainEvent> fact)
    {
        var prepared = PrepareWrite(programId, expectedRevision, version);
        if (!prepared.IsSuccess)
            return prepared;
        if (!Owns(operation, version) || operation.LogicalId != operation.RootOperationId ||
            operation.CausationEventId is not null || operation.OriginalExpectedRevision != (ulong)expectedRevision)
            return Fail(RequestErrorKind.Validation, "The decision must name its exact manifest owner and version.");
        var accepted = fact();
        var kind = accepted switch
        {
            AuditPackageValidated => nameof(ValidateAuditPackage),
            AuditPackageValidationOverrideApproved => nameof(ApproveAuditPackageValidationOverride),
            AuditPackageDisclosureApproved => nameof(ApproveAuditPackageDisclosure),
            _ => throw new InvalidOperationException("The decision fact kind is unsupported."),
        };
        if (!Root(operation, kind, expectedRevision))
            return Fail(RequestErrorKind.Validation, "The decision differs from its exact authored command kind.");
        if (Require(operation) is { IsSuccess: false } unverified)
            return unverified;
        RaiseEvent(accepted);
        return Result.Success;
    }

    Result Require(OperationProvenance operation) => ReplayCut.Require(this, Acceptance, operation);

    static Uuid? Member(OperationProvenance operation) => operation.Actor.Kind == "member" &&
        Uuid.TryParse(operation.Actor.Id, CultureInfo.InvariantCulture, out var member) ? member : null;

    bool Root(OperationProvenance operation, string kind, long? expectedRevision) =>
        operation.CommandKind == kind && operation.LogicalId == operation.RootOperationId &&
        operation.CausationEventId is null && (operation.FirstOwner is null || operation.FirstOwner == Stream) &&
        operation.OriginalExpectedRevision == (expectedRevision is { } expected ? (ulong)expected : null);

    bool Owns(OperationProvenance operation, long version) => operation.LogicalId != Uuid.Empty &&
        operation.RootOperationId != Uuid.Empty && operation.Owner == Stream && operation.AffectedOwner == Stream &&
        operation.ExpectedRevisionOwner == Stream && operation.Episode == version.ToString(CultureInfo.InvariantCulture);

    void ApplyFact(DomainEvent domainEvent)
    {
        var fact = (IAuditPackageManifestFact)domainEvent;
        if (fact.TenantId != _tenantId || fact.SubjectId != Id || fact.Revision != _revision + 1 ||
            fact.Change is null || fact.ActorMemberId != Member(fact.Operation) || !Owns(fact.Operation, fact.PackageVersion))
            throw new InvalidOperationException("A manifest fact must advance its exact owner at the next local revision.");
        if (domainEvent is AuditPackageDeliveryAdmitted delivery &&
            (delivery.Owner != Stream || delivery.Generation != delivery.PackageVersion))
            throw new InvalidOperationException("A delivery admission must name its exact owner and manifest generation.");
        if (fact.OperationResult is null && domainEvent is not (AuditPackageBuildRequested or AuditPackageDeliveryAdmitted))
            throw new InvalidOperationException("A terminal native package fact must retain its original typed result.");
        var kind = domainEvent switch
        {
            AuditPackageCreated => nameof(CreateAuditPackage),
            AuditPackageAmended => nameof(AmendAuditPackage),
            AuditPackageBuildRequested => nameof(RequestAuditPackageBuild),
            AuditPackageBuilt => nameof(RecordAuditPackageBuildResult),
            AuditPackageValidated => nameof(ValidateAuditPackage),
            AuditPackageValidationOverrideApproved => nameof(ApproveAuditPackageValidationOverride),
            AuditPackageDisclosureApproved => nameof(ApproveAuditPackageDisclosure),
            AuditPackageDeliveryAdmitted => nameof(RecordAuditPackageDelivery),
            _ => throw new InvalidOperationException("The native package fact kind is not supported."),
        };
        if (fact.Operation.CommandKind != kind || domainEvent is not AuditPackageBuilt &&
            !Root(fact.Operation, kind, domainEvent is AuditPackageCreated ? null : _revision))
            throw new InvalidOperationException("The fact differs from its root command kind or revision authority.");
        if (domainEvent is AuditPackageCreated)
        {
            if (IsCreated || fact.PackageVersion != 1 || fact.ProgramId == Uuid.Empty || fact.Operation.LogicalId != Id)
                throw new InvalidOperationException("A manifest is created once in its authoritative program.");
            _programId = fact.ProgramId;
            _currentVersion = 1;
            _currentContent = fact.Change;
        }
        else
        {
            if (!IsCreated || fact.ProgramId != _programId)
                throw new InvalidOperationException("A manifest fact cannot change its program binding.");
            if (domainEvent is AuditPackageAmended)
            {
                if (fact.PackageVersion != _currentVersion + 1)
                    throw new InvalidOperationException("An amendment must create the next manifest version.");
                _currentVersion = fact.PackageVersion;
                _currentContent = fact.Change;
                _currentFrozen = false;
            }
            else if (!HasVersion(fact.PackageVersion))
                throw new InvalidOperationException("A fact cannot refer to an uncreated manifest version.");
            if (domainEvent is AuditPackageDeliveryAdmitted)
            {
                if (fact.PackageVersion == _currentVersion)
                    _currentFrozen = true;
                if (fact.PackageVersion == _selectedVersion)
                    _selectedFrozen = true;
            }
            else if (domainEvent is not AuditPackageAmended && KnownFrozen(fact.PackageVersion) == true)
                throw new InvalidOperationException("A delivery-admitted version cannot be changed.");
        }
        if ((domainEvent is AuditPackageCreated or AuditPackageAmended) && fact.PackageVersion == _selectedVersion)
            _selectedContent = fact.Change;
        Acceptance.Observe(domainEvent);
        _revision = fact.Revision;
        Latest = domainEvent;
        ReplayCut.Observe(domainEvent);
    }

    static Result Fail(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
