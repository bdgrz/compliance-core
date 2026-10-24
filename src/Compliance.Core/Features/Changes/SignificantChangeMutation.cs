namespace Bdgrz.Compliance.Features.Changes;

/// <summary>Immutable proof of the exact before body and one verified business transition.</summary>
public sealed record SignificantChangeMutation(string Kind, string? BeforeSha256, string AfterSha256,
    SignificantChangeView After, bool Changed);
