using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Tenant-scoped proposal and review record; its source identity never leaves this tenant stream.</summary>
public sealed record MethodologyTemplateContributionView(
    Uuid TenantId,
    Uuid ContributionId,
    string Status,
    long Revision,
    Uuid SourceRecordId,
    long SourceRecordVersion,
    MethodologyTemplateRecordContent ProposedContent,
    string DeidentificationRationale,
    Uuid ProposedByMemberId,
    string ProposedByDisplay,
    DateTimeOffset ProposedAt,
    MethodologyTemplateReviewView? Review,
    Uuid? AcceptanceId,
    string? AcceptanceRationale,
    Uuid? AcceptedByMemberId,
    string? AcceptedByDisplay,
    DateTimeOffset? AcceptedAt,
    Uuid ActorMemberId,
    string ActorDisplay,
    DateTimeOffset UpdatedAt,
    string ContentSha256);
