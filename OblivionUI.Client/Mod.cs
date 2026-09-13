using System.Diagnostics;
using OblivionMp.Sdk;
using ReadyM.Api.DI;
using ReadyM.Sdk.Common;
using ReadyM.Sdk.Common.Input;
using ReadyM.Modloader.Mods;
using Microsoft.Extensions.Logging;

namespace OblivionUI.Client;

public sealed class Mod : ModBase
{
    public override string Name => "OblivionUI";
    protected override void RegisterServices(IDependencyContainer services) { }
    public override void Start()
    {
        UiDiagnostics.Log=message=>Logger.LogInformation("{Message}",message);
        Hud.IsReady=true;
        SDK.Input.RegisterKeyBind(ModifierKeys.Alt, Key.U, () => {
            UiDiagnostics.Write($"[OblivionUI] Alt+U received: ready={Hud.IsReady}, player={SDK.Sync.LocalPlayer is not null}, input={SDK.Input.CanApplyInput()}.");
            if(Hud.IsReady && SDK.Sync.LocalPlayer is not null && SDK.Input.CanApplyInput()) BookDemo.Open();
        });
        SDK.Input.RegisterKeyBind(ModifierKeys.Alt, Key.N, () => {
            if (!Hud.IsReady || SDK.Sync.LocalPlayer is null || !SDK.Input.CanApplyInput()) return;
            Notifications.Show("Arena notice", "Your next challenge awaits.", NotificationSide.Left);
            Notifications.Show("Journal updated", "A new opportunity awaits in the Imperial City.", NotificationSide.Right);
        });
        UiDiagnostics.Write("[OblivionUI] 0.9.4 started. Alt+U opens the UI demo; Alt+N previews notifications.");
    }
    public override void DeInit()
    {
        HudPanelRuntime.Reset(); SpeakerRuntime.Reset(); BookRuntime.Reset(); Books.IsAvailable=false; Hud.IsReady=false; Hud.Reset(); UiSystem.Reset(); base.DeInit();
    }
}

public sealed class UiSystem : ModSystemBase
{
    private static readonly Stopwatch Clock=Stopwatch.StartNew();
    private static (string Prompt,string Left,string Right)? _last;
    private static double _nextFrame, _nextError, _nextRetry;
    private static string? _player;
    protected override void OnUpdate(UpdateTick tick)
    {
        if (!Hud.IsReady || Clock.Elapsed.TotalSeconds < _nextFrame) return;
        double now=Clock.Elapsed.TotalSeconds; _nextFrame=now+1d/30;
        if(now<_nextRetry)return;
        UiDiagnostics.Begin();
        UnrealBookBridge.BeginFrame();
        try
        {
            var local=SDK.Sync.LocalPlayer;
            string? player=local is { } me ? me.PlayerId.ToString() : null;
            if (player != _player) { if (_player is not null) { Hud.Reset(); SpeakerRuntime.Reset(); } _player=player; UiDiagnostics.Write($"[OblivionUI] Update loop active; player available={player is not null}."); }
            ToolkitDemo.Tick(Environment.TickCount64/1000d);
            HudPanelRuntime.Frame(now,player is not null && SDK.Input.CanApplyInput() && Books.Current is not {Closed:false});
            UiDiagnostics.Mark(0);
            BookRuntime.Frame(player is not null);
            UiDiagnostics.Mark(1);
            bool modal; lock(Hud.Gate) modal=Books.Current is {Closed:false};
            SpeakerRuntime.Frame(now,player is not null && !modal && SDK.Input.CanApplyInput());
            UiDiagnostics.Mark(2);
            if (player is null || !SDK.Input.CanApplyInput()) { Hide(); return; }
            string prompt; (string Left,string Right) notice;
            lock (Hud.Gate) { prompt=Hud.Prompt; notice=Hud.Queue.Frame(now); }
            if (prompt.Length==0 && notice.Left.Length==0 && notice.Right.Length==0) { Hide(); return; }
            var frame=(prompt,notice.Left,notice.Right);
            if (frame==_last) return;
            SDK.GameMessage.ShowGameMessage(prompt,notice.Left,notice.Right); _last=frame;
        }
        catch (Exception ex)
        {
            _nextRetry=now+2;
            try { HudPanelRuntime.Reset(); SpeakerRuntime.Reset(); BookRuntime.Reset(); } catch { }
            if (now < _nextError) return; _nextError=now+10;
            UiDiagnostics.Write($"[OblivionUI] HUD update failed; retry delayed by two seconds: {ex}");
        }
        finally { UnrealBookBridge.EndFrame(); UiDiagnostics.End(); }
    }
    private static void Hide() { if (_last is null) return; SDK.GameMessage.HideGameMessage(); _last=null; }
    internal static void Reset() { try { Hide(); } catch { } _last=null; _player=null;_nextRetry=_nextError=_nextFrame=0; }
}
