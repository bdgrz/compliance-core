using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Examinations.Models;

/// <summary>Current scalar engagement authority; terms, contacts, sources and history remain immutable content.</summary>
public sealed record ExaminationEngagementHeader(Uuid ProgramId, string EngagementType, Uuid BoundaryId,
    string Status, long Revision, Uuid? BaselineId, long BaselineRevision);
