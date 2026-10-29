using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.AuditorRequests.Models;

/// <summary>
///     The canonical authored content of one admitted response draft: the normalized summary and evidence, whether the
///     request required an independent internal review, and the original author. The response owner never loads the
///     live request, so the review policy is frozen here at admission.
/// </summary>
public sealed record AuditorResponseDraftContent(Uuid ProgramId, Uuid RequestId, Uuid ResponseId, string Summary,
    IReadOnlyList<Uuid> EvidenceArtifactIds, bool RequiresInternalReview, Uuid AuthorMemberId, string AuthorDisplay,
    DateTimeOffset At);
