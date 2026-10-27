using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.NativeOperations;

[Discriminator("test.native-http.child", 1)]
sealed record NativeHttpChildRequest(Uuid CommandId) : IRequest<UnitResult>, IIdentifiedRequest
{
    public Uuid RequestId => CommandId;
}
