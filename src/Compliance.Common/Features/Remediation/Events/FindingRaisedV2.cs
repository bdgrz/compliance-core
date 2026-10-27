using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation.Events;

/// <summary>
///     The root native fact of a finding. <paramref name="LinksManifest" /> is the complete original link list as
///     verified immutable content, so a large list is never an event-size rejection.
/// </summary>
[Discriminator("bdgrz.finding.raised", 3)]
public sealed record FindingRaisedV2(Uuid RequestId, Uuid TenantId, Uuid ProgramId, Uuid FindingId, long Revision,
    FindingSource Source, string Title, string Description, string Severity,
    string AffectedScope, Uuid OwnerMemberId, DateOnly DueOn, ImmutableContentRef LinksManifest,
    ActorReference RaisedBy, DateTimeOffset RaisedAt, string CreationKind, string? InitialActionSha256,
    OperationProvenance Operation, ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult)
    : DomainEvent, INativeOperationFact;
