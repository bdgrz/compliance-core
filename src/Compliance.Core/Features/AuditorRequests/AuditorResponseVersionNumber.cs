namespace Bdgrz.Compliance.Features.AuditorRequests;

static class AuditorResponseVersionNumber
{
    public static bool TryGet(long value, out int version)
    {
        version = 0;
        if (value is <= 0 or > int.MaxValue)
            return false;
        version = (int)value;
        return true;
    }
}
