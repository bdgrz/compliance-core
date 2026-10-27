using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Risks.Contracts;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Models;
namespace Bdgrz.Compliance.Features.Risks.Aggregates;

/// <summary>Current compact risk identity and one selected native acceptance; narrative and history are read composition.</summary>
public sealed class RiskDraft : Aggregate
{
    public const string Area = "risks";
    public const int MaximumDraftEventPayloadBytes = 48 * 1024;
    readonly Uuid _tenantId;
    public RiskDraft(Uuid tenantId, Uuid riskId, Uuid selectedCommandId = default)
        : base(riskId, new EventStreamAddress(tenantId.ToString(), Area, riskId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedCommandId);
        On<RiskDraftCreated>(Apply);
        On<RiskDraftRevised>(Apply);
    }
    public Uuid TenantId => _tenantId;
    public bool IsCreated => Header is not null;
    public Uuid ProgramId => Header?.ProgramId ?? Uuid.Empty;
    public long Revision => Header?.Revision ?? 0;
    public RiskDraftAuthorityHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentContent { get; private set; }
    public long NativeSequence { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public static string NormalizeIdentifier(string? identifier) => RiskDraftRules.NormalizeIdentifier(identifier);
    public static Uuid IdFor(Uuid tenant, Uuid program, string identifier) => RiskDraftRules.IdFor(tenant, program, identifier);

    public Result Append(DomainEvent value, RiskDraftRevisionView prepared, long sequence, ulong physicalPosition)
    {
        if (NativeSequence != sequence || CommittedStreamPosition != physicalPosition)
            return Failure("The risk draft changed during its immutable preparation.", true);
        if (value is not IRiskDraftFact fact || prepared is null || fact.Content is null || RiskDraftRules.Validate(prepared.Identifier, prepared.Content) is not null ||
            ContentDigest.OfJson(prepared.Content) != ContentDigest.OfJson(RiskDraftRules.Clean(prepared.Content)) || RiskDraftFactContent.Header(prepared) != fact.Header ||
            ContentDigest.OfJson(prepared) != fact.Content.Sha256)
            return Failure("The risk draft fact differs from its exact prepared content and authority.");
        try
        { Validate(value, fact); }
        catch (InvalidOperationException) { return Failure("The risk draft fact has an invalid native owner binding."); }
        RaiseEvent(value);
        return Result.Success;
    }
    void Apply(RiskDraftCreated fact) => ApplyFact(fact);
    void Apply(RiskDraftRevised fact) => ApplyFact(fact);
    void ApplyFact(DomainEvent value)
    {
        var fact = (IRiskDraftFact)value;
        Validate(value, fact);
        Header = fact.Header;
        CurrentContent = fact.Content;
        NativeSequence = fact.NativeSequence;
        Acceptance.Observe(value);
    }
    void Validate(DomainEvent value, IRiskDraftFact fact)
    {
        try
        { RiskDraftFactContent.ValidateNative(value); }
        catch (ImmutableContentIntegrityException failure) { throw new InvalidOperationException("The risk draft native fact has invalid authority.", failure); }
        var h = fact.Header;
        if (h.TenantId != _tenantId || h.RiskId != Id || h.Revision != Revision + 1 || fact.NativeSequence != NativeSequence + 1 ||
            value is RiskDraftCreated && Header is not null ||
            value is RiskDraftRevised && (Header is null || Header.ProgramId != h.ProgramId || Header.Identifier != h.Identifier))
            throw new InvalidOperationException("The risk draft native fact has another exact predecessor or owner cut.");
    }
    static Result Failure(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
