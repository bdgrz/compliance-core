using Cntryl.Portia;
using Bdgrz.Compliance.Features.NativeOperations.Models;
namespace Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;

[Discriminator("test.native-http.probe", 1)]
sealed record NativeHttpProbeRequest(Uuid CommandId) : IRequest<UnitResult>, IIdentifiedRequest
{
    public Uuid RequestId => CommandId;
}
