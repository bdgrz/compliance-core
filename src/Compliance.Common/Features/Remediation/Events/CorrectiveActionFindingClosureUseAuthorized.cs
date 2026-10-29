using Cntryl.Portia;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     A completed corrective action is held for a finding closure. The use does not advance the public action
///     revision: <paramref name="Revision" /> is the held revision, and <paramref name="Generation" /> is the finding
///     generation the closure seals.
/// </summary>
[Discriminator("bdgrz.corrective-action.corrective-action-finding-closure-use-authorized", 1)]
public sealed record CorrectiveActionFindingClosureUseAuthorized(EventStreamAddress Owner, long Revision,
    long Generation, ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, ISourceUseFact;
