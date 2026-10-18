using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Records the record significant change intent required by the owning feature workflow.</summary>
[Discriminator("bdgrz.changes.record-significant-change", 2)]
public sealed record RecordSignificantChange(Uuid TenantId, Uuid ProgramId, Uuid ChangeId, SignificantChangeContent Content) : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
