using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Rules;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Identifies the exact top-level request type whose generated endpoint has the native result adapter.</summary>
public sealed record NativeFirstResponseEndpoint(Type RequestType);
