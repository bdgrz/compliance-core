using Bdgrz.Compliance.Features.NativeOperations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.UserIdentities;

/// <summary>Retains one finalized target cut across asynchronous native response preparation and append.</summary>
sealed class IdentityOwnerPreparation(Aggregate? initial = null)
{
    IdentityOwnerPreparationCut? _cut = initial is null ? null : IdentityOwnerPreparationCut.Of(initial);

    public Result Check(Aggregate owner) => _cut?.Require(owner) ?? Result.Success;

    public Result<FirstResponseDescriptor> Prepare(Aggregate owner, Result guard, FirstResponseDescriptor response)
    {
        var same = Check(owner);
        if (!same.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(same.Error!);
        if (!guard.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(guard.Error!);
        _cut = IdentityOwnerPreparationCut.Of(owner);
        return Result<FirstResponseDescriptor>.Success(response);
    }

    public async ValueTask<Result<FirstResponseDescriptor>> PrepareAsync(Aggregate owner, Result guard,
        Func<CancellationToken, Task<FirstResponseDescriptor>> response, CancellationToken ct)
    {
        var same = Check(owner);
        if (!same.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(same.Error!);
        if (!guard.IsSuccess)
            return Result<FirstResponseDescriptor>.Failure(guard.Error!);
        _cut = IdentityOwnerPreparationCut.Of(owner);
        return Result<FirstResponseDescriptor>.Success(await response(ct).ConfigureAwait(false));
    }

    public Result Apply(Aggregate owner, Func<Result> append)
    {
        var current = (_cut ?? throw new InvalidOperationException("An identity append has no prepared owner cut.")).Require(owner);
        return current.IsSuccess ? append() : current;
    }
}
