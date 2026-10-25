using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Complete immutable local decisions and bounded native facts; no history rows are truncated.</summary>
public static class AuditPopulationContent
{
    public const string StateSchema = "audit-population-state.v1";
    public static AuditPopulationHeader Header(AuditPopulationView state)
    {
        if (state is null || state.DefinitionVersions is null || state.CurrentDefinition is not { } definition ||
            state.Rows is null || state.SourceSnapshots is null || state.Versions is null || state.Amendments is null ||
            state.Exports is null || state.CloseSnapshot is null)
            throw new ImmutableContentIntegrityException("A complete population body is missing required local authority fields.");
        return new(state.TenantId, state.ProgramId, state.AuditPopulationId, state.AuditPopulationDefinitionId,
            state.TypeIIPeriodId, state.ExaminationId, state.Status, state.Revision, definition.Version,
            state.CurrentVersionNumber, state.Rows.Count, state.SourceSnapshots.Count, state.CloseSnapshot.CloseSnapshotId,
            state.CloseSnapshot.ContentSha256, state.SampleEligible, state.ActorMemberId, state.ActorDisplay, state.UpdatedAt);
    }

    public static AuditPopulationView State(DomainEvent fact) => fact switch
    {
        AuditPopulationDefined value => value.State,
        AuditPopulationDefinitionRevised value => value.State,
        AuditPopulationGenerated value => value.State,
        AuditPopulationFactRecorded value => value.State,
        AuditPopulationRowClassified value => value.State,
        AuditPopulationReconciled value => value.State,
        AuditPopulationAmended value => value.State,
        AuditPopulationFrozen value => value.State,
        AuditPopulationExportGenerated value => value.State,
        _ => throw new ImmutableContentIntegrityException("The occupied population fact has another exact state kind.")
    };
    public static DomainEvent LegacyFact(DomainEvent fact, AuditPopulationView state) => fact switch
    {
        AuditPopulationDefinedV3 => new AuditPopulationDefined(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationDefinitionRevisedV2 => new AuditPopulationDefinitionRevised(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationGeneratedV3 => new AuditPopulationGenerated(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationFactRecordedV3 => new AuditPopulationFactRecorded(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationRowClassifiedV3 => new AuditPopulationRowClassified(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationReconciledV3 => new AuditPopulationReconciled(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationAmendedV3 => new AuditPopulationAmended(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationFrozenV3 => new AuditPopulationFrozen(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        AuditPopulationExportGeneratedV3 => new AuditPopulationExportGenerated(state.TenantId, state.AuditPopulationId, state.Revision, state.ActorMemberId, state, state.UpdatedAt),
        _ => throw new ImmutableContentIntegrityException("The compact population fact has another exact kind.")
    };
    internal static string CommandKind(DomainEvent fact) => fact switch
    {
        AuditPopulationDefinitionRevisedV2 => nameof(ReviseAuditPopulationDefinition),
        AuditPopulationFactRecordedV3 => nameof(RecordAuditPopulationFacts),
        AuditPopulationRowClassifiedV3 => nameof(ClassifyAuditPopulationRow),
        AuditPopulationReconciledV3 => nameof(ReconcileAuditPopulation),
        AuditPopulationFrozenV3 => nameof(FreezeAuditPopulation),
        AuditPopulationAmendedV3 => nameof(AmendAuditPopulation),
        _ => throw new ImmutableContentIntegrityException("The selected population fact has another exact native command kind.")
    };
    internal static IRequest Authored(AuditPopulationCommandContent command)
    {
        IRequest?[] alternatives = [command.ReviseDefinition, command.RecordFacts, command.ClassifyRow, command.Reconcile, command.Freeze, command.Amend];
        var present = alternatives.Where(value => value is not null).ToArray();
        return present.Length == 1 ? present[0]! : throw new ImmutableContentIntegrityException("A population decision requires exactly one complete authored command.");
    }
    internal static CommandFailure? Decide(AuditPopulationRules rules, IRequest request, ActorReference actor, DateTimeOffset at)
    {
        if (!Uuid.TryParse(actor.Id, out var memberId) || memberId == Uuid.Empty || actor.Kind != "member")
            return CommandFailure.InvalidContent("A population decision requires its exact canonical personal member.");
        return request switch
        {
            ReviseAuditPopulationDefinition value when rules.Current?.CurrentDefinition is { } prior => rules.ReviseDefinition(
                value.ExpectedRevision, prior.Content with { SourceUniverse = value.SourceBoundary, InclusionRules = value.InclusionRules,
                    ExclusionRules = value.ExclusionRules }, value.ChangeReason, memberId, actor.Display, at),
            RecordAuditPopulationFacts value => rules.RecordFacts(value.ExpectedRevision, value.SourceSnapshot, value.Rows, memberId, actor.Display, at),
            ClassifyAuditPopulationRow value => rules.ClassifyRow(rules.Revision, value.RowId, value.Classification, value.Rationale, memberId, actor.Display, at),
            ReconcileAuditPopulation value when rules.Current is { } current => rules.Reconcile(value.ExpectedRevision,
                new AuditPopulationReconciliationContent(current.Rows.Count, Count(current, "included"), Count(current, "excluded"),
                    Count(current, "duplicate"), Count(current, "missing"), Count(current, "rejected"), Count(current, "unresolved"),
                    Count(current, "canceled"), Count(current, "not_applicable"), Count(current, "exception_backed"), value.Rationale),
                memberId, actor.Display, at),
            FreezeAuditPopulation value => rules.Freeze(value.ExpectedRevision, value.FreezeRationale, memberId, actor.Display, at),
            AmendAuditPopulation value => rules.Amend(value.ExpectedRevision, value.AmendmentReason, value.SourceSnapshot, value.ChangedRows, memberId, actor.Display, at),
            _ => CommandFailure.MissingRecord("The audit population was not found.")
        };
    }
    static int Count(AuditPopulationView state, string classification) => state.Rows.Count(row => row.Classification == classification);

    internal static async Task<Result<ImmutableContentRef>> FreezeStateAsync(IImmutableContentStore content,
        AuditPopulationView state, CancellationToken ct)
    {
        if (JsonSerializer.SerializeToUtf8Bytes(state, ComplianceCoreJsonContext.Default.AuditPopulationView).LongLength > FitzKvImmutableContentStore.MaximumContentBytes)
            return Result<ImmutableContentRef>.Failure(new RequestError(RequestErrorKind.Validation,
                "The complete population body exceeds the existing immutable-content byte admission bound."));
        return Result<ImmutableContentRef>.Success(await NativeContent.WriteAsync(content, state.TenantId, StateSchema, state, ct).ConfigureAwait(false));
    }

    internal static DomainEvent NativeFact(DomainEvent rule, AuditPopulationView state, ImmutableContentRef body,
        ImmutableContentRef command, ImmutableContentRef sources, OperationProvenance operation, ImmutableContentRef first,
        ImmutableContentRef result)
    {
        var header = Header(state);
        return rule switch
        {
            AuditPopulationDefinitionRevised => new AuditPopulationDefinitionRevisedV2(state.TenantId, state.ProgramId, state.AuditPopulationId, header, body, command, sources, operation, first, result),
            AuditPopulationFactRecorded => new AuditPopulationFactRecordedV3(state.TenantId, state.ProgramId, state.AuditPopulationId, header, body, command, sources, operation, first, result),
            AuditPopulationRowClassified => new AuditPopulationRowClassifiedV3(state.TenantId, state.ProgramId, state.AuditPopulationId, header, body, command, sources, operation, first, result),
            AuditPopulationReconciled => new AuditPopulationReconciledV3(state.TenantId, state.ProgramId, state.AuditPopulationId, header, body, command, sources, operation, first, result),
            AuditPopulationFrozen => new AuditPopulationFrozenV3(state.TenantId, state.ProgramId, state.AuditPopulationId, header, body, command, sources, operation, first, result),
            AuditPopulationAmended => new AuditPopulationAmendedV3(state.TenantId, state.ProgramId, state.AuditPopulationId, header, body, command, sources, operation, first, result),
            _ => throw new InvalidOperationException("The prepared population decision has another exact native kind.")
        };
    }
    internal static DomainEvent UnqualifiedFact(DomainEvent rule, AuditPopulationView state, ImmutableContentRef body) => rule switch
    {
        AuditPopulationDefined => new AuditPopulationDefinedV3(state.TenantId, state.ProgramId, state.AuditPopulationId, Header(state), body),
        AuditPopulationGenerated => new AuditPopulationGeneratedV3(state.TenantId, state.ProgramId, state.AuditPopulationId, Header(state), body),
        AuditPopulationExportGenerated => new AuditPopulationExportGeneratedV3(state.TenantId, state.ProgramId, state.AuditPopulationId, Header(state), body),
        _ => throw new InvalidOperationException("This producer cannot manufacture a native operation receipt.")
    };
}
