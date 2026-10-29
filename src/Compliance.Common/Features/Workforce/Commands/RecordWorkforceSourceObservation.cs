using Cntryl.Portia;
using Bdgrz.Compliance.Features.Workforce.Contracts;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Workforce.Models;
namespace Bdgrz.Compliance.Features.Workforce.Commands;

/// <summary>Manually records one immutable source revision, explicitly correlated to a governed record.</summary>
[Discriminator("bdgrz.workforce.source.record", 1)]
public sealed record RecordWorkforceSourceObservation(Uuid TenantId, WorkforceSourceIdentity Source,
    string TargetKind, Uuid TargetId, long ExpectedTargetRevision, WorkforceSourceFacts Facts,
    DateTimeOffset ObservedAt) : IRequest<WorkforceSourceRegistration>, IWorkforceRequest, IClientManagementMutationRequest, ICallable;
