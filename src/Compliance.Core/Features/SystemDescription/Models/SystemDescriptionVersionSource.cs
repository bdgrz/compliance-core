using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Features.SystemDescription.Models;

/// <summary>Exact verified narrative version and authoritative native production/approval proofs.</summary>
public sealed record SystemDescriptionVersionSource(SystemDescriptionVersionView Version, ImmutableContentRef Content,
    NativeFactReference Source, NativeFactReference? ApprovalSource, SystemDescriptionApprovalView? Approval,
    long NativeSequence, ulong PhysicalPosition);
