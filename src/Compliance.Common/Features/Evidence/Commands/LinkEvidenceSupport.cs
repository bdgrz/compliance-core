using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Programs.Contracts;
using Bdgrz.Compliance.Features.Retention.Commands;
namespace Bdgrz.Compliance.Features.Evidence.Commands;

/// <summary>Links an exact artifact to the program record and period it supports.</summary>
[Discriminator("bdgrz.evidence.support.link", 1)]
public sealed record LinkEvidenceSupport(Uuid CommandId, Uuid TenantId, Uuid ProgramId, Uuid ArtifactId,
    string SupportedRecordType, Uuid SupportedRecordId, long SupportedRecordRevision,
    DateOnly PeriodStart, DateOnly PeriodEnd, string Purpose, string SufficiencyRationale, long ExpectedRevision)
    : IRequest<EvidenceSupportView>, IProgramScopedRequest, IClientManagementMutationRequest,
        ICallable;
