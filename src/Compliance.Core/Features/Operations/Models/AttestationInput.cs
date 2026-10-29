using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>The performer-supplied content of one attestation version.</summary>
public sealed record AttestationInput(string Result, DateTimeOffset PerformedAt,
    DateOnly? CoveredFrom, DateOnly? CoveredUntil, string? Notes, string? Rationale,
    IReadOnlyList<EvidenceReference> Evidence, OperatingHolder PerformedBy);
