using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>One unpublished preparation is valid only at the exact source prefix it verified.</summary>
sealed record ExaminationEngagementPreparedCut(EventStreamAddress Owner, ulong Version, ulong Position, string Digest)
{
    public static ExaminationEngagementPreparedCut Capture(ExaminationEngagement owner) =>
        new(owner.Stream, owner.Version, owner.CommittedStreamPosition, owner.ReplayCut.Digest);
    public bool Matches(ExaminationEngagement owner) => owner.Stream == Owner && owner.Version == Version &&
        owner.CommittedStreamPosition == Position && owner.ReplayCut.Digest == Digest;
}
