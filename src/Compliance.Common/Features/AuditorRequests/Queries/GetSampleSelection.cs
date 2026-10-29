using Cntryl.Portia;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.auditor-requests.get-sample-selection", 1)]
public sealed record GetSampleSelection(Uuid TenantId, Uuid ProgramId, Uuid SampleSelectionId)
    : IRequest<SampleSelectionView>, IProgramReadRequest, ICallable;
