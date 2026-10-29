using Cntryl.Portia;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
namespace Bdgrz.Compliance.Features.Tenants.Queries;

[Discriminator("bdgrz.platform.operator.list", 1)]
public sealed record ListPlatformOperators() : IRequest<PlatformOperatorRosterView>, ICallable,
    IPlatformOperatorRequest;
