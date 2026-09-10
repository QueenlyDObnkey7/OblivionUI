using System.Globalization;

namespace OblivionUI;

public enum NotificationSide { Left, Right }

/// <summary>Client HUD shared with OblivionInteraction. All methods may be called from any thread.</summary>
public static class Hud
{
    internal static readonly object Gate = new();
    internal static readonly NotificationQueue Queue = new();
    internal static string Prompt = "";
    public static bool IsReady { get; internal set; }
    /// <summary>Display a prompt; null clears only the prompt, preserving notifications.</summary>
    public static void SetPrompt(string? text) { lock (Gate) Prompt = text ?? ""; }
    internal static void Reset() { lock (Gate) { Prompt = ""; Queue.Clear(); } }
}

public static class Notifications
{
    /// <summary>Queue a parchment notice at 20% viewport height. Duration excludes the slide transitions.
    /// The oldest waiting notice is dropped when the 16-notice queue is full. Dispose to cancel.</summary>
    public static IDisposable Show(string title, string body = "", NotificationSide side = NotificationSide.Right, double duration = 4)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(body);
        if (!Enum.IsDefined(side)) throw new ArgumentOutOfRangeException(nameof(side));
        if (!double.IsFinite(duration) || duration < 1 || duration > 30) throw new ArgumentOutOfRangeException(nameof(duration), "Use 1–30 seconds.");
        lock (Hud.Gate) return Hud.Queue.Add(Clean(title,64),Clean(body,180),side,duration);
    }
    private static string Clean(string text, int limit)
    {
        text = text.Replace('|',' ').Replace('\r',' ').Replace('\n',' ').Replace('\t',' ').Trim();
        var elements = StringInfo.ParseCombiningCharacters(text);
        return elements.Length <= limit ? text : text[..elements[limit-1]] + "…";
    }
    public static void Clear() { lock (Hud.Gate) Hud.Queue.Clear(); }
}

internal sealed class NotificationQueue
{
    private sealed class Notice(string title, string body, NotificationSide side, double duration) : IDisposable
    {
        public readonly string Title = title, Body = body;
        public readonly NotificationSide Side = side;
        public readonly double Duration = duration;
        public volatile bool Cancelled;
        public void Dispose() => Cancelled = true;
    }
    private readonly Queue<Notice> _waiting = new();
    private Notice? _active;
    private double _start;
    internal IDisposable Add(string title, string body, NotificationSide side, double duration)
    {
        if (_waiting.Count >= 16) _waiting.Dequeue();
        var notice = new Notice(title,body,side,duration); _waiting.Enqueue(notice); return notice;
    }
    internal (string Left, string Right) Frame(double now)
    {
        if (_active is { } old && (old.Cancelled || now-_start >= old.Duration+.64)) _active = null;
        while (_active is null && _waiting.TryDequeue(out var next))
            if (!next.Cancelled) { _active=next; _start=now; }
        if (_active is not { } item) return ("","");
        double age=now-_start;
        double t = age < .32 ? 1-age/.32 : age > item.Duration+.32 ? (age-item.Duration-.32)/.32 : 0;
        t=Math.Clamp(t,0,1); t=t*t*(3-2*t);
        double offset=380*t*(item.Side==NotificationSide.Left?-1:1);
        string packet=FormattableString.Invariant($"@oui|{offset:F1}|{item.Title}|{item.Body}");
        return item.Side==NotificationSide.Left ? (packet,"") : ("",packet);
    }
    internal void Clear() { _waiting.Clear(); _active=null; }
}
