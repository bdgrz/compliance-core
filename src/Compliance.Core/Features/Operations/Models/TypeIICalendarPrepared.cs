using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Operations.Models;

/// <summary>One exact immutable before/after cut and its transient terminal membership evidence.</summary>
public sealed record TypeIICalendarPrepared(TypeIICalendarAuthority Before, TypeIICalendarAuthority After,
    ImmutableContentRef Authority, IReadOnlyList<TypeIICalendarHistoryRead> History,
    TypeIICalendarHistoryNode? NewHistory, object Change);
