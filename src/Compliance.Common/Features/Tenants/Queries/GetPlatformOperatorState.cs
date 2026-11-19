using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Queries;

/// <summary>Reads exact per-user and primary-owner state to prepare conditional operator changes.</summary>
[Discriminator("bdgrz.platform.operator.state.get", 1)]
public sealed record GetPlatformOperatorState(Uuid UserId)
    : IRequest<PlatformOperatorStateView>, ICallable, IPlatformOperatorRequest;
