using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.ControlMappings;

static class ControlCoverageDirectorySchema
{
    public static readonly KvDirectoryIndex<ControlCriterionMappingView> MappingsByProgram = new(
        "by_program_criterion", 1, static mapping =>
        [
            mapping.ProgramId.ToString(), mapping.EditionId.ToString(),
            mapping.CriterionIdentifier, mapping.MappingId.ToString(),
        ]);

    public static readonly KvDirectory<ControlCriterionMappingView, Uuid> Mappings = new(
        "control_mappings_v1", ComplianceCoreJsonContext.Default.ControlCriterionMappingView,
        static mapping => LineKey(mapping),
        static lineId => [lineId.ToString()], [MappingsByProgram]);

    /// <summary>The key of a mapping line: the identity of its owner stream.</summary>
    public static Uuid LineKey(ControlCriterionMappingView mapping) => ControlCriterionMapping.IdentityOf(
        mapping.TenantId, mapping.ProgramId, mapping.ControlId, mapping.EditionId, mapping.CriterionIdentifier);

    public static readonly KvDirectoryIndex<CriterionApplicabilityView> DecisionsByProgram = new(
        "by_program_criterion", 1, static decision =>
        [
            decision.ProgramId.ToString(), decision.EditionId.ToString(),
            decision.CriterionIdentifier,
        ]);

    public static readonly KvDirectory<CriterionApplicabilityView, Uuid> Decisions = new(
        "criterion_applicability_v1",
        ComplianceCoreJsonContext.Default.CriterionApplicabilityView,
        static decision => LineKey(decision),
        static lineId => [lineId.ToString()], [DecisionsByProgram]);

    /// <summary>The key of an applicability line: the identity of its owner stream.</summary>
    public static Uuid LineKey(CriterionApplicabilityView decision) => CriterionApplicability.IdentityOf(
        decision.TenantId, decision.ProgramId, decision.EditionId, decision.CriterionIdentifier);
}
