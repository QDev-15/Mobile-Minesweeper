namespace Minesweeper.Services;

/// <summary>Stops a Clicked/Tapped handler from re-entering while its own previous call (almost always an
/// `await Shell.Current.GoToAsync(...)`, a Play Billing purchase, or similar) is still running (owner's
/// report, 2026-10-20: "các action không có spinner nên khi máy chậm tôi click nhiều nó bị crash... hoặc nó
/// bị mở quá nhiều lần" -- a slow device plus no loading feedback meant a real double/triple-tap fired the
/// SAME handler again before the first call returned, pushing the same page onto the Shell nav stack more
/// than once, or racing two PurchaseProAsync/RestoreAsync calls against each other). One instance per page,
/// shared by every Clicked/Tapped handler on it: once ANY of that page's actions is in flight, every other
/// one is blocked too, not just the one most recently tapped -- correct, since navigating away twice at once
/// (e.g. tap "Dễ" then immediately "Vừa") is just as broken as tapping the same button twice.</summary>
public sealed class ActionGuard
{
    private bool _busy;

    /// <summary>Call at the very start of a handler. False means a previous call from this same guard is
    /// still running -- return immediately without doing anything. True means it's safe to proceed; the
    /// caller must call <see cref="Exit"/> (typically in a finally block) once its own work is done.
    /// <paramref name="view"/>, if given, is also disabled for the duration -- the tapped button visibly
    /// stops responding instead of silently doing nothing, which is the actual feedback the owner asked for
    /// (a full spinner on every button in the app is a much bigger change for the same practical result).</summary>
    public bool TryEnter(View? view = null)
    {
        if (_busy) return false;
        _busy = true;
        if (view != null) view.IsEnabled = false;
        return true;
    }

    public void Exit(View? view = null)
    {
        _busy = false;
        if (view != null) view.IsEnabled = true;
    }
}
