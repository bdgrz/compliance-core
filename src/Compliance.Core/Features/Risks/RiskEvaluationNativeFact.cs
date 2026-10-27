using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Risks;

/// <summary>Validates one native source envelope without trusting its read or projection caller.</summary>
static class RiskEvaluationNativeFact
{
    public static IRiskEvaluationFact Require(DomainEvent value, bool committed = false)
    {
        if (value is not IRiskEvaluationFact fact || fact.Header is not { } header || fact.Operation is not { } op ||
            fact.TenantId == Uuid.Empty || fact.ProgramId == Uuid.Empty || fact.RiskId == Uuid.Empty || fact.Revision < 1 || fact.NativeSequence < 1 ||
            header.TenantId != fact.TenantId || header.ProgramId != fact.ProgramId || header.RiskId != fact.RiskId || header.Revision != fact.Revision ||
            header.AssessmentCount < 0 || header.AcceptanceCount < 0 || !Digest(header.ViewSha256))
            throw Invalid();
        var kind = value switch
        {
            RiskAssessmentRecorded => nameof(RecordRiskAssessment),
            RiskTreatmentChosen => nameof(ChooseRiskTreatment),
            RiskAccepted => nameof(AcceptRisk),
            _ => string.Empty,
        };
        var schema = value is RiskAssessmentRecorded ? RiskEvaluationContent.AssessmentSchema :
            value is RiskTreatmentChosen ? RiskEvaluationContent.TreatmentSchema : RiskEvaluationContent.AcceptanceSchema;
        var stream = new EventStreamAddress(fact.TenantId.ToString(), RiskEvaluation.Area, fact.RiskId.ToString());
        if (kind.Length == 0 || !Reference(fact.ChangeContent, schema) || !Reference(fact.CurrentAuthorityContent, RiskEvaluationContent.AuthoritySchema) ||
            !Reference(fact.FirstResponse, FirstResponses.Schema) || !Reference(fact.OperationResult, "mutation-result-content.v1") ||
            op.LogicalId == Uuid.Empty || op.LogicalId != op.RootOperationId || op.CausationEventId is not null || op.CommandKind != kind ||
            op.Owner != stream || op.FirstOwner != stream || op.AffectedOwner != stream || op.ExpectedRevisionOwner != stream ||
            op.Episode != fact.RiskId.ToString() || op.OriginalExpectedRevision != unchecked((ulong)(fact.Revision - 1)) ||
            op.Actor is not { Kind: "member" } actor || !Uuid.TryParse(actor.Id, out var member) || member == Uuid.Empty ||
            op.AcceptedAt == default || !Digest(op.AuthoredIntentSha256))
            throw Invalid();
        if (committed)
        {
            var metadata = Metadata(value);
            if (metadata.IsAudit || metadata.EventId == Uuid.Empty || metadata.AggregateId != fact.RiskId ||
                metadata.AggregateVersion == 0 || metadata.OccurredOn == default)
                throw Invalid();
        }
        return fact;
    }

    public static DomainEventMetadata Metadata(DomainEvent value)
    {
        try
        { return value.Metadata; }
        catch (InvalidOperationException)
        {
            throw new ImmutableContentIntegrityException("The evaluation source carries no committed event metadata.");
        }
    }

    public static bool SameSource(DomainEvent candidate, DomainEvent stored)
    {
        var left = Require(candidate, true);
        var right = Require(stored, true);
        return candidate.GetType() == stored.GetType() && left.TenantId == right.TenantId && left.ProgramId == right.ProgramId &&
            left.RiskId == right.RiskId && left.Revision == right.Revision && left.NativeSequence == right.NativeSequence &&
            left.Header == right.Header && left.ChangeContent == right.ChangeContent && left.CurrentAuthorityContent == right.CurrentAuthorityContent &&
            left.Operation == right.Operation && left.FirstResponse == right.FirstResponse && left.OperationResult == right.OperationResult &&
            candidate.Metadata == stored.Metadata;
    }

    static bool Reference(ImmutableContentRef? value, string schema) => value is not null && value.ContentId != Uuid.Empty && value.Bytes >= 0 &&
        value.Schema == schema && value.MediaType == ImmutableContentStoreExtensions.JsonMediaType && Digest(value.Sha256);
    static bool Digest(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    static ImmutableContentIntegrityException Invalid() => new("The evaluation source has another exact native owner, metadata, actor, intent or result binding.");
}
