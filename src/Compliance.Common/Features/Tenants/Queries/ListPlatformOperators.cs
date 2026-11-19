using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Cntryl.Fitz.Extensions;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Queries;

/// <summary>Lists active platform operators. The directory may lag the source streams.</summary>
[Discriminator("bdgrz.platform.operator.list", 1)]
public sealed record ListPlatformOperators(int? Limit = null, string? Cursor = null)
    : IRequest<Page<PlatformOperatorDirectoryEntry>>, ICallable, IPlatformOperatorRequest;
