using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Programs.History;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Features.Versioning.Rules;
using Bdgrz.Compliance.Features.Versioning.Services;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Programs.Events;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Programs.Aggregates;

/// <summary>Current service authority and selected native receipt; authored values and history compose explicitly.</summary>
public sealed class ClientService : Aggregate
{
    public const string Area = "client-services";
    readonly Uuid _tenantId;
    bool _created;
    bool _retired;
    long _revision;
    Uuid _programId;
    ulong _sequence;
    IProductClientServiceFact? _value;
    ClientServiceRetired? _retirement;

    public ClientService(Uuid tenantId, Uuid serviceId, Uuid selectedLogicalId = default)
        : base(serviceId, new EventStreamAddress(tenantId.ToString(), Area, serviceId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ClientServiceCreated>(ApplyFact);
        On<ClientServiceRevised>(ApplyFact);
        On<ClientServiceRetired>(ApplyFact);
    }

    public Uuid TenantId => _tenantId;
    public bool IsCreated => _created;
    public bool IsActive => _created && !_retired;
    public long Revision => _revision;
    public Uuid? ProgramId => _programId == Uuid.Empty ? null : _programId;
    public ulong NativeSequence => _sequence;
    public ImmutableContentRef? CurrentContent => _value?.Content;
    public IProductClientServiceFact? CurrentValueFact => _value;
    public ClientServiceRetired? Retirement => _retirement;
    public NativeAcceptance Acceptance { get; }
    bool _emitting;
    internal NativeReplayCommitment NativeCut { get; } = new();

    public Result PrepareCreate() => !_created ? Result.Success : Failure(RequestErrorKind.Conflict,
        "The service already exists with different content.");

    public Result PrepareChange(long expectedRevision)
    {
        if (!_created)
            return Failure(RequestErrorKind.NotFound, "The service was not found.");
        if (_retired)
            return Failure(RequestErrorKind.Conflict, "The service is retired.");
        return expectedRevision == Revision ? Result.Success :
            Result.Failure(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("service", Revision)).ToRequestError());
    }

    public Result Create(OperationProvenance operation, Uuid programId, ImmutableContentRef content,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        var cut = NativeCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        var ready = PrepareCreate();
        if (!ready.IsSuccess)
            return ready;
        if (programId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation, "A service requires its owning program.");
        Emit(new ClientServiceCreated(_tenantId, Id, programId, 1, content, operation.AcceptedAt,
            operation, first, result, NativeSequence + 1));
        return Result.Success;
    }

    public Result Revise(OperationProvenance operation, ImmutableContentRef content,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        var ready = PrepareAppend(operation);
        if (!ready.IsSuccess)
            return ready;
        Emit(new ClientServiceRevised(_tenantId, Id, _programId, Revision + 1, content, operation.AcceptedAt,
            operation, first, result, NativeSequence + 1));
        return Result.Success;
    }

    public Result Retire(OperationProvenance operation, ImmutableContentRef retirement,
        ImmutableContentRef first, ImmutableContentRef result)
    {
        var ready = PrepareAppend(operation);
        if (!ready.IsSuccess)
            return ready;
        Emit(new ClientServiceRetired(_tenantId, Id, _programId, Revision + 1, _value!.Content, retirement,
            operation.AcceptedAt, operation, first, result, NativeSequence + 1));
        return Result.Success;
    }

    Result PrepareAppend(OperationProvenance operation)
    {
        var cut = NativeCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        return operation.OriginalExpectedRevision is { } expected && expected <= long.MaxValue
            ? PrepareChange((long)expected) : Failure(RequestErrorKind.Conflict, "A service change requires its original business revision.");
    }

    internal void ObserveVerified(DomainEvent ev)
    {
        ApplyFact((IProductClientServiceFact)ev);
        NativeCut.Observe(ev, false);
    }

    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        if (!ev.Metadata.IsAudit)
            NativeCut.Observe(ev, _emitting);
    }

    void Emit(DomainEvent fact)
    {
        _emitting = true;
        try
        {
            RaiseEvent(fact);
        }
        finally
        {
            _emitting = false;
        }
    }

    void ApplyFact(IProductClientServiceFact fact)
    {
        ProductClientServiceHistory.ValidateHeader(fact);
        if (fact.TenantId != _tenantId || fact.ServiceId != Id || fact.NativeSequence != NativeSequence + 1 ||
            fact.ChangedAt != fact.Operation.AcceptedAt)
            throw Invalid();
        switch (fact)
        {
            case ClientServiceCreated:
                if (_created || fact.Revision != 1)
                    throw Invalid();
                _created = true;
                _programId = fact.ProgramId;
                _value = fact;
                break;
            case ClientServiceRevised:
                if (!_created || _retired || fact.ProgramId != _programId ||
                    fact.Operation.OriginalExpectedRevision != (ulong)Revision || fact.Revision != Revision + 1)
                    throw Invalid();
                _value = fact;
                break;
            case ClientServiceRetired retired:
                if (!_created || _retired || fact.ProgramId != _programId || fact.Content != _value!.Content ||
                    fact.Operation.OriginalExpectedRevision != (ulong)Revision || fact.Revision != Revision + 1)
                    throw Invalid();
                _retired = true;
                _retirement = retired;
                break;
        }
        Acceptance.Observe((DomainEvent)fact);
        _revision = fact.Revision;
        _sequence = fact.NativeSequence;
    }

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    static InvalidOperationException Invalid() => new("The client service fact differs from its exact scope, predecessor or native transition.");
}
