using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace ZenithReborn;

// 动作曲线与实际弹幕共用，预览不能另写一套更夸张的运动。
public static class MartialMotion
{
    public const int PopFrames=7,SpinFrames=13,AimFrames=8;
    public const int ThunderImpact=10,ChargeFrames=60,FlingSpinFrames=27;
    public const int SlashRelease=6;
    public static float Ease(float t)=>1-MathF.Pow(1-Math.Clamp(t,0,1),3);
    public static float ThrustSpeed(int age)=>MathHelper.Lerp(8,46,Ease(age/6f));
    public static float SlashTurn(int age)
    {
        // 先压住刀势，随后十帧切过主体角度，末段只作小幅收势。
        float t=Math.Clamp((age-2)/10f,0,1);
        float cut=t*t*(3-2*t);
        return MathHelper.Lerp(-1.55f,1.45f,cut);
    }
    public static (Vector2 Center,float Rotation,float Scale) Formation(int age,int order,int count,Vector2 player,int facing,Vector2 target)
    {
        int duration=MartialTuning.OrbitFrames+order*2;
        float pop=Ease(age/(float)PopFrames);
        float spread=(order-(count-1)*.5f)*25;
        var center=player+new Vector2(-facing*(18+80*pop),-26+spread*pop);
        float aim=(target-center).SafeNormalize(Vector2.UnitX*facing).ToRotation();
        float settle=Math.Clamp((age-(duration-AimFrames))/(float)AimFrames,0,1);
        float spin=Math.Clamp((age-PopFrames)/(float)(duration-AimFrames-PopFrames),0,1);
        // 三圈高速自旋结束后，最后八帧单独定向，不是一边乱转一边发射。
        float rotation=aim+MathHelper.PiOver4+MathHelper.TwoPi*3*Ease(spin)*(1-settle);
        if(age>=duration-AimFrames)rotation=aim+MathHelper.PiOver4+(1-Ease(settle))*.4f;
        float scale=age<PopFrames?MathHelper.Lerp(.25f,1.5f,pop):MathHelper.Lerp(1.5f,1.2f,Math.Clamp((age-PopFrames)/10f,0,1));
        return(center,rotation,scale);
    }
}

// 只在真正突进的十二帧隐藏人物绘制层；其他阶段和取消后立刻恢复。
public class MartialDashPlayer : ModPlayer
{
    // Main.ActiveProjectiles 是自定义迭代器，不支持 LINQ 的 Any，改手动遍历。
    public bool IsLightDash=>CheckLightDash();
    private bool CheckLightDash()
    {
        foreach(var q in Main.ActiveProjectiles)
            if(q.owner==Player.whoAmI &&
               q.ModProjectile is MartialGiant g && g.Tier==3 && g.Phase==2 &&
               Player.HeldItem.ModItem is SoulItem s && s.SoulId==q.GetGlobalProjectile<SoulSource>().SoulId)
                return true;
        return false;
    }
    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        if(IsLightDash)foreach(var layer in PlayerDrawLayerLoader.Layers)layer.Hide();
    }
}
