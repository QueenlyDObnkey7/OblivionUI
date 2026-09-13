using OblivionMp.Sdk;
using OblivionMp.Sdk.Entities.Player;
using System.Reflection;

namespace OblivionUI.Client;

internal static class SpeakerRuntime
{
    private static UnrealBookBridge? _ue;
    private static nint _overlay,_host,_controller,_canvas,_local,_iconClass,_system,_layout,_library;
    private sealed class Icon
    {
        public nint Widget,Slot;
        public bool Desired;
        public byte Visibility=byte.MaxValue;
        public double X=double.NaN,Y=double.NaN;
    }
    private static readonly Dictionary<string,Icon> Icons=new(StringComparer.Ordinal);
    private static readonly HashSet<string> Speaking=new(StringComparer.Ordinal),Observed=new(StringComparer.Ordinal);
    private static readonly List<string> Removed=[];
    private static string[]? _speakingSnapshot;
    private static byte[]? _headName;
    private static byte _overlayVisibility=byte.MaxValue,_localVisibility=byte.MaxValue;
    private static double _nextRetry;

    internal static void Frame(double now,bool playable)
    {
        if(_ue is {} current && _overlay!=0 && current.FindWidget()!=_host)Abandon();
        if(!playable){Hide();return;}
        var state=SpeakerIndicators.Snapshot();
        if(!state.Local && state.Players.Length==0){Hide();return;}
        if(now<_nextRetry)return;
        try
        {
            _ue??=new();var u=_ue;
            EnsureOverlay(u);
            Show(u,_overlay,true,ref _overlayVisibility);Show(u,_local,state.Local,ref _localVisibility);
            // Default every icon to hidden, then commit visibility once after this frame's
            // checks. Any failed check or exception still hides it, without hide/show churn.
            foreach(var row in Icons.Values)row.Desired=false;
            try
            {
                // The local corner indicator does not need player enumeration, projection or traces.
                if(state.Players.Length==0)return;
                if(!ReferenceEquals(_speakingSnapshot,state.Players))
                {
                    Speaking.Clear();foreach(string id in state.Players)Speaking.Add(id);
                    _speakingSnapshot=state.Players;
                }
                RenderPlayers(u);
            }
            finally
            {
                foreach(var row in Icons.Values)Show(u,row.Widget,row.Desired,ref row.Visibility);
            }
        }
        catch(Exception ex)
        {
            Hide();_nextRetry=now+5;
            Console.WriteLine($"[OblivionUI] Speaker indicators hidden; visibility check unavailable: {ex.Message}");
        }
    }

    private static void RenderPlayers(UnrealBookBridge u)
    {
        var me=SDK.Sync.LocalPlayer;
        if(me is not {IsValid:true} listener)return;
        nint localPawn=Pawn(listener);
        if(!Valid(u,localPawn)||!Valid(u,_controller))return;
        var camera=u.Object(_controller,"PlayerCameraManager");
        if(!Valid(u,camera))return;
        var start=u.Vector(u.Call(camera,"GetCameraLocation"),"GetCameraLocation");
        var size=u.Vector(u.Call(_layout,"GetViewportSize",("WorldContextObject",_host)),"GetViewportSize");
        float scale=u.ResultFloat(u.Call(_layout,"GetViewportScale",("WorldContextObject",_host)),"GetViewportScale");
        if(!float.IsFinite(scale)||scale<=0)return;
        _headName??=u.ResultBytes(u.Call(u.DefaultObject("/Script/Engine.Default__KismetStringLibrary"),"Conv_StringToName",("InString","head")),"Conv_StringToName");
        Observed.Clear();int visibleCount=0;
        foreach(var player in SDK.Sync.AllPlayers)
        {
            if(!player.IsValid)continue;
            string id=player.PlayerId.ToString();
            // Animation/health are native SDK reads; don't request them for non-speakers.
            if(!Speaking.Contains(id)||player.PlayerId==listener.PlayerId)continue;
            if(!SpeakerVisibility.Eligible(true,false,player.Animation.IsSneaking,player.Hp>0,true))continue;
            Observed.Add(id);
            if(visibleCount>=32)continue;
            nint pawn=Pawn(player);
            if(!Valid(u,pawn))continue;
            var mesh=u.Object(pawn,"Mesh");
            if(!Valid(u,mesh))continue;
            var head=u.Vector(u.Call(mesh,"GetSocketLocation",("InSocketName",_headName)),"GetSocketLocation");
            if(!double.IsFinite(head.X)||!double.IsFinite(head.Y)||!double.IsFinite(head.Z))continue;
            var projected=u.Call(_layout,"ProjectWorldLocationToWidgetPosition",("PlayerController",_controller),
                ("WorldLocation",(head.X,head.Y,head.Z+32)),("bPlayerViewportRelative",false));
            var screen=u.Vector(projected,"ProjectWorldLocationToWidgetPosition","ScreenPosition");
            if(!SpeakerVisibility.OnScreen(u.ResultBool(projected,"ProjectWorldLocationToWidgetPosition"),screen.X,screen.Y,size.X/scale,size.Y/scale))continue;
            // Still trace every frame for each on-screen speaker. Never reuse visibility from
            // an earlier frame: walls, corners, and sneaking must hide the indicator immediately.
            var trace=u.Call(_system,"LineTraceSingle",("WorldContextObject",_host),
                ("Start",start),("End",head),("TraceChannel",(byte)0),("bTraceComplex",true),
                ("ActorsToIgnore",new nint[]{localPawn,pawn}),("DrawDebugType",(byte)0),("bIgnoreSelf",true));
            if(u.ResultBool(trace,"LineTraceSingle"))continue;
            visibleCount++;
            if(!Icons.TryGetValue(id,out var row))
            {
                var widget=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",_iconClass),("OwningPlayer",_controller));
                if(widget==0)continue;
                var slot=u.Pointer(_canvas,"AddChildToCanvas",("Content",widget));
                if(slot==0){u.Call(widget,"RemoveFromParent");continue;}
                row=new(){Widget=widget,Slot=slot};Icons.Add(id,row);
                u.Call(slot,"SetSize",("InSize",(36d,36d)));
            }
            double x=screen.X-18,y=screen.Y-36;
            if(row.X!=x||row.Y!=y){u.Call(row.Slot,"SetPosition",("InPosition",(x,y)));row.X=x;row.Y=y;}
            row.Desired=true;
        }
        Removed.Clear();
        foreach(var pair in Icons)if(!Observed.Contains(pair.Key))Removed.Add(pair.Key);
        foreach(string id in Removed){u.Call(Icons[id].Widget,"RemoveFromParent");Icons.Remove(id);}
        // Occluded/off-screen speakers keep their hidden widget while they are speaking.
        // Walking around a corner no longer destroys and recreates the same icon repeatedly.
    }

    private static void EnsureOverlay(UnrealBookBridge u)
    {
        if(_overlay!=0)return;
        _host=u.FindWidget();
        if(_host==0){SDK.GameMessage.ShowGameMessage("","","");_host=u.FindWidget();}
        if(_host==0)throw new InvalidOperationException("Waiting for game HUD.");
        _system=u.DefaultObject("/Script/Engine.Default__KismetSystemLibrary");
        _layout=u.DefaultObject("/Script/UMG.Default__WidgetLayoutLibrary");
        _library=u.Library();_controller=u.Pointer(_host,"GetOwningPlayer");
        _iconClass=u.Object(_host,"SpeakerIconClass");
        var cls=u.Object(_host,"VoiceOverlayClass");
        if(cls==0||_iconClass==0||_controller==0)throw new InvalidOperationException("Voice UI assets or controller are unavailable.");
        _overlay=u.Pointer(_library,"Create",("WorldContextObject",_host),("WidgetType",cls),("OwningPlayer",_controller));
        if(_overlay==0)throw new InvalidOperationException("Voice overlay creation failed.");
        _canvas=u.Object(_overlay,"VoiceCanvas");_local=u.Object(_overlay,"LocalSpeaker");
        Show(u,_overlay,false,ref _overlayVisibility);
        u.Call(_overlay,"AddToViewport",("ZOrder",40));
        Console.WriteLine("[OblivionUI] Speaker overlay ready; camera occlusion and sneak filtering enabled.");
    }
    private static PropertyInfo? _entityProperty,_pawnProperty;
    private static nint Pawn(ReadyMainCharacter player)
    {
        // Resolve lazily inside Frame's failure boundary: a changed SDK must not break other UI.
        _entityProperty??=typeof(ReadyMainCharacter).GetProperty("Entity",BindingFlags.Instance|BindingFlags.NonPublic)
            ?? throw new NotSupportedException("SDK player entity contract is unavailable.");
        _pawnProperty??=_entityProperty.PropertyType.GetProperty("Pawn",BindingFlags.Instance|BindingFlags.Public)
            ?? throw new NotSupportedException("SDK pawn mapping contract is unavailable.");
        return (nint)(_pawnProperty.GetValue(_entityProperty.GetValue(player))??nint.Zero);
    }
    private static void Abandon()
    {
        Icons.Clear();Speaking.Clear();Observed.Clear();Removed.Clear();_speakingSnapshot=null;_headName=null;
        _overlay=_host=_controller=_canvas=_local=_iconClass=_system=_layout=_library=0;
        _overlayVisibility=_localVisibility=byte.MaxValue;_nextRetry=0;
    }
    private static bool Valid(UnrealBookBridge u,nint obj)=>obj!=0&&u.ResultBool(u.Call(_system,"IsValid",("Object",obj)),"IsValid");
    private static void Show(UnrealBookBridge u,nint obj,bool show,ref byte previous)
    {
        byte value=show?(byte)3:(byte)1;
        if(obj==0||previous==value)return;
        u.Call(obj,"SetVisibility",("InVisibility",value));previous=value;
    }
    private static void Hide()
    {
        if(_ue is {} u&&_overlay!=0)try{if(u.FindWidget()==_host)Show(u,_overlay,false,ref _overlayVisibility);else Abandon();}catch{}
    }
    internal static void Reset()
    {
        Hide();
        if(_ue is {} u&&_overlay!=0)try{if(u.FindWidget()==_host)u.Call(_overlay,"RemoveFromParent");}catch{}
        Abandon();SpeakerIndicators.Clear();
    }
}
