namespace Bdgrz.Compliance.Hosting.NativeResponses;

/// <summary>Identifies the exact top-level request type whose generated endpoint has the native result adapter.</summary>
public sealed record NativeFirstResponseEndpoint(Type RequestType);
