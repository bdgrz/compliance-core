using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Features.Remediation.Models;

/// <summary>The permanent mismatch bound to one exact closure admission, intent, root and generation.</summary>
public sealed record FindingClosureFailureProof(Uuid ProgramId, Uuid FindingId, string Reason, Uuid? ActionId,
    NativeFactReference Admission, ImmutableContentRef Intent, Uuid RootOperationId, long Generation);
