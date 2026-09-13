using System.Runtime.InteropServices;

namespace OblivionUI.Client;

// UE4SS exposes these C++ wrappers with the Windows x64 ABI. Property offsets come from
// reflection; function argument layouts are exported by the project's UE 5.3.2 commandlet.
internal sealed class UnrealBookBridge
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet=CharSet.Unicode)] private delegate nint Find(string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet=CharSet.Unicode)] private delegate nint Named(nint self,string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Ref(nint self);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Event(nint self,nint fn,nint args);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet=CharSet.Unicode)] private delegate nint FindObject(nint cls,nint outer,string name,[MarshalAs(UnmanagedType.I1)] bool exact);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] private delegate bool ReadBool(nint prop,nint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void WriteBool(nint prop,nint value,[MarshalAs(UnmanagedType.I1)] bool enabled);
    [DllImport("kernel32",CharSet=CharSet.Unicode)] private static extern nint GetModuleHandle(string name);
    private readonly Find _find;
    private readonly FindObject _object;
    private readonly Named _property, _function;
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint NameIndex(nint name);
    private readonly Ref _offset, _elementSize, _paramSize, _children, _next, _name, _class;
    private readonly NameIndex _index;
    private readonly HashSet<string> _resolved=new(StringComparer.Ordinal);
    private readonly Event _event;
    private readonly ReadBool _readBool;
    private readonly WriteBool _writeBool;
    private sealed record Layout(int Size,Dictionary<string,(int Offset,int Size)> Fields);
    private readonly Dictionary<string,Layout> _layouts=new(StringComparer.Ordinal);
    private readonly Dictionary<(nint Class,string Name),nint> _functions=new();
    private readonly Dictionary<(nint Class,string Name),(nint Property,int Offset,int Size)> _fields=new();
    private readonly Dictionary<string,nint> _defaults=new(StringComparer.Ordinal);
    private nint _lastHost;
    private static bool _frameScoped;
    private static nint _frameHost;
    // UI systems share one game-thread update. Never retain a scanned widget between updates.
    internal static void BeginFrame(){_frameScoped=true;_frameHost=0;}
    internal static void EndFrame(){_frameScoped=false;_frameHost=0;}
    public UnrealBookBridge()
    {
        var module=GetModuleHandle("UE4SS.dll"); if(module==0) throw new NotSupportedException("UE4SS is not loaded.");
        T Bind<T>(string name) where T:Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(module,name));
        _find=Bind<Find>("?FindFirstOf@UObjectGlobals@Unreal@RC@@YAPEAVUObject@23@PEB_W@Z");
        _object=Bind<FindObject>("?StaticFindObject_InternalSlow@UObjectGlobals@Unreal@RC@@YAPEAVUObject@23@PEAVUClass@23@PEAV423@PEB_W_N@Z");
        _property=Bind<Named>("?GetPropertyByNameInChain@UObject@Unreal@RC@@QEAAPEAVFProperty@23@PEB_W@Z");
        _function=Bind<Named>("?GetFunctionByNameInChain@UObject@Unreal@RC@@QEAAPEAVUFunction@23@PEB_W@Z");
        _offset=Bind<Ref>("?GetOffset_Internal@FProperty@Unreal@RC@@QEAAAEAHXZ");
        _elementSize=Bind<Ref>("?GetElementSize@FProperty@Unreal@RC@@QEAAAEAHXZ");
        _paramSize=Bind<Ref>("?GetParmsSize@UFunction@Unreal@RC@@QEAAAEAGXZ");
        _children=Bind<Ref>("?GetChildProperties@UStruct@Unreal@RC@@QEAAAEAPEAVFField@23@XZ");
        _next=Bind<Ref>("?GetNext@FField@Unreal@RC@@AEAAAEAPEAV123@XZ");
        _name=Bind<Ref>("?GetNamePrivate@FField@Unreal@RC@@AEAAAEAVFName@23@XZ");
        _class=Bind<Ref>("?GetClassPrivate@UObjectBase@Unreal@RC@@QEAAAEAPEAVUClass@23@XZ");
        _index=Bind<NameIndex>("?GetComparisonIndex@FName@Unreal@RC@@QEBAIXZ");
        _event=Bind<Event>("?ProcessEvent@UObject@Unreal@RC@@QEAAXPEAVUFunction@23@PEAX@Z");
        _readBool=Bind<ReadBool>("?GetPropertyValue@FBoolProperty@Unreal@RC@@QEAA_NPEBX@Z");
        _writeBool=Bind<WriteBool>("?SetPropertyValue@FBoolProperty@Unreal@RC@@QEAAXPEAX_N@Z");
        using var stream=typeof(UnrealBookBridge).Assembly.GetManifestResourceStream("OblivionUI.Client.BookNative.tsv")!;
        using var reader=new StreamReader(stream);
        while(reader.ReadLine() is {} line) {
            var parts=line.Split('|'); var fields=new Dictionary<string,(int,int)>();
            foreach(var part in parts.Skip(2)) { var v=part.Split(':'); fields.Add(v[0],(int.Parse(v[1]),int.Parse(v[2]))); }
            _layouts.Add(parts[0],new(int.Parse(parts[1]),fields));
        }
        foreach(var name in new[]{"SetTitle","SetSubtitle","SetPageLabel","SetLabel","SetDescription","SetButtonLabel","SetInput","SetSliderMinimum","SetSliderMiddle","SetSliderMaximum","SetSliderCurrent","SetCloseLabel","SetArtwork","SetPaperArtwork","SetPageArtwork","SetTabLeatherArtwork","SetTabLeftBlendArtwork","SetTabRightBlendArtwork","SetTabActivePaper","SetTabInactivePaper","SetTabBasePaper"}) _layouts.Add(name,new(16,new(){{"Text",(0,16)}}));
        foreach(var name in new[]{"SetLabelSize","SetDescriptionSize","SetButtonSize","SetTitleSize","SetSubtitleSize"})_layouts.Add(name,new(4,new(){{"Size",(0,4)}}));
        foreach(var name in new[]{"SetLabelInk","SetDescriptionInk","SetButtonInk","SetTitleInk","SetSubtitleInk"})_layouts.Add(name,new(16,new(){{"Color",(0,16)}}));
    }
    public void Font(nint widget,string fn,int size)=>Call(widget,fn,("Size",size));
    public void Ink(nint widget,string fn,UiColor color)=>Call(widget,fn,("Color",(color.R,color.G,color.B,color.A)));
    public byte[] ResultBytes(byte[] data,string fn,string field="ReturnValue") {var f=_layouts[fn].Fields[field];return data.AsSpan(f.Offset,f.Size).ToArray();}
    private Layout Resolve(string name,nint fn,Layout expected) {
        if(_resolved.Contains(name))return expected;
        var props=new List<(uint Name,int Offset,int Size)>();
        for(nint p=Marshal.ReadIntPtr(_children(fn));p!=0;p=Marshal.ReadIntPtr(_next(p))) {
            if(props.Count>=64)throw new NotSupportedException("Unexpected parameter chain.");
            props.Add((_index(_name(p)),Marshal.ReadInt32(_offset(p)),Marshal.ReadInt32(_elementSize(p))));
        }
        int size=(ushort)Marshal.ReadInt16(_paramSize(fn));
        var fields=new Dictionary<string,(int Offset,int Size)>();
        if(name=="Conv_StringToName") {
            // Bootstrap by reflected parameter offset/type width; FName is 8 or 12 bytes.
            if(props.Count!=2 || !props.Any(p=>p.Offset==0&&p.Size==16))throw new NotSupportedException("String conversion ABI changed.");
            var ret=props.Single(p=>p.Offset==16&&(p.Size==8||p.Size==12));
            fields.Add("InString",(0,16));fields.Add("ReturnValue",(ret.Offset,ret.Size));
        } else foreach(var key in expected.Fields.Keys) {
            var raw=Call(DefaultObject("/Script/Engine.Default__KismetStringLibrary"),"Conv_StringToName",("InString",key));
            var bytes=ResultBytes(raw,"Conv_StringToName");var mem=Marshal.AllocHGlobal(bytes.Length);
            try {Marshal.Copy(bytes,0,mem,bytes.Length);uint id=_index(mem);var p=props.Single(p=>p.Name==id);fields.Add(key,(p.Offset,p.Size));}finally{Marshal.FreeHGlobal(mem);}
        }
        foreach(var f in fields.Values)if(f.Offset<0||f.Size<0||f.Offset+f.Size>size)throw new NotSupportedException("Parameter bounds invalid.");
        var layout=new Layout(size,fields);_layouts[name]=layout;_resolved.Add(name);return layout;
    }
    public nint FindWidget()
    {
        nint host=_frameScoped&&_frameHost!=0?_frameHost:_find("WBP_GameMessage_C");
        if(_frameScoped)_frameHost=host;
        if(host!=_lastHost){_functions.Clear();_fields.Clear();_defaults.Clear();_resolved.Clear();_lastHost=host;}
        return host;
    }
    public nint DefaultObject(string path)
    {
        // Only native script CDOs have a process lifetime; asset objects remain uncached.
        if(!path.StartsWith("/Script/",StringComparison.Ordinal))return _object(0,0,path,false);
        if(_defaults.TryGetValue(path,out var found))return found;
        var obj=_object(0,0,path,false);if(obj!=0)_defaults.Add(path,obj);return obj;
    }
    public nint Library()=>DefaultObject("/Script/UMG.Default__WidgetBlueprintLibrary");
    private (nint Property,nint Address) Field(nint obj,string name,int size=0)
    {
        if(obj==0) throw new InvalidOperationException("Missing Unreal object.");
        var key=(Marshal.ReadIntPtr(_class(obj)),name);
        if(!_fields.TryGetValue(key,out var field)){
            var p=_property(obj,name); if(p==0) throw new MissingMemberException(name);
            field=(p,Marshal.ReadInt32(_offset(p)),Marshal.ReadInt32(_elementSize(p)));
            if(field.Offset<0 || field.Offset>1_048_576)throw new NotSupportedException($"Property ABI mismatch: {name}");
            _fields.Add(key,field);
        }
        if(size!=0 && field.Size!=size)throw new NotSupportedException($"Property ABI mismatch: {name}");
        return (field.Property,obj+field.Offset);
    }
    public nint Object(nint obj,string name)=>Marshal.ReadIntPtr(Field(obj,name,8).Address);
    public int Serial(nint obj,string name)=>Marshal.ReadInt32(Field(obj,name,4).Address);
    public string Text(nint obj,string name)
    {
        var p=Field(obj,name,16).Address; int length=Marshal.ReadInt32(p,8);
        if(length<0 || length>65536) throw new InvalidOperationException("Invalid Unreal string length.");
        return length<=1 ? "" : Marshal.PtrToStringUni(Marshal.ReadIntPtr(p),length-1) ?? "";
    }
    public bool Bool(nint obj,string name) { var f=Field(obj,name); return _readBool(f.Property,f.Address); }
    public void Bool(nint obj,string name,bool value) { var f=Field(obj,name); _writeBool(f.Property,f.Address,value); }
    public unsafe byte[] Call(nint obj,string name,params (string Name,object Value)[] values)
    {
        if(obj==0) throw new InvalidOperationException($"Missing target for {name}");
        var functionKey=(Marshal.ReadIntPtr(_class(obj)),name);
        if(!_functions.TryGetValue(functionKey,out var fn)){fn=_function(obj,name);if(fn==0)throw new MissingMethodException(name);_functions.Add(functionKey,fn);}
        var layout=Resolve(name,fn,_layouts[name]);
        var data=new byte[layout.Size]; List<nint>? strings=null;
        try {
            foreach(var (key,value) in values) {
                var field=layout.Fields[key];
                if(NativeArguments.TryWrite(data.AsSpan(field.Offset,field.Size),value))continue;
                byte[] bytes=value switch {
                    nint[] pointers=>PointerArray(pointers),
                    string s=>StringBytes(s), _=>throw new ArgumentException("Unsupported Unreal argument")
                };
                if(bytes.Length!=field.Size) throw new NotSupportedException($"Argument ABI mismatch: {name}.{key}");
                bytes.CopyTo(data,field.Offset);
            }
            // ProcessEvent consumes parameters synchronously. Pinning avoids a native allocation
            // and two complete copies for every hover test, property setter and projection.
            byte empty=0;
            fixed(byte* buffer=data)_event(obj,fn,data.Length==0?(nint)(&empty):(nint)buffer);
            return data;
        } finally { if(strings is not null)foreach(var p in strings) Marshal.FreeHGlobal(p); }
        byte[] PointerArray(nint[] values) {
            var p=Marshal.AllocHGlobal(values.Length*8); (strings??=new()).Add(p);
            for(int i=0;i<values.Length;i++) Marshal.WriteIntPtr(p,i*8,values[i]);
            return [..BitConverter.GetBytes(p.ToInt64()),..BitConverter.GetBytes(values.Length),..BitConverter.GetBytes(values.Length)];
        }
        byte[] StringBytes(string s) { var p=Marshal.StringToHGlobalUni(s); (strings??=new()).Add(p); return [..BitConverter.GetBytes(p.ToInt64()),..BitConverter.GetBytes(s.Length+1),..BitConverter.GetBytes(s.Length+1)]; }
    }
    public nint Pointer(nint obj,string name,params (string Name,object Value)[] args) { var b=Call(obj,name,args); return (nint)BitConverter.ToInt64(b,_layouts[name].Fields["ReturnValue"].Offset); }
    public int Integer(nint obj,string name) { var b=Call(obj,name); return BitConverter.ToInt32(b,_layouts[name].Fields["ReturnValue"].Offset); }
    public float Number(nint obj,string name) { var b=Call(obj,name); return BitConverter.ToSingle(b,_layouts[name].Fields["ReturnValue"].Offset); }
    public bool Boolean(nint obj,string name) { var b=Call(obj,name); return b[_layouts[name].Fields["ReturnValue"].Offset]!=0; }
    public bool ResultBool(byte[] data,string fn,string field="ReturnValue")=>data[_layouts[fn].Fields[field].Offset]!=0;
    public (double X,double Y,double Z) Vector(byte[] data,string fn,string field="ReturnValue") {
        int offset=_layouts[fn].Fields[field].Offset;
        return (BitConverter.ToDouble(data,offset),BitConverter.ToDouble(data,offset+8),_layouts[fn].Fields[field].Size>=24 ? BitConverter.ToDouble(data,offset+16) : 0);
    }
    public float ResultFloat(byte[] data,string fn,string field="ReturnValue")=>BitConverter.ToSingle(data,_layouts[fn].Fields[field].Offset);
    public void SetText(nint obj,string fn,string text)=>Call(obj,fn,("Text",text));
    public void Visible(nint obj,bool visible)=>Call(obj,"SetVisibility",("InVisibility",visible ? (byte)0 : (byte)1));
}
