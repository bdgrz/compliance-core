using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs;

public sealed class ProductClientServiceRules
{
    readonly Uuid _tenantId;
    bool _created;
    bool _retired;
    long _revision;
    Uuid _programId;
    string? _initialName;
    string? _initialPurpose;
    string? _initialOwnerReference;

    public bool IsActive => _created && !_retired;
    public bool IsCreated => _created;
    public long Revision => _revision;
    public Uuid? ProgramId => _programId == Uuid.Empty ? null : _programId;

    public ProductClientServiceRules(Uuid tenantId, Uuid serviceId)
    {
        _tenantId = tenantId;
        Id = serviceId;
    }
    public Uuid TenantId => _tenantId;
    public Uuid Id { get; }
    public IReadOnlyList<ProductClientServiceChange> AppliedChanges => _applied;
    public IReadOnlyList<ProductClientServiceChange> PendingChanges => _pending;
    readonly List<ProductClientServiceChange> _applied = [];
    readonly List<ProductClientServiceChange> _pending = [];
    public void ClearPendingChanges() => _pending.Clear();
    void Add(ProductClientServiceChange change)
    {
        _applied.Add(change);
        _pending.Add(change);
        _revision = change.Revision;
        if (change.Kind == nameof(CreateClientService))
        {
            _created = true;
            _programId = change.ProgramId;
            _initialName = change.Content!.Name;
            _initialPurpose = change.Content.Purpose;
            _initialOwnerReference = change.Content.OwnerReference;
        }
        if (change.Kind == nameof(RetireClientService))
            _retired = true;
    }

    public Result<ClientServiceRegistration> Create(Uuid programId, string name, string purpose,
        string ownerReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        if (_created)
            return _programId == programId &&
                   StringComparer.Ordinal.Equals(_initialName, name?.Trim()) &&
                   StringComparer.Ordinal.Equals(_initialPurpose, purpose?.Trim()) &&
                   StringComparer.Ordinal.Equals(_initialOwnerReference, ownerReference?.Trim())
                ? Result<ClientServiceRegistration>.Success(new ClientServiceRegistration(Id))
                : Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.Conflict,
                    "The service already exists with different content."));
        if (programId == Uuid.Empty)
            return Result<ClientServiceRegistration>.Failure(new RequestError(RequestErrorKind.Validation,
                "A service requires its owning program."));
        var error = Validate(name, purpose, ownerReference);
        if (error is not null)
            return Result<ClientServiceRegistration>.Failure(error);
        Add(new ProductClientServiceChange(nameof(CreateClientService), 1, programId, new ProductClientServiceContent(name.Trim(), purpose.Trim(), ownerReference.Trim()), null,
            ActorReference.ForMember(actorMemberId, actorDisplay), changedAt));
        return Result<ClientServiceRegistration>.Success(new ClientServiceRegistration(Id));
    }

    public CommandFailure? Revise(long expectedRevision, string name, string purpose,
        string ownerReference, Uuid actorMemberId, string actorDisplay, DateTimeOffset changedAt)
    {
        var error = CheckChange(expectedRevision);
        if (error is not null)
            return error;
        var validationError = Validate(name, purpose, ownerReference);
        if (validationError is not null)
            return CommandFailure.InvalidContent(validationError.Message!);
        Add(new ProductClientServiceChange(nameof(ReviseClientService), _revision + 1, _programId, new ProductClientServiceContent(name.Trim(), purpose.Trim(), ownerReference.Trim()), null,
            ActorReference.ForMember(actorMemberId, actorDisplay), changedAt));
        return null;
    }

    public CommandFailure? Retire(long expectedRevision, string rationale, Uuid actorMemberId,
        string actorDisplay, DateTimeOffset changedAt)
    {
        var error = CheckChange(expectedRevision);
        if (error is not null)
            return error;
        if (string.IsNullOrWhiteSpace(rationale))
            return CommandFailure.InvalidContent("Retiring a service requires a rationale.");
        Add(new ProductClientServiceChange(nameof(RetireClientService), _revision + 1, _programId, null, rationale.Trim(),
            ActorReference.ForMember(actorMemberId, actorDisplay), changedAt));
        return null;
    }

    CommandFailure? CheckChange(long expectedRevision)
    {
        if (!_created)
            return CommandFailure.MissingRecord("The service was not found.");
        if (_retired)
            return CommandFailure.StateConflict("The service is retired.");
        return expectedRevision == _revision ? null :
            CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("service", _revision));
    }

    public static RequestError? Validate(ProductClientServiceContent value) => Validate(value.Name, value.Purpose, value.OwnerReference);

    static RequestError? Validate(string name, string purpose, string ownerReference)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new RequestError(RequestErrorKind.Validation, "A service requires a name.");
        if (string.IsNullOrWhiteSpace(purpose))
            return new RequestError(RequestErrorKind.Validation, "A service requires a purpose.");
        if (string.IsNullOrWhiteSpace(ownerReference))
            return new RequestError(RequestErrorKind.Validation, "A service requires an owner reference.");
        return null;
    }
}
