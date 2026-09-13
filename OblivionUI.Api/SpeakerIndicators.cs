using System.Diagnostics;
namespace OblivionUI;

/// <summary>Speaking icons rendered by OblivionUI. Publish regularly from a client update system.</summary>
public static class SpeakerIndicators
{
    private static readonly object Gate=new();
    private static bool _local;
    private static string[] _players=[];
    private static long _published;
    /// <summary>Player IDs use SDK PlayerId.ToString(). Remote icons require a visible, non-sneaking pawn.
    /// State expires after one second without refresh. This does not change voice audio.</summary>
    public static void Publish(bool localSpeaking,IEnumerable<string> speakingPlayerIds)
    {
        ArgumentNullException.ThrowIfNull(speakingPlayerIds);
        var ids=speakingPlayerIds.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(64).ToArray();
        lock(Gate) { _local=localSpeaking; if(!_players.AsSpan().SequenceEqual(ids))_players=ids; _published=Stopwatch.GetTimestamp(); }
    }
    public static void Clear() {lock(Gate) {_local=false;_players=[];_published=0;}}
    internal static (bool Local,string[] Players) Snapshot()
    {
        lock(Gate) return _published==0 || Stopwatch.GetElapsedTime(_published).TotalSeconds>1 ? (false,[]) : (_local,_players);
    }
}

internal static class SpeakerVisibility
{
    internal static bool Eligible(bool valid,bool self,bool sneaking,bool alive,bool speaking)
        => valid && !self && !sneaking && alive && speaking;
    internal static bool OnScreen(bool projected,double x,double y,double width,double height)
        => projected && double.IsFinite(x) && double.IsFinite(y) && width>0 && height>0
            && x>=18 && y>=18 && x<=width-18 && y<=height-18;
}
