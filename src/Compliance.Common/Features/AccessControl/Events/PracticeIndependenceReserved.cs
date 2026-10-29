using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Commands;
using Bdgrz.Compliance.Features.SourceUse.Contracts;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.AccessControl.Events;

/// <summary>
///     A temporary exclusive reservation of one practice subject for one acceptance or staff assignment. It grants no
///     permanent authority: the wall exists only once <see cref="PracticeIndependenceCommitted" /> follows the
///     irreversible authorization. <paramref name="Revision" /> is the committed revision held, and the practice and
///     engagement are carried because the owner's reducer cannot read immutable content.
/// </summary>
[Discriminator("bdgrz.practice-independence-commitment.practice-independence-reserved", 1)]
public sealed record PracticeIndependenceReserved(EventStreamAddress Owner, long Revision, long Generation,
    ImmutableContentRef Intent, ImmutableContentRef? SourceProofs, OperationProvenance Operation,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult, string Practice, Uuid EngagementId)
    : DomainEvent, ISourceUseFact;
