using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
namespace Bdgrz.Compliance.Features.NativeOperations.Models;

/// <summary>Closed outcome of a per-first-owner native acceptance.</summary>
public abstract record AcceptedAppendResult
{
    private protected AcceptedAppendResult()
    {
    }

    /// <summary>Conflict error returned for a logical id bound to different intent.</summary>
    public static RequestError ConflictError { get; } = new(RequestErrorKind.Conflict,
        "This operation identity already has different accepted intent.", false);

    /// <summary>The batch committed; the next position is the exclusive append frontier of the first owner.</summary>
    public sealed record Committed(NativeFactReference FirstFact, ulong NextPhysicalPositionExclusive,
        ImmutableContentRef FirstResponse, FirstResponseDescriptor Response) : AcceptedAppendResult;

    /// <summary>The binding was already accepted with equal fields; nothing was appended.</summary>
    public sealed record Replayed(NativeFactReference FirstFact, ImmutableContentRef FirstResponse,
        FirstResponseDescriptor Response) : AcceptedAppendResult;

    /// <summary>The logical id is bound to different intent on this owner; nothing was appended.</summary>
    public sealed record Conflict : AcceptedAppendResult
    {
        /// <summary>The single conflict instance.</summary>
        public static Conflict Instance { get; } = new();
    }
}
