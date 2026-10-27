using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Work.Models;

/// <summary>One exact attributed change and the source identity it was authorized against; no cumulative history.</summary>
public sealed record WorkAssignmentChangeContent(WorkAssignmentSourceIdentity Source, WorkAssignmentEntryView Entry,
    DateOnly SelectionDate, Cntryl.Portia.Uuid? EffectiveAssigneeMemberId)
{
    public const string Schema = "work-assignment-change.v2";
}
