using Cntryl.Fitz.Extensions;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.Policies.Models;
using Bdgrz.Compliance.Features.Programs.Contracts;
namespace Bdgrz.Compliance.Features.Policies.Queries;

/// <summary>Lists every retained immutable version, including superseded and retired versions.</summary>
[Discriminator("bdgrz.policy.versions.list", 1)]
public sealed record ListPolicyVersions(Uuid TenantId, Uuid ProgramId, Uuid PolicyId,
    int? Limit = null, string? Cursor = null)
    : IRequest<Page<PolicyVersionView>>, IProgramReadRequest, ICallable;
