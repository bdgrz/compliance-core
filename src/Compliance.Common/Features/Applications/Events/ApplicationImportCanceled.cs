using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Applications.Events;

/// <summary>The batch is canceled: every target was fenced and nothing was published.</summary>
[Discriminator("bdgrz.application_import.canceled", 2)]
public sealed record ApplicationImportCanceled(Uuid TenantId, Uuid BatchId, long Revision,
    string Reason, Uuid ActorMemberId, string ActorDisplay,
    DateTimeOffset CanceledAt, OperationProvenance Operation, ImmutableContentRef Intent,
    ImmutableContentRef? FirstResponse, ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
