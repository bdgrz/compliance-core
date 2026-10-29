using Bdgrz.Compliance.Hosting.Persistence;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Risks.Contracts;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Aggregates;
/// <summary>One compact risk evaluation owner; immutable history is reconstructed outside command state.</summary>
public sealed class RiskEvaluation : Aggregate
{
    public const string Area = "risk-evaluations";
    public const string Inherent = "inherent";
    public const string Target = "target";
    public const string Residual = "residual";
    public const string ComplianceLead = "compliance_lead";
    public const string Executive = "executive";
    public Uuid TenantId { get; }
    public Uuid ProgramId => Header?.ProgramId ?? Uuid.Empty;
    public long Revision => Header?.Revision ?? 0;
    public long NativeSequence { get; private set; }
    public RiskEvaluationAuthorityHeader? Header { get; private set; }
    public ImmutableContentRef? CurrentAuthority { get; private set; }
    public NativeAcceptance Acceptance { get; }
    public RiskEvaluation(Uuid tenantId, Uuid riskId, Uuid selectedLogicalId = default)
        : base(riskId, new EventStreamAddress(tenantId.ToString(), Area, riskId.ToString()))
    {
        TenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<RiskAssessmentRecorded>(ApplyFact);
        On<RiskTreatmentChosen>(ApplyFact);
        On<RiskAccepted>(ApplyFact);
    }
    public Result Append(DomainEvent value, FrozenRiskEvaluationMutation frozen, long sequence, ulong physicalPosition)
    {
        if (NativeSequence != sequence || CommittedStreamPosition != physicalPosition)
            return Failure("The evaluation changed while its exact authority was prepared.", true);
        IRiskEvaluationFact fact;
        try
        { fact = RiskEvaluationNativeFact.Require(value); }
        catch (ImmutableContentIntegrityException) { return Failure("The evaluation native provenance is invalid."); }
        if (!RiskEvaluationContent.WellFormed(frozen.Mutation) || frozen.Before.TenantId != TenantId || frozen.Before.RiskId != Id ||
            (Header is null ? frozen.Before.Revision != 0 : RiskEvaluationContent.Header(frozen.Before) != Header) ||
            frozen.Header != fact.Header || frozen.Change != fact.ChangeContent || frozen.CurrentAuthority != fact.CurrentAuthorityContent ||
            frozen.CommandKind != fact.Operation.CommandKind ||
            RiskEvaluationContent.LogicalId(frozen.Mutation) != fact.Operation.LogicalId ||
            RiskEvaluationContent.Actor(frozen.Mutation) != fact.Operation.Actor || RiskEvaluationContent.At(frozen.Mutation) != fact.Operation.AcceptedAt ||
            !RiskEvaluationContent.InScope(frozen.Mutation, frozen.Header) || !RiskEvaluationContent.BindingMatches(frozen.Mutation, fact.Operation) ||
            !RiskEvaluationContent.Verify(frozen))
            return Failure("The evaluation mutation differs from its exact verified local transition.");
        try
        {
            Validate(value, fact);
            if (DomainEventPayload.Size(value) > DomainEventPayload.MaximumBytes)
                return Failure("The compact native evaluation fact exceeds the bounded envelope.");
        }
        catch (ImmutableContentIntegrityException)
        {
            return Failure("The evaluation native provenance is invalid.");
        }
        RaiseEvent(value);
        return Result.Success;
    }
    void ApplyFact(DomainEvent value)
    {
        var fact = (IRiskEvaluationFact)value;
        Validate(value, fact);
        Header = fact.Header;
        CurrentAuthority = fact.CurrentAuthorityContent;
        NativeSequence = fact.NativeSequence;
        try
        { Acceptance.Observe(value); }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("An evaluation logical identity occurs more than once on its native owner.");
        }
    }
    void Validate(DomainEvent value, IRiskEvaluationFact fact)
    {
        RiskEvaluationNativeFact.Require(value);
        if (fact.TenantId != TenantId || fact.RiskId != Id || Header is not null && fact.ProgramId != Header.ProgramId ||
            fact.Revision != Revision + 1 || fact.NativeSequence != NativeSequence + 1 ||
            fact.Header.AssessmentCount != (Header?.AssessmentCount ?? 0) + (value is RiskAssessmentRecorded ? 1 : 0) ||
            fact.Header.AcceptanceCount != (Header?.AcceptanceCount ?? 0) + (value is RiskAccepted ? 1 : 0))
            throw new ImmutableContentIntegrityException("A native evaluation fact requires its exact owner revision and current authority counts.");
    }

    static Result Failure(string message, bool transient = false) => Result.Failure(new RequestError(RequestErrorKind.Conflict, message, transient));
}
