using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     One accepted lifecycle change of a service engagement draft: create, amend, propose, withdraw or close.
///     <paramref name="Intent" /> is the stable command kind and <paramref name="Change" /> is the immutable
///     <see cref="ServiceEngagementChange" />; the complete resulting view is the operation result. No cumulative
///     parent snapshot is carried. <paramref name="Revision" /> is the engagement revision after the change.
/// </summary>
[Discriminator("bdgrz.service-engagement.changed", 1)]
public sealed record ServiceEngagementMutationRecorded(Uuid TenantId, Uuid OperationId, string Intent,
    ImmutableContentRef Change, OperationProvenance Operation, ulong Revision, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
