using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessReviews.Contracts;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Services;
using Bdgrz.Compliance.Features.Evidence.Rules;
using Bdgrz.Compliance.Features.Examinations.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Services;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Risks.Rules;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Events;
namespace Bdgrz.Compliance.Features.Risks.Services;

public static class RiskEvaluationContent
{
    public const string AuthoritySchema = "risk-evaluation-current-authority.v1";
    public const string AssessmentSchema = "risk-assessment-change.v1";
    public const string TreatmentSchema = "risk-treatment-change.v1";
    public const string AcceptanceSchema = "risk-acceptance-change.v1";
    public const string AssessmentResultSchema = "risk-assessment-result.v1";
    public const string AcceptanceResultSchema = "risk-acceptance-result.v1";
    public static RiskEvaluationAuthorityHeader Header(RiskEvaluationView view) => new(view.TenantId, view.ProgramId, view.RiskId,
        view.Revision, view.Assessments.LastOrDefault(value => value.Phase == RiskEvaluation.Inherent)?.AssessmentId,
        view.Assessments.LastOrDefault(value => value.Phase == RiskEvaluation.Inherent)?.MethodVersionId,
        view.Assessments.LastOrDefault(value => value.Phase == RiskEvaluation.Residual)?.AssessmentId,
        view.Treatment?.Kind, LatestAcceptance(view)?.AcceptanceId, view.Assessments.Count, view.Acceptances.Count,
        AuthoredIntentDigest.Compute(view));
    public static RiskEvaluationCurrentAuthority Current(RiskEvaluationView view) => new(
        view.Assessments.LastOrDefault(value => value.Phase == RiskEvaluation.Inherent),
        view.Assessments.LastOrDefault(value => value.Phase == RiskEvaluation.Target),
        view.Assessments.LastOrDefault(value => value.Phase == RiskEvaluation.Residual), view.Treatment,
        LatestAcceptance(view), view.LastChangedBy, view.LastChangedAt);
    static RiskAcceptanceView? LatestAcceptance(RiskEvaluationView view) => view.Acceptances.Count == 0 ? null : view.Acceptances[^1];
    public static async ValueTask<FrozenRiskEvaluationMutation> FreezeAsync(IImmutableContentStore content,
        RiskEvaluationView before, RiskEvaluationRules rules, string kind, CancellationToken ct = default)
    {
        if (rules.PendingChanges.Count != 1)
            throw new ImmutableContentIntegrityException("A new evaluation command must produce exactly one local business transition.");
        var mutation = rules.PendingChanges[0];
        var after = rules.ToView();
        var change = mutation switch
        {
            RiskAssessmentRecordedBody value => await NativeContent.WriteAsync(content, rules.TenantId, AssessmentSchema, value, ct).ConfigureAwait(false),
            RiskTreatmentChosenBody value => await NativeContent.WriteAsync(content, rules.TenantId, TreatmentSchema, value, ct).ConfigureAwait(false),
            RiskAcceptedBody value => await NativeContent.WriteAsync(content, rules.TenantId, AcceptanceSchema, value, ct).ConfigureAwait(false),
            _ => throw new ImmutableContentIntegrityException("The evaluation mutation has another selected kind."),
        };
        var authority = await NativeContent.WriteAsync(content, rules.TenantId, AuthoritySchema, Current(after), ct).ConfigureAwait(false);
        return new(before, after, Header(after), change, authority, mutation, kind);
    }
    public static async ValueTask<object> ReadMutationAsync(IImmutableContentStore content, DomainEvent value, CancellationToken ct = default)
    {
        var fact = RiskEvaluationNativeFact.Require(value);
        object body = value switch
        {
            RiskAssessmentRecorded when fact.ChangeContent.Schema == AssessmentSchema => await NativeContent.ReadAsync<RiskAssessmentRecordedBody>(content, fact.TenantId, fact.ChangeContent, ct).ConfigureAwait(false),
            RiskTreatmentChosen when fact.ChangeContent.Schema == TreatmentSchema => await NativeContent.ReadAsync<RiskTreatmentChosenBody>(content, fact.TenantId, fact.ChangeContent, ct).ConfigureAwait(false),
            RiskAccepted when fact.ChangeContent.Schema == AcceptanceSchema => await NativeContent.ReadAsync<RiskAcceptedBody>(content, fact.TenantId, fact.ChangeContent, ct).ConfigureAwait(false),
            _ => throw new ImmutableContentIntegrityException("The evaluation source has another exact change schema."),
        };
        if (!WellFormed(body) || !InScope(body, fact.Header) || !BindingMatches(body, fact.Operation) ||
            LogicalId(body) != fact.Operation.LogicalId || Actor(body) != fact.Operation.Actor || At(body) != fact.Operation.AcceptedAt)
            throw new ImmutableContentIntegrityException("The evaluation immutable change has incomplete or mismatched native rule inputs.");
        return body;
    }
    public static async ValueTask<RiskAcceptanceView> ReadAcceptanceAsync(IImmutableContentStore content, RiskAccepted value, CancellationToken ct = default) =>
        ((RiskAcceptedBody)await ReadMutationAsync(content, value, ct).ConfigureAwait(false)).Acceptance;
    public static bool Verify(FrozenRiskEvaluationMutation frozen)
    {
        if (!WellFormed(frozen.Mutation) || !InScope(frozen.Mutation, frozen.Header) || LogicalId(frozen.Mutation) == Uuid.Empty ||
            Actor(frozen.Mutation) is not { Kind: "member" } member || !Uuid.TryParse(member.Id, out var parsedMember) || parsedMember == Uuid.Empty ||
            At(frozen.Mutation) == default)
            return false;
        var rules = RiskEvaluationRules.FromView(frozen.Before);
        var failure = frozen.Mutation switch
        {
            RiskAssessmentRecordedBody value when value.Method is not null => rules.RecordAssessment(value.ProgramId, frozen.Before.Revision,
                value.Assessment.AssessmentId, value.Method, value.Assessment.Phase, value.Assessment.Likelihood, value.Assessment.Impact,
                value.Assessment.Rationale, parsedMember, value.Assessment.Assessor.Display,
                value.Assessment.AssessedAt, value.HasAcceptedControlTreatment),
            RiskTreatmentChosenBody value => rules.ChooseTreatment(value.ProgramId, frozen.Before.Revision, value.Treatment.Kind,
                value.Treatment.Rationale, parsedMember, value.Treatment.ChosenBy.Display, value.Treatment.ChosenAt,
                value.CommandId, value.AuthoredIntentSha256),
            RiskAcceptedBody value when value.Method is not null => rules.Accept(value.ProgramId, frozen.Before.Revision,
                value.Acceptance.AcceptanceId, value.Acceptance.ResidualAssessmentId, value.Method, value.Acceptance.ApproverAuthority,
                value.ApproverHoldsAuthority, value.Acceptance.ExpiresAt, value.Acceptance.Rationale, value.Acceptance.ApproverMemberId,
                value.Acceptance.Approver.Display, value.Acceptance.AcceptedAt, value.OwnerMemberId, value.WaiverProof),
            _ => throw new ImmutableContentIntegrityException("The evaluation change has no exact rule inputs."),
        };
        return failure is null && rules.PendingChanges.Count == 1 &&
            AuthoredIntentDigest.Compute(rules.ToView()) == AuthoredIntentDigest.Compute(frozen.After) && Header(frozen.After) == frozen.Header;
    }
    public static bool WellFormed(object value) => value switch
    {
        RiskAssessmentRecordedBody { Assessment: { Assessor: not null }, Method: { LikelihoodScale: not null, ImpactScale: not null, PublishedBy: not null } } => true,
        RiskTreatmentChosenBody { Treatment: { ChosenBy: not null } } => true,
        RiskAcceptedBody { Acceptance: { Approver: not null }, Method: { LikelihoodScale: not null, ImpactScale: not null, PublishedBy: not null } } item =>
            item.WaiverProof is null || item.WaiverProof.ApprovalFact is { Source: not null },
        _ => false,
    };
    public static bool InScope(object value, RiskEvaluationAuthorityHeader header) => header is not null && (value switch
    {
        RiskAssessmentRecordedBody item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.RiskId == header.RiskId && item.Revision == header.Revision,
        RiskTreatmentChosenBody item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.RiskId == header.RiskId && item.Revision == header.Revision,
        RiskAcceptedBody item => item.TenantId == header.TenantId && item.ProgramId == header.ProgramId && item.RiskId == header.RiskId && item.Revision == header.Revision,
        _ => false,
    });
    public static bool BindingMatches(object value, OperationProvenance operation) => value switch
    {
        RiskAssessmentRecordedBody item => item.OriginalExpectedRevision is { } revision && unchecked((ulong)revision) == operation.OriginalExpectedRevision && item.AuthoredIntentSha256 == operation.AuthoredIntentSha256,
        RiskTreatmentChosenBody item => item.OriginalExpectedRevision is { } revision && unchecked((ulong)revision) == operation.OriginalExpectedRevision && item.AuthoredIntentSha256 == operation.AuthoredIntentSha256,
        RiskAcceptedBody item => item.OriginalExpectedRevision is { } revision && unchecked((ulong)revision) == operation.OriginalExpectedRevision && item.AuthoredIntentSha256 == operation.AuthoredIntentSha256,
        _ => false,
    };
    public static Uuid LogicalId(object value) => value switch
    {
        RiskAssessmentRecordedBody item => item.Assessment.AssessmentId,
        RiskTreatmentChosenBody item => item.CommandId ?? Uuid.Empty,
        RiskAcceptedBody item => item.Acceptance.AcceptanceId,
        _ => Uuid.Empty,
    };
    public static ActorReference Actor(object value) => value switch
    {
        RiskAssessmentRecordedBody item => item.Assessment.Assessor,
        RiskTreatmentChosenBody item => item.Treatment.ChosenBy,
        RiskAcceptedBody item => item.Acceptance.Approver,
        _ => throw new ImmutableContentIntegrityException("The evaluation mutation has no member actor."),
    };
    public static DateTimeOffset At(object value) => value switch
    {
        RiskAssessmentRecordedBody item => item.Assessment.AssessedAt,
        RiskTreatmentChosenBody item => item.Treatment.ChosenAt,
        RiskAcceptedBody item => item.Acceptance.AcceptedAt,
        _ => default,
    };
    public static DomainEvent Fact(FrozenRiskEvaluationMutation frozen, long sequence, OperationProvenance operation,
        ImmutableContentRef first, ImmutableContentRef result) => frozen.Mutation switch
        {
            RiskAssessmentRecordedBody => new RiskAssessmentRecorded(frozen.Header.TenantId, frozen.Header.ProgramId, frozen.Header.RiskId,
                frozen.Header.Revision, sequence, frozen.Header, frozen.Change, frozen.CurrentAuthority, operation, first, result),
            RiskTreatmentChosenBody => new RiskTreatmentChosen(frozen.Header.TenantId, frozen.Header.ProgramId, frozen.Header.RiskId,
                frozen.Header.Revision, sequence, frozen.Header, frozen.Change, frozen.CurrentAuthority, operation, first, result),
            RiskAcceptedBody => new RiskAccepted(frozen.Header.TenantId, frozen.Header.ProgramId, frozen.Header.RiskId,
                frozen.Header.Revision, sequence, frozen.Header, frozen.Change, frozen.CurrentAuthority, operation, first, result),
            _ => throw new ImmutableContentIntegrityException("The evaluation mutation has another selected native fact."),
        };
}
