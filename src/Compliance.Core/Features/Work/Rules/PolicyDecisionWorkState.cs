using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Models;
namespace Bdgrz.Compliance.Features.Work.Rules;

public sealed record PolicyDecisionWorkState(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    long SourceRevision, AccountableWorkItemView[] Items);
