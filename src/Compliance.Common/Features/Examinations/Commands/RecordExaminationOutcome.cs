using Bdgrz.Compliance.Features.Programs;
using Bdgrz.Compliance.Features.AccessControl;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Records an auditor-supplied result against one exact delivered package version.</summary>
[Discriminator("bdgrz.examinations.record-examination-outcome", 2)]
public sealed record RecordExaminationOutcome(
    Uuid TenantId,
    Uuid ProgramId,
    Uuid ExaminationId,
    Uuid OutcomeId,
    Uuid PackageId,
    int PackageVersion,
    string OutcomeCode,
    string OutcomeSummary,
    string IssuedReportReference,
    Uuid? ReportArtifactId,
    DateOnly IssuedOn)
    : IRequest, IProgramScopedRequest, IClientManagementMutationRequest, ICallable;
