using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Queries;

/// <summary>Lists the program policy inventory, including policies with retired status.</summary>
[Discriminator("bdgrz.policies.list", 1)]
public sealed record ListPolicies(Uuid TenantId, Uuid ProgramId, int? Limit = null,
    string? Cursor = null)
    : IRequest<Page<PolicySummaryView>>, IProgramReadRequest, ICallable;
