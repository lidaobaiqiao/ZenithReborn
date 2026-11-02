using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// 粘性空腔挥动特效（残影扫掠版）。
// 物理模型：武器在粘性介质里挥动，划过的地方被排开形成空腔，
// 空腔不会瞬间塌陷，而是靠粘性缓慢回填（低雷诺数 Stokes 流，无涡、无湍流）。
//
// 实现：不碰 RenderTarget，只记录"每帧的武器轮廓"（残影），
// 按寿命淡出——这些残影的并集就是扫掠体积。
// 每个残影的浓度本身就代表该位置的排开量，越靠近剑尖越明显。
[Autoload(Side=ModSide.Client)]
public class ViscousSlash : ModSystem
{
    private const int Lifetime=26;          // 残影寿命（帧）：越小回填越快
    private const int MaxGhosts=90;         // 上限，防止长时间挥动堆积
    private const float BaseAlpha=.55f;     // 单个残影强度（加法混合，不再是透明度）
    private const float GlowScale=1.18f;   // 外扩光晕的倍数
    private const float GlowAlpha=.30f;    // 外扩光晕强度
    private const float HiltPivotX=.22f,HiltPivotY=.78f;  // 剑柄在贴图里的相对位置

    private readonly record struct Ghost(int ItemType,Vector2 Center,float Rotation,float Scale,float Age,Color Tint);
    private static readonly List<Ghost> ghosts=new();

    public override void Load(){if(!Main.dedServ)On_Main.DrawDust+=DrawHook;}
    public override void Unload(){On_Main.DrawDust-=DrawHook;ghosts.Clear();}
    // 残影存在静态列表里，切世界/换存档必须清空，
    // 否则上一个世界的剑影会留在新世界里（表现为"残余剑影"）。
    public override void OnWorldUnload(){ghosts.Clear();}
    public override void OnWorldLoad(){ghosts.Clear();}

    // 每帧记录：谁在挥动，就把当前武器轮廓存一份残影
    public override void PostUpdateEverything()
    {
        if(!VfxIsolation.ViscousSlash||Main.dedServ)return;
        for(int i=0;i<ghosts.Count;i++)
        {
            var g=ghosts[i];int newAge=(int)g.Age+1;
            // 残影进入后半段时，沿剑身位置飘出小圆圈，像光被烧干一样蒸发；
            // 颜色取当前武器色并向暖白过渡。
            if(!Main.gameMenu && newAge>Lifetime*.5f && newAge<Lifetime && newAge%2==0)
            {
                var axis=(g.Rotation+MathHelper.PiOver4).ToRotationVector2();
                for(int k=0;k<2;k++)
                {
                    Vector2 at=g.Center+axis*Main.rand.NextFloat(40,260)*g.Scale;
                    EvapCircle.Spawn(new EvapCircleSpawn(at,
                        new Vector2(Main.rand.NextFloat(-.2f,.2f),Main.rand.NextFloat(-1.3f,-.6f)),
                        Color.Lerp(g.Tint,new Color(235,242,255),.5f),
                        Main.rand.NextFloat(.3f,.6f),Main.rand.Next(18,26)));
                }
            }
            ghosts[i]=g with {Age=newAge};
        }
        ghosts.RemoveAll(g=>g.Age>Lifetime);
        foreach(var p in Main.player)
        {
            if(!p.active||p.dead)continue;
            var item=p.HeldItem;
            if(item.IsAir||item.type<=ItemID.None)continue;
            if(p.itemAnimation<=0&&p.itemTime<=0)continue;
            // 只处理真正挥动的近战武器：枪械、法杖不排开胶体
            if(!item.CountsAsClass(DamageClass.Melee) || item.useStyle!=ItemUseStyleID.Swing)continue;
            // 剑魂已有真实飞剑带面，容器挥臂不再叠加一份虚构剑影。
            if(item.ModItem is SoulItem)continue;
            // 原版天顶剑是召唤飞剑型武器，没有实体挥砍，不产生空腔
            if(item.type==ItemID.Zenith)continue;
            if(!float.IsFinite(p.itemRotation)||!float.IsFinite(p.itemLocation.X))continue;
            // 天顶剑魂本身是召唤容器：残影画里面当前寄宿的那把剑，颜色也取它。
            int ghostType=item.type;Color tint=SoulVisuals.Tint(item.type);
            if(item.ModItem is SoulItem soul && soul.Attached)
            {
                var host=soul.Active.FirstOrDefault(i=>!i.IsAir);
                if(host!=null){ghostType=host.type;tint=SoulVisuals.Tint(host.type);}
            }
            ghosts.Add(new Ghost(ghostType,p.itemLocation,p.itemRotation,p.GetAdjustedItemScale(item),0f,tint));
        }
        if(ghosts.Count>MaxGhosts)ghosts.RemoveRange(0,ghosts.Count-MaxGhosts);
    }

    private static void DrawHook(On_Main.orig_DrawDust orig,Main self)
    {
        orig(self);
        if(!VfxIsolation.ViscousSlash||Main.dedServ||Main.gameMenu||ghosts.Count==0)return;
        try
        {
            // 复用混合、深度、光栅、纹理与采样状态的恢复包装。
            // 此处不切换渲染目标；该入口并不负责保存渲染目标内容。
            VfxRenderState.DrawWithRestoredState(()=>
            {
                // 加法混合：残影不是"变淡的武器"，而是"武器留下的光"。
                // 用 AlphaBlend + 低 alpha 会画成半透明黑，看起来暗沉沉；
                // 加法下暗部不贡献、亮部叠加成发光，才和特效的亮度协调。
                Main.spriteBatch.Begin(SpriteSortMode.Deferred,BlendState.Additive,Main.DefaultSamplerState,
                    DepthStencilState.None,Main.Rasterizer,null,Main.GameViewMatrix.TransformationMatrix);
                foreach(var ghost in ghosts)
                {
                    var texture=TextureAssets.Item[ghost.ItemType].Value;
                    if(texture is null||texture.Width<=0)continue;
                    float t=ghost.Age/Lifetime;
                    if(t>=1f)continue;
                    float fade=(1f-t)*(1f-t);          // 先慢后快的回填曲线
                    // 残影颜色跟随武器，末尾向暖白过渡，像光被烧干。
                    Color tint=Color.Lerp(ghost.Tint,new Color(235,242,255),t*t);
                    Vector2 at=ghost.Center-Main.screenPosition;
                    Vector2 origin=new(texture.Width*HiltPivotX,texture.Height*HiltPivotY);
                    float rotation=ghost.Rotation+MathHelper.PiOver4;
                    // 第一层：外扩光晕，让边缘有溢出
                    Main.spriteBatch.Draw(texture,at,null,tint*(GlowAlpha*fade),
                        rotation,origin,ghost.Scale*GlowScale,SpriteEffects.None,0);
                    // 第二层：本体，亮度更高、收得更紧
                    Main.spriteBatch.Draw(texture,at,null,tint*(BaseAlpha*fade),
                        rotation,origin,ghost.Scale,SpriteEffects.None,0);
                }
                Main.spriteBatch.End();
            });
        }
        catch(Exception)
        {
            ghosts.Clear();   // 出错就清空，不再尝试
        }
    }
}
