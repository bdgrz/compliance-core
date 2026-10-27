using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;

namespace Bdgrz.Compliance.Features.SystemDescription;

/// <summary>Exact verified narrative version and authoritative native production/approval proofs.</summary>
public sealed record SystemDescriptionVersionSource(SystemDescriptionVersionView Version, ImmutableContentRef Content,
    NativeFactReference Source, NativeFactReference? ApprovalSource, SystemDescriptionApprovalView? Approval,
    long NativeSequence, ulong PhysicalPosition);
