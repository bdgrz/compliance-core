using Cntryl.Portia;
using Cntryl.Fitz.Extensions;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.AuditorRequests.Queries;

/// <summary>Reads the feature state required by the linked product workflow.</summary>
[Discriminator("bdgrz.auditor-requests.list-request-responses", 1)]
public sealed record ListRequestResponses(Uuid TenantId, Uuid ProgramId, Uuid AuditorRequestId, int? Limit = null, string? Cursor = null)
    : IRequest<Page<AuditorRequestResponseView>>, IProgramReadRequest, ICallable;
