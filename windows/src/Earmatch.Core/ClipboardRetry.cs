using System.Runtime.InteropServices;

namespace Earmatch.Core;

public static class ClipboardRetry
{
    public const int Attempts = 3;

    public static bool TryWrite(Action write, Action pause)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                write();
                return true;
            }
            catch (ExternalException)
            {
                if (attempt < Attempts) pause();
            }
        }
        return false;
    }
}
