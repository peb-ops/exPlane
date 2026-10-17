namespace ExPlane.Services;

/// <summary>Single source of truth for the pinned (always-on-top) state; button, hotkey and tray all go through here.</summary>
public sealed class TopmostService(bool initial)
{
    public bool IsPinned { get; private set; } = initial;

    public event EventHandler? Changed;

    public void Set(bool pinned)
    {
        if (pinned == IsPinned) return;
        IsPinned = pinned;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => Set(!IsPinned);
}
