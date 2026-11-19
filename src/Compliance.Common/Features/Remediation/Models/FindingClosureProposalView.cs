using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The exact current closure proposal revision awaiting personal review.</summary>
public sealed record FindingClosureProposalView(Uuid ClosureId, long ProposalRevision, FindingClosureTerms Terms,
    IReadOnlyList<Uuid> ActionsReliedOn, ActorReference ProposedBy, DateTimeOffset ProposedAt, IReadOnlyList<Uuid>? Authors = null);
