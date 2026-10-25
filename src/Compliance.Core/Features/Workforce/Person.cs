using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce;

/// <summary>One native person: bounded current header/reference and one selected receipt; full values/history are external.</summary>
public sealed class Person : Aggregate
{
    bool _emitting;
    public Person(Uuid tenantId, Uuid personId, Uuid selectedLogicalId = default)
        : base(personId, new EventStreamAddress(tenantId.ToString(), "people", personId.ToString()))
    {
        TenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<PersonRecorded>(ObserveLegacy);
        On<PersonRevised>(ObserveLegacy);
        On<PersonMembershipCorrelated>(ObserveLegacy);
        On<PersonRegistrationRecorded>(ObserveNative);
        On<PersonDetailsRevised>(ObserveNative);
        On<PersonMembershipCorrelationRecorded>(ObserveNative);
    }
    public Uuid TenantId { get; }
    public NativeAcceptance Acceptance { get; }
    public PersonHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public bool IsCreated => Header is not null;
    public long Revision => Header?.Revision ?? 0;
    public Uuid? CorrelatedUserId => Header?.CorrelatedUserId;
    internal NativeReplayCommitment ReplayCut { get; } = new();
    public Result Append(PersonPrepared prepared, OperationProvenance operation, ImmutableContentRef frozen)
    {
        var ready = ReplayCut.Require(this, Acceptance, operation);
        if (!ready.IsSuccess) return ready;
        if (prepared.Version != Version || prepared.Position != CommittedStreamPosition || prepared.PrefixSha256 != ReplayCut.Digest || prepared.MetadataSha256 != ReplayCut.MetadataDigest ||
            prepared.Before != Header || prepared.BeforeContent != CurrentContent || !PersonContent.Verify(prepared, operation))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The exact prepared person values or native prefix changed.", true));
        _emitting = true;
        try { RaiseEvent(PersonContent.Fact(prepared, operation, frozen)); }
        finally { _emitting = false; }
        return Result.Success;
    }
    void ObserveLegacy(DomainEvent fact)
    {
        if (CurrentContent is not null)
            throw new ImmutableContentIntegrityException("A read-only legacy person fact cannot follow native content authority.");
        Header = PersonHistory.NextLegacy(TenantId, Id, Header, fact);
    }
    void ObserveNative<T>(T value) where T : DomainEvent, IPersonNativeFact
    {
        Header = PersonHistory.NextNative(TenantId, Id, Stream, Header, value);
        CurrentContent = value.Content;
        Acceptance.Observe(value);
    }
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit) ReplayCut.Observe(ev, _emitting);
        base.Apply(ev);
    }
}
