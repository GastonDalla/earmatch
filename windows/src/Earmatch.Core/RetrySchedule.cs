namespace Earmatch.Core;

public sealed class RetrySchedule
{
    public enum Reason
    {
        Failure,
        PermissionDenied,
        WindowOffScreen,
    }

    public readonly record struct Timer(double Delay, double FireTime);

    public const double FailureDelay = 1.0;
    public const double PermissionDelay = 2.0;
    public const double OffScreenDelayMin = 0.1;
    public const double OffScreenDelayMax = 2;

    private double? _armedFireTime;
    private double _nextAttempt = double.NegativeInfinity;
    private int _offScreenLooks;

    public Timer? Retry(Reason reason, double at)
    {
        double delay;
        switch (reason)
        {
            case Reason.Failure:
                delay = FailureDelay;
                break;
            case Reason.PermissionDenied:
                delay = PermissionDelay;
                break;
            default:
                delay = Math.Min(OffScreenDelayMin * (1 << Math.Min(_offScreenLooks, 16)), OffScreenDelayMax);
                _offScreenLooks++;
                break;
        }
        var fireTime = at + delay;
        if (_armedFireTime is { } armed && armed <= fireTime)
        {
            _nextAttempt = armed;
            return null;
        }
        _nextAttempt = fireTime;
        _armedFireTime = fireTime;
        return new Timer(delay, fireTime);
    }

    public bool AllowsAttempt(double at) => at >= _nextAttempt;

    public void AttemptStarted() => _armedFireTime = null;

    public void WindowFound() => _offScreenLooks = 0;

    public void Reset() => _offScreenLooks = 0;

    public bool Fire(Timer timer)
    {
        if (_armedFireTime != timer.FireTime) return false;
        _armedFireTime = null;
        return true;
    }
}
