using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>
///     Records every send of an invitation. It can fail the first send as a provider rejection, or cancel the worker
///     right after the first send is accepted, which is how a crash between the send and its stored result looks.
/// </summary>
sealed class RecordingInvitationDelivery : ITenantInvitationDelivery
{
    public List<(Uuid AttemptId, Uuid TenantId, string EmailAddress, string Token)> Sends { get; } = [];

    public bool FailFirst { get; init; }

    public CancellationTokenSource? CancelAfterFirstSend { get; set; }

    public ValueTask SendAsync(Uuid attemptId, Uuid tenantId, string emailAddress, string token, CancellationToken ct)
    {
        Sends.Add((attemptId, tenantId, emailAddress, token));
        if (FailFirst && Sends.Count == 1)
            throw new InvalidOperationException($"provider secret {token} for {emailAddress}");
        if (CancelAfterFirstSend is { } cancellation && Sends.Count == 1)
        {
            cancellation.Cancel();
            ct.ThrowIfCancellationRequested();
        }

        return ValueTask.CompletedTask;
    }
}
