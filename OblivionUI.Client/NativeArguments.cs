using System.Runtime.InteropServices;

namespace OblivionUI.Client;

internal static class NativeArguments
{
    // Write primitive/struct parameters directly into their reflected field. Keep exact
    // size validation: silently truncating a parameter can corrupt Unreal's stack.
    internal static bool TryWrite(Span<byte> target,object value)
    {
        switch(value){
            case nint p: Write(target,p);break;
            case int i: Write(target,i);break;
            case float f: Write(target,f);break;
            case bool b: Write(target,b?(byte)1:(byte)0);break;
            case byte b: Write(target,b);break;
            case ValueTuple<double,double> v: Size(target,16);MemoryMarshal.Write(target,in v.Item1);MemoryMarshal.Write(target[8..],in v.Item2);break;
            case ValueTuple<double,double,double> v: Size(target,24);MemoryMarshal.Write(target,in v.Item1);MemoryMarshal.Write(target[8..],in v.Item2);MemoryMarshal.Write(target[16..],in v.Item3);break;
            case ValueTuple<float,float,float,float> v: Size(target,16);MemoryMarshal.Write(target,in v.Item1);MemoryMarshal.Write(target[4..],in v.Item2);MemoryMarshal.Write(target[8..],in v.Item3);MemoryMarshal.Write(target[12..],in v.Item4);break;
            case byte[] raw: Size(target,raw.Length);raw.CopyTo(target);break;
            default:return false;
        }
        return true;
    }
    private static void Size(Span<byte> target,int expected){if(target.Length!=expected)throw new NotSupportedException("Unreal argument ABI size mismatch.");}
    private static void Write<T>(Span<byte> target,T value) where T:unmanaged {Size(target,System.Runtime.CompilerServices.Unsafe.SizeOf<T>());MemoryMarshal.Write(target,in value);}
}
