namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Reusable firm-authored draft content, without tenant records or evidence.</summary>
public sealed record MethodologyTemplateRecordContent(
    string EntryKey,
    string RecordKind,
    string Title,
    string Summary,
    IReadOnlyList<MethodologyTemplateCriterionReference> CriteriaReferences,
    Cntryl.Portia.Uuid? ContributionApprovalId = null);
