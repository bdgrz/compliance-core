using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

[Discriminator("test.native-http.child", 1)]
sealed record NativeHttpChildRequest(Uuid CommandId) : IRequest<UnitResult>, IIdentifiedRequest
{
    public Uuid RequestId => CommandId;
}
