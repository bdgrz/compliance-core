using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Models;

/// <summary>
///     The frozen terms of one professional-duty change: the exact authored command, the signed assertion and its
///     verified scope, the predecessor fact and, for recording, the exact staff use the change needs. Every receiver
///     copies it; the signed validity interval is judged at <c>Header.AcceptedAt</c>, the declared decision instant.
/// </summary>
/// <remarks>
///     <paramref name="PreviousAssertion" /> is the native reference of the designation fact that was current at
///     admission (a locator without broker positions until the stream-backed resolver is registrable).
/// </remarks>
public sealed record ProfessionalDutyChangeIntent(DurableIntentHeader Header, Uuid DesignationId,
    ProfessionalDutyChangeKind ChangeKind, ImmutableContentRef AuthoredChange, string SignedAssertion,
    Uuid VerifiedAssertionId, ulong AuthorityRevision, ImmutableContentRef VerifiedScope,
    NativeFactReference? PreviousAssertion, SourceUseRequirement? StaffEligibility)
{
    /// <summary>Schema of the content written to the immutable store.</summary>
    public const string Schema = "professional-duty-change-intent.v1";
}
