using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Queries;

[Discriminator("bdgrz.platform.operator.list", 1)]
public sealed record ListPlatformOperators() : IRequest<PlatformOperatorRosterView>, ICallable,
    IPlatformOperatorRequest;
