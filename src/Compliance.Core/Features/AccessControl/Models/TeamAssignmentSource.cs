using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     A live team assignment proven by native facts: the member, the membership episode it was made for, the
///     assignment fact itself, the assignment generation and the owner revision after its last fact.
/// </summary>
/// <param name="MemberId">The assigned member.</param>
/// <param name="MembershipEpisodeId">The membership episode the assignment was made for.</param>
/// <param name="AssignmentEventId">The assignment fact that opened this episode.</param>
/// <param name="Generation">The number of assignments ever made on the owner, including this one.</param>
/// <param name="Revision">The owner revision after its last fact.</param>
sealed record TeamAssignmentSource(Uuid MemberId, Uuid MembershipEpisodeId, Uuid AssignmentEventId, ulong Generation,
    ulong Revision);
