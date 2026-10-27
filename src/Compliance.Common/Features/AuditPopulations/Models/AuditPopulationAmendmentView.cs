using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations.Models;

/// <summary>Attributable link from a successor population version to changed stable rows.</summary>
public sealed record AuditPopulationAmendmentView(
    int SupersededVersion,
    int SuccessorVersion,
    string Reason,
    IReadOnlyList<Uuid> ChangedRowIds,
    string SupersededContentSha256,
    Uuid AmendedByMemberId,
    string AmendedByDisplay,
    DateTimeOffset AmendedAt);
