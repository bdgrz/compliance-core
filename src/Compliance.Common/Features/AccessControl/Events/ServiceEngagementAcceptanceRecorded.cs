using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The engagement was accepted: every temporary grant was authorized irreversibly and every permanent practice
///     wall was committed before this fact. <paramref name="AcceptanceContent" /> is the complete original
///     <see cref="ServiceEngagementAcceptanceView" />, and <paramref name="Revision" /> is the engagement revision after
///     it. <paramref name="OperationResult" /> is the terminal result of the acceptance operation.
/// </summary>
[Discriminator("bdgrz.service-engagement.acceptance-recorded", 1)]
public sealed record ServiceEngagementAcceptanceRecorded(Uuid TenantId, Uuid OperationId, string Intent,
    ImmutableContentRef AcceptanceContent, OperationProvenance Operation, ulong Revision,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
