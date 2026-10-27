using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>
///     Records accepted provenance after canonical facts match, or dismisses conflicting facts with
///     an attributable note. This personal decision is HTTP-only. Never changes canonical facts or
///     platform/provider access.
/// </summary>
[Discriminator("bdgrz.workforce.source.reconcile", 1)]
public sealed record ReconcileWorkforceSourceObservation(Uuid TenantId, Uuid ObservationId,
    long ExpectedRevision, long ExpectedTargetRevision, string Outcome, string Note)
    : IRequest, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
