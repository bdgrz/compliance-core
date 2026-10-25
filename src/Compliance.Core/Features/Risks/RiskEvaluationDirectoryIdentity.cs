using Cntryl.Portia;
namespace Bdgrz.Compliance.Features.Risks;

public sealed record RiskEvaluationDirectoryIdentity(Uuid TenantId, Uuid ProgramId, Uuid RiskId, long Revision, long NativeSequence);
