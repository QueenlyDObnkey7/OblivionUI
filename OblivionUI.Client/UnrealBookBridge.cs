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
    private readonly Ref _offset, _elementSize, _paramSize;
    private readonly Event _event;
    private readonly ReadBool _readBool;
    private readonly WriteBool _writeBool;
    private sealed record Layout(int Size,Dictionary<string,(int Offset,int Size)> Fields);
    private readonly Dictionary<string,Layout> _layouts=new(StringComparer.Ordinal);
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
        foreach(var name in new[]{"SetTitle","SetSubtitle","SetPageLabel","SetLabel","SetDescription","SetButtonLabel","SetInput"}) _layouts.Add(name,new(16,new(){{"Text",(0,16)}}));
    }
    public nint FindWidget()=>_find("WBP_GameMessage_C");
    public nint Library()=>_object(0,0,"/Script/UMG.Default__WidgetBlueprintLibrary",false);
    private (nint Property,nint Address) Field(nint obj,string name,int size=0)
    {
        if(obj==0) throw new InvalidOperationException("Missing Unreal object.");
        var p=_property(obj,name); if(p==0) throw new MissingMemberException(name);
        int offset=Marshal.ReadInt32(_offset(p)); int actual=Marshal.ReadInt32(_elementSize(p));
        if(offset<0 || offset>1_048_576 || (size!=0 && actual!=size)) throw new NotSupportedException($"Property ABI mismatch: {name}");
        return (p,obj+offset);
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
    public byte[] Call(nint obj,string name,params (string Name,object Value)[] values)
    {
        if(obj==0) throw new InvalidOperationException($"Missing target for {name}");
        var fn=_function(obj,name); if(fn==0) throw new MissingMethodException(name);
        var layout=_layouts[name];
        if((ushort)Marshal.ReadInt16(_paramSize(fn))!=layout.Size) throw new NotSupportedException($"UE function ABI mismatch: {name}");
        var data=new byte[layout.Size]; var strings=new List<nint>(); var buffer=Marshal.AllocHGlobal(Math.Max(1,layout.Size));
        try {
            foreach(var (key,value) in values) {
                var field=layout.Fields[key]; byte[] bytes=value switch {
                    nint p=>BitConverter.GetBytes(p.ToInt64()), int i=>BitConverter.GetBytes(i), float f=>BitConverter.GetBytes(f),
                    bool b=>[b ? (byte)1 : (byte)0], byte b=>[b],
                    ValueTuple<double,double> v=>[..BitConverter.GetBytes(v.Item1),..BitConverter.GetBytes(v.Item2)],
                    string s=>StringBytes(s), _=>throw new ArgumentException("Unsupported Unreal argument")
                };
                if(bytes.Length!=field.Size) throw new NotSupportedException($"Argument ABI mismatch: {name}.{key}");
                bytes.CopyTo(data,field.Offset);
            }
            Marshal.Copy(data,0,buffer,data.Length); _event(obj,fn,buffer); Marshal.Copy(buffer,data,0,data.Length); return data;
        } finally { Marshal.FreeHGlobal(buffer); foreach(var p in strings) Marshal.FreeHGlobal(p); }
        byte[] StringBytes(string s) { var p=Marshal.StringToHGlobalUni(s); strings.Add(p); return [..BitConverter.GetBytes(p.ToInt64()),..BitConverter.GetBytes(s.Length+1),..BitConverter.GetBytes(s.Length+1)]; }
    }
    public nint Pointer(nint obj,string name,params (string Name,object Value)[] args) { var b=Call(obj,name,args); return (nint)BitConverter.ToInt64(b,_layouts[name].Fields["ReturnValue"].Offset); }
    public int Integer(nint obj,string name) { var b=Call(obj,name); return BitConverter.ToInt32(b,_layouts[name].Fields["ReturnValue"].Offset); }
    public float Number(nint obj,string name) { var b=Call(obj,name); return BitConverter.ToSingle(b,_layouts[name].Fields["ReturnValue"].Offset); }
    public bool Boolean(nint obj,string name) { var b=Call(obj,name); return b[_layouts[name].Fields["ReturnValue"].Offset]!=0; }
    public void SetText(nint obj,string fn,string text)=>Call(obj,fn,("Text",text));
    public void Visible(nint obj,bool visible)=>Call(obj,"SetVisibility",("InVisibility",visible ? (byte)0 : (byte)1));
}
