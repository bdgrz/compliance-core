using System.Globalization;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPackages;

/// <summary>
/// Owns package identity, immutable manifest version references, local approvals, and build/delivery admissions.
/// Command state contains no selections, archive bytes, delivery children, worker attempts or full read snapshots.
/// </summary>
public sealed class AuditPackageManifest : Aggregate
{
    public const string Area = "audit-packages";
    readonly Uuid _tenantId;
    readonly List<DomainEvent> _facts = [];
    readonly Dictionary<long, ImmutableContentRef> _versions = [];
    readonly HashSet<long> _frozenVersions = [];
    Uuid _programId;
    long _revision;
    long _currentVersion;

    public AuditPackageManifest(Uuid tenantId, Uuid packageId, Uuid selectedLogicalId = default,
        string? selectedCommandKind = null)
        : base(packageId, new EventStreamAddress(tenantId.ToString(), Area, packageId.ToString()))
    {
        _tenantId = tenantId;
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
    public IReadOnlyList<DomainEvent> Facts => _facts;
    public ImmutableContentRef? VersionContent(long version) => _versions.GetValueOrDefault(version);
    public bool IsFrozen(long version) => _frozenVersions.Contains(version);
    public DomainEvent? FactById(Uuid eventId) => _facts.FirstOrDefault(fact => fact.Metadata?.EventId == eventId);

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
        if (!_versions.ContainsKey(packageVersion) || !allowHistorical && packageVersion != _currentVersion)
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
        RaiseEvent(new AuditPackageBuildRequested(_tenantId, Id, programId, _revision + 1, version,
            intent, operation.AcceptedAt, operation, firstResponse, null, Member(operation)));
        return Result.Success;
    }

    public Result RecordBuilt(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        NativeFactReference request, ImmutableContentRef verifiedOutput, ImmutableContentRef operationResult)
    {
        var prepared = PrepareWrite(programId, expectedRevision, version, allowHistorical: true);
        if (!prepared.IsSuccess)
            return prepared;
        if (!Owns(operation, version) || operation.CommandKind != nameof(RecordAuditPackageBuildResult) || request.Stream != Stream ||
            FactById(request.EventId) is not AuditPackageBuildRequested admitted ||
            admitted.Metadata.AggregateVersion != request.AggregateVersion || admitted.PackageVersion != version ||
            admitted.Operation.RootOperationId != operation.RootOperationId || operation.CausationEventId != request.EventId)
            return Fail(RequestErrorKind.Conflict, "The successful archive must follow its exact admitted build.");
        if (_facts.OfType<AuditPackageBuilt>().Any(fact => fact.Operation.RootOperationId == operation.RootOperationId))
            return Fail(RequestErrorKind.Conflict, "This logical build already has a successful native result.");
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

    public Result AdmitDelivery(Uuid programId, long expectedRevision, long version, OperationProvenance operation,
        ImmutableContentRef intent, NativeFactReference build, NativeFactReference disclosure,
        IReadOnlyList<NativeFactReference> validations, ImmutableContentRef firstResponse)
    {
        var prepared = PrepareWrite(programId, expectedRevision, version, allowFrozen: true);
        if (!prepared.IsSuccess)
            return prepared;
        if (!Owns(operation, version) || !Root(operation, nameof(RecordAuditPackageDelivery), expectedRevision) ||
            !Contains<AuditPackageBuilt>(build, version) ||
            !Contains<AuditPackageDisclosureApproved>(disclosure, version) || validations.Count == 0 ||
            validations.Any(proof => !Contains<AuditPackageValidated>(proof, version) &&
                                    !Contains<AuditPackageValidationOverrideApproved>(proof, version)))
            return Fail(RequestErrorKind.Conflict, "Delivery requires this version's exact built archive and approval proofs.");
        var latest = _facts.OfType<AuditPackageValidated>().LastOrDefault(fact => fact.PackageVersion == version);
        if (latest is null || validations.All(proof => proof.EventId != latest.Metadata.EventId))
            return Fail(RequestErrorKind.Conflict, "Delivery must pin the latest applicable validation.");
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
        RaiseEvent(accepted);
        return Result.Success;
    }

    bool Contains<T>(NativeFactReference proof, long version) where T : DomainEvent, IAuditPackageManifestFact =>
        proof.Stream == Stream && FactById(proof.EventId) is T fact && fact.PackageVersion == version &&
        fact.Metadata.AggregateVersion == proof.AggregateVersion;

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
            _versions.Add(1, fact.Change);
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
                _versions.Add(_currentVersion, fact.Change);
            }
            else if (!_versions.ContainsKey(fact.PackageVersion))
                throw new InvalidOperationException("A fact cannot refer to an uncreated manifest version.");
            if (domainEvent is AuditPackageDeliveryAdmitted)
                _frozenVersions.Add(fact.PackageVersion);
            else if (domainEvent is not AuditPackageAmended && IsFrozen(fact.PackageVersion))
                throw new InvalidOperationException("A delivery-admitted version cannot be changed.");
        }
        Acceptance.Observe(domainEvent);
        _revision = fact.Revision;
        _facts.Add(domainEvent);
    }

    static Result Fail(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
