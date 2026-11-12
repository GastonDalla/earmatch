using System.Runtime.InteropServices;

namespace Earmatch.Core;

/// Writing to the Windows clipboard fails while another app has it open; Copy Diagnostics tries a few times and says
/// whether the report got there, so it never shows "Copied" for a copy that failed.
public static class ClipboardRetry
{
    public const int Attempts = 3;

    /// Runs `write` until it works or `Attempts` tries failed with a clipboard error (an ExternalException, which the
    /// WPF clipboard throws as a COMException), pausing between tries. Other exceptions are not clipboard trouble and
    /// propagate.
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
