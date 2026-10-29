using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AccessReviews.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.NativeOperations.Contracts;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.AccessReviews.Events;

/// <summary>
///     An explicit classification decision for one exact accepted principal; earlier decisions remain in history.
///     The decision number is the position of this fact in the principal's own stream.
///     <paramref name="DecisionContent" /> is the complete <see cref="AccessPrincipalClassificationView" />.
/// </summary>
[Discriminator("bdgrz.access_population.principal_classified", 2)]
public sealed record AccessPrincipalClassified(Uuid TenantId, Uuid PopulationId, string ProviderSubjectId,
    ImmutableContentRef DecisionContent, OperationProvenance Operation, ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
