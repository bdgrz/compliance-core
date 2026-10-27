using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Aggregates;

/// <summary>Bounded command authority for one program's immutable publication line.</summary>
public sealed class RiskMethod : Aggregate
{
    public const string Area = "risk-methods";
    public const string QualitativeFiveByFive = "qualitative_5x5";
    public const string AnnualReassessment = "P1Y";
    readonly Uuid _tenant;
    readonly Uuid _program;

    public RiskMethod(Uuid tenantId, Uuid programId, Uuid selectedLogicalId = default)
        : base(IdFor(tenantId, programId), new EventStreamAddress(tenantId.ToString(), Area, IdFor(tenantId, programId).ToString()))
    {
        (_tenant, _program) = (tenantId, programId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<RiskMethodVersionPublished>(ApplyFact);
    }

    public NativeAcceptance Acceptance { get; }
    public RiskMethodVersionPublished? Latest { get; private set; }
    public long PublishedVersion => Latest?.PublishedVersion ?? 0;
    public static Uuid IdFor(Uuid tenantId, Uuid programId) => Uuid.CreateVersion5(tenantId, $"risk-method:{programId}");

    public Result PreparePublish(long expectedVersion) => expectedVersion == PublishedVersion && expectedVersion < long.MaxValue
        ? Result.Success : Result.Failure(new RequestError(RequestErrorKind.Conflict,
            $"Reload the risk method before publishing. Current version: {PublishedVersion}."));

    public Result Append(OperationProvenance operation, ImmutableContentRef version, ImmutableContentRef first,
        ImmutableContentRef result)
    {
        if (operation.OriginalExpectedRevision is not { } expected || expected > long.MaxValue)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A method publication requires its original version gate."));
        var prepared = PreparePublish((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new RiskMethodVersionPublished(_tenant, _program, PublishedVersion + 1, version, operation, first, result));
        return Result.Success;
    }

    void ApplyFact(RiskMethodVersionPublished fact)
    {
        if (fact.TenantId != _tenant || fact.ProgramId != _program || fact.PublishedVersion != PublishedVersion + 1 ||
            !RiskMethodContent.ValidNative(fact) || fact.Operation.FirstOwner != Stream)
            throw new InvalidOperationException("A method publication differs from its exact native owner and predecessor.");
        Acceptance.Observe(fact);
        Latest = fact;
    }
}
