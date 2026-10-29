using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;

namespace ZenithPrototypeCheck;

// 仅供独立检查：读取 FNA 的 D3D11 设备并用时间戳查询测量显卡命令。
// 不修改图形状态，也不对借用的设备或上下文释放引用。
internal sealed class LiquidGlassGpuTimer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct QueryDescription { public int Query;public uint MiscFlags; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateQuery(IntPtr self,ref QueryDescription desc,out IntPtr query);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void QueryMarker(IntPtr self,IntPtr query);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetQueryData(IntPtr self,IntPtr query,IntPtr data,uint size,uint flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint Release(IntPtr self);
    [DllImport("FNA3D",CallingConvention=CallingConvention.Cdecl)]
    private static extern void FNA3D_GetSysRendererEXT(IntPtr device,IntPtr info);

    private readonly IntPtr context;
    private readonly QueryMarker begin,end;
    private readonly GetQueryData getData;
    private IntPtr disjoint,start,finish;
    private bool disposed;
    private static T Method<T>(IntPtr instance,int index) where T:Delegate
    {
        IntPtr table=Marshal.ReadIntPtr(instance);
        return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(table,index*IntPtr.Size));
    }
    internal LiquidGlassGpuTimer(GraphicsDevice graphics)
    {
        // 扩展结构版本 0；联合区域预留 64 字节，避免按具体后端缩短结构。
        IntPtr info=Marshal.AllocHGlobal(72);
        try
        {
            for(int i=0;i<72;i++)Marshal.WriteByte(info,i,0);
            var field=typeof(GraphicsDevice).GetField("GLDevice",BindingFlags.Instance|BindingFlags.NonPublic)
                ??throw new InvalidOperationException("无法读取 FNA 设备句柄");
            var fna=(IntPtr)(field.GetValue(graphics)??IntPtr.Zero);
            if(fna==IntPtr.Zero)throw new InvalidOperationException("FNA 设备句柄为空");
            FNA3D_GetSysRendererEXT(fna,info);
            if(Marshal.ReadInt32(info,4)!=2)throw new NotSupportedException("计时仅支持 D3D11 后端");
            IntPtr device=Marshal.ReadIntPtr(info,8);context=Marshal.ReadIntPtr(info,16);
            if(device==IntPtr.Zero||context==IntPtr.Zero)throw new InvalidOperationException("D3D11 设备或上下文为空");
            var create=Method<CreateQuery>(device,24);
            begin=Method<QueryMarker>(context,27);end=Method<QueryMarker>(context,28);
            getData=Method<GetQueryData>(context,29);
            try
            {
                var desc=new QueryDescription{Query=3};
                Marshal.ThrowExceptionForHR(create(device,ref desc,out disjoint));
                desc.Query=2;
                Marshal.ThrowExceptionForHR(create(device,ref desc,out start));
                Marshal.ThrowExceptionForHR(create(device,ref desc,out finish));
                if(disjoint==IntPtr.Zero||start==IntPtr.Zero||finish==IntPtr.Zero)
                    throw new InvalidOperationException("D3D11 计时查询创建失败");
            }
            catch{Dispose();throw;}
        }
        finally{Marshal.FreeHGlobal(info);}
    }
    internal double MeasureMilliseconds(Action draw,int iterations)
    {
        if(disposed)throw new ObjectDisposedException(nameof(LiquidGlassGpuTimer));
        if(iterations<=0)throw new ArgumentOutOfRangeException(nameof(iterations));
        begin(context,disjoint);end(context,start);
        try{for(int i=0;i<iterations;i++)draw();}
        finally{end(context,finish);end(context,disjoint);}
        IntPtr data=Marshal.AllocHGlobal(16);
        try
        {
            Read(disjoint,data,16);
            ulong frequency=unchecked((ulong)Marshal.ReadInt64(data));
            bool invalid=Marshal.ReadInt32(data,8)!=0;
            if(invalid||frequency==0)throw new InvalidOperationException("GPU 时间戳频率失效");
            Read(start,data,8);ulong first=unchecked((ulong)Marshal.ReadInt64(data));
            Read(finish,data,8);ulong last=unchecked((ulong)Marshal.ReadInt64(data));
            if(last<first)throw new InvalidOperationException("GPU 时间戳顺序异常");
            return (last-first)*1000.0/frequency/iterations;
        }
        finally{Marshal.FreeHGlobal(data);}
    }
    private void Read(IntPtr query,IntPtr data,uint size)
    {
        var watch=Stopwatch.StartNew();
        while(true)
        {
            int result=getData(context,query,data,size,0);
            if(result==0)return;
            if(result<0)Marshal.ThrowExceptionForHR(result);
            if(result!=1)throw new InvalidOperationException($"GPU 查询返回未知状态 {result}");
            if(watch.Elapsed.TotalSeconds>5)throw new TimeoutException("GPU 时间戳查询超过五秒");
            Thread.Sleep(1);
        }
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        foreach(var query in new[]{finish,start,disjoint})
            if(query!=IntPtr.Zero)Method<Release>(query,2)(query);
        finish=start=disjoint=IntPtr.Zero;
    }
}
