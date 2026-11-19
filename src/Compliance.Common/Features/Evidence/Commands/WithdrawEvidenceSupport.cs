using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Programs.Contracts;

namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Withdraws a mistaken support assertion without changing its original content.</summary>
[Discriminator("bdgrz.evidence.support.withdraw", 1)]
public sealed record WithdrawEvidenceSupport(Uuid CommandId, Uuid TenantId, Uuid ProgramId,
    Uuid SupportId, long ExpectedRevision, string Reason)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
