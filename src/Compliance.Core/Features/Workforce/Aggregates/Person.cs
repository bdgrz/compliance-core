using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.Workforce.History;
using Bdgrz.Compliance.Features.Workforce.Models;
using Bdgrz.Compliance.Features.Workforce.Services;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Workforce.Events;
namespace Bdgrz.Compliance.Features.Workforce.Aggregates;

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
        if (!ready.IsSuccess)
            return ready;
        if (prepared.Version != Version || prepared.Position != CommittedStreamPosition || prepared.PrefixSha256 != ReplayCut.Digest || prepared.MetadataSha256 != ReplayCut.MetadataDigest ||
            prepared.Before != Header || prepared.BeforeContent != CurrentContent || !PersonContent.Verify(prepared, operation))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict, "The exact prepared person values or native prefix changed.", true));
        _emitting = true;
        try
        { RaiseEvent(PersonContent.Fact(prepared, operation, frozen)); }
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
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev, _emitting);
        base.Apply(ev);
    }
}
