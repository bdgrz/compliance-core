using Cntryl.Portia;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Features.Programs.Models;

/// <summary>Source-to-successor references for carried work and records.</summary>
public sealed record CarryForwardReferenceView(Uuid TenantId, Uuid ProgramId, Uuid CarryForwardReferenceId, Uuid SourceRecordId, Uuid SuccessorRecordId, string Status);
