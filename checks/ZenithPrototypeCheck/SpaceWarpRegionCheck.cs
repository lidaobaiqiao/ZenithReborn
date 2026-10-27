using Microsoft.Xna.Framework;

namespace ZenithPrototypeCheck;

// 使用覆盖邻域的保守边界验收局部交互，不复刻着色器中的采样方向和压力公式。
internal static class SpaceWarpRegionCheck
{
    internal sealed record Result(int ChangedPixels,int OutsideBladeChangedPixels,int BeyondReachChangedPixels,long Difference);
    internal static Result Compare(Color[] off,Color[] on,Color[] coverage,int width,int height,float radius)
    {
        int stride=width+1;var prefix=new int[stride*(height+1)];
        for(int y=0;y<height;y++)
        {
            int row=0;
            for(int x=0;x<width;x++)
            {if(coverage[y*width+x].A>0)row++;prefix[(y+1)*stride+x+1]=prefix[y*stride+x+1]+row;}
        }
        int changed=0,outside=0,beyond=0,reach=(int)Math.Ceiling(radius)+2;long sum=0;
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int index=y*width+x;var a=off[index];var b=on[index];
            int delta=Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);sum+=delta;
            if(delta<=3)continue;changed++;if(coverage[index].A==0)outside++;
            int left=Math.Max(0,x-reach),right=Math.Min(width,x+reach+1),top=Math.Max(0,y-reach),bottom=Math.Min(height,y+reach+1);
            int count=prefix[bottom*stride+right]-prefix[top*stride+right]-prefix[bottom*stride+left]+prefix[top*stride+left];
            if(count==0)beyond++;
        }
        return new(changed,outside,beyond,sum);
    }
}
