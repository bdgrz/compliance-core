using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.MethodologyTemplates.Models;

/// <summary>Tenant-owned adopt, adapt, or decline decision for an exact successor.</summary>
public sealed record TemplateApplicationSuccessorDecisionView(
    Uuid TemplateVersionId,
    long TemplateVersionNumber,
    string Disposition,
    string Rationale,
    Uuid DecidedByMemberId,
    string DecidedByDisplay,
    DateTimeOffset DecidedAt);
