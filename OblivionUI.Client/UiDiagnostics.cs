using System.Diagnostics;
namespace OblivionUI.Client;

// Low-frequency summaries distinguish script CPU cost from rendering/network FPS.
internal static class UiDiagnostics
{
    internal static Action<string>? Log;
    private static long _start,_mark,_window;
    private static int _frames;
    private static double _total,_peak;
    private static readonly double[] Phases=new double[3];
    internal static void Write(string message)=>Log?.Invoke(message);
    internal static void Begin(){_start=_mark=Stopwatch.GetTimestamp();if(_window==0)_window=_start;}
    internal static void Mark(int phase){long end=Stopwatch.GetTimestamp();Phases[phase]+=Stopwatch.GetElapsedTime(_mark,end).TotalMilliseconds;_mark=end;}
    internal static void End()
    {
        double elapsed=Stopwatch.GetElapsedTime(_start).TotalMilliseconds;_frames++;_total+=elapsed;_peak=Math.Max(_peak,elapsed);
        if(Stopwatch.GetElapsedTime(_window).TotalSeconds<30)return;
        Write(FormattableString.Invariant($"[OblivionUI] Script timing: updates={_frames}, mean={_total/_frames:F3}ms, peak={_peak:F3}ms; HUD={Phases[0]/_frames:F3}ms, book={Phases[1]/_frames:F3}ms, speakers={Phases[2]/_frames:F3}ms. CPU update time only, not FPS."));
        _window=Stopwatch.GetTimestamp();_frames=0;_total=_peak=0;Array.Clear(Phases);
    }
}
