using System.Diagnostics;
using OblivionMp.Sdk;
using ReadyM.Api.DI;
using ReadyM.Sdk.Common;
using ReadyM.Sdk.Common.Input;
using ReadyM.Modloader.Mods;

namespace OblivionUI.Client;

public sealed class Mod : ModBase
{
    public override string Name => "OblivionUI";
    protected override void RegisterServices(IDependencyContainer services) { }
    public override void Start()
    {
        Hud.IsReady=true;
        SDK.Input.RegisterKeyBind(ModifierKeys.Alt, Key.B, () => {
            if(Hud.IsReady && SDK.Sync.LocalPlayer is not null && SDK.Input.CanApplyInput()) BookDemo.Open();
        });
        SDK.Input.RegisterKeyBind(ModifierKeys.Alt, Key.N, () => {
            if (!Hud.IsReady || SDK.Sync.LocalPlayer is null || !SDK.Input.CanApplyInput()) return;
            Notifications.Show("Arena notice", "Your next challenge awaits.", NotificationSide.Left);
            Notifications.Show("Journal updated", "A new opportunity awaits in the Imperial City.", NotificationSide.Right);
        });
        Console.WriteLine("[OblivionUI] Parchment notifications ready. Alt+N previews left, then right.");
    }
    public override void DeInit()
    {
        BookRuntime.Reset(); Books.IsAvailable=false; Hud.IsReady=false; Hud.Reset(); UiSystem.Reset(); base.DeInit();
    }
}

public sealed class UiSystem : ModSystemBase
{
    private static readonly Stopwatch Clock=Stopwatch.StartNew();
    private static string? _last;
    private static double _nextFrame, _nextError;
    private static string? _player;
    protected override void OnUpdate(UpdateTick tick)
    {
        if (!Hud.IsReady || Clock.Elapsed.TotalSeconds < _nextFrame) return;
        double now=Clock.Elapsed.TotalSeconds; _nextFrame=now+1d/30;
        try
        {
            var local=SDK.Sync.LocalPlayer;
            string? player=local is { } me ? me.PlayerId.ToString() : null;
            if (player != _player) { if (_player is not null) Hud.Reset(); _player=player; }
            BookRuntime.Frame(player is not null);
            if (player is null || !SDK.Input.CanApplyInput()) { Hide(); return; }
            string prompt; (string Left,string Right) notice;
            lock (Hud.Gate) { prompt=Hud.Prompt; notice=Hud.Queue.Frame(now); }
            if (prompt.Length==0 && notice.Left.Length==0 && notice.Right.Length==0) { Hide(); return; }
            string frame=prompt+"\0"+notice.Left+"\0"+notice.Right;
            if (frame==_last) return;
            SDK.GameMessage.ShowGameMessage(prompt,notice.Left,notice.Right); _last=frame;
        }
        catch (Exception ex)
        {
            try { BookRuntime.Reset(); } catch { }
            if (now < _nextError) return; _nextError=now+10;
            Console.WriteLine($"[OblivionUI] HUD unavailable; retrying: {ex.Message}");
        }
    }
    private static void Hide() { if (_last is null) return; SDK.GameMessage.HideGameMessage(); _last=null; }
    internal static void Reset() { try { Hide(); } catch { } _last=null; _player=null; }
}

