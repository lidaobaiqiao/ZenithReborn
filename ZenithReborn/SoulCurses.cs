using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

public enum SoulCurse { Binding, Switching, Sheathing, Barrier, Solitude, Bluntness }
public enum CurseStage { Cursed, Weakened, Released, Blessed }

public static partial class SoulCurses
{
    public const int All=(1<<18)-1, AllReleased=63;
    public const int Eye=1,Brain=2,Eater=4,Skeleton=8,Cultist=16,Moon=32,Bee=64,Flower=128,Temple=256,
        Destroyer=512,Retinazer=1024,Spazmatism=2048,Prime=4096,Flesh=8192,Queen=16384,Empress=32768,Duke=65536,Deer=131072;
    public static readonly string[] Names=["缚身","切换","归鞘","阻隔","独行","断锋"];
    public static readonly string[] Blessings=["御剑","切换","轮转","穿界","独行","连斩"];
    public static readonly int[][] Groups=[[Eye,Moon],[Brain,Cultist],[Eater,Skeleton],
        [Bee,Flower,Temple],[Destroyer,Retinazer|Spazmatism,Prime],[Flesh,Queen,Empress]];
    public static readonly string[][] BossNames=[["克苏鲁之眼","月亮领主"],["克苏鲁之脑","拜月教邪教徒"],
        ["世界吞噬怪","骷髅王"],["蜂王","世纪之花","石巨人"],["毁灭者","双子魔眼（两只）","机械骷髅王"],
        ["血肉墙","史莱姆女皇","光之女皇"]];
    public static int BossSeal(int type)=>type switch{
        NPCID.EyeofCthulhu=>Eye,NPCID.BrainofCthulhu=>Brain,
        NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail=>Eater,
        NPCID.SkeletronHead=>Skeleton,NPCID.CultistBoss=>Cultist,NPCID.MoonLordCore=>Moon,NPCID.QueenBee=>Bee,
        NPCID.Plantera=>Flower,NPCID.Golem=>Temple,NPCID.TheDestroyer or NPCID.TheDestroyerBody or NPCID.TheDestroyerTail=>Destroyer,
        NPCID.Retinazer=>Retinazer,NPCID.Spazmatism=>Spazmatism,NPCID.SkeletronPrime=>Prime,NPCID.WallofFlesh=>Flesh,
        NPCID.QueenSlimeBoss=>Queen,NPCID.HallowBoss=>Empress,NPCID.DukeFishron=>Duke,NPCID.Deerclops=>Deer,_=>0};
    public static int Count(SoulItem soul,SoulCurse curse)=>Groups[(int)curse].Count(bit=>(soul.Seals&bit)==bit);
    public static CurseStage RecordedStage(SoulItem soul,SoulCurse curse)
    {
        int count=Count(soul,curse),total=Groups[(int)curse].Length;
        return StageForCount(count,total);
    }
    public static CurseStage StageForCount(int count,int total)=>count>=total?CurseStage.Blessed:count>=total-1?CurseStage.Released:count==1?CurseStage.Weakened:CurseStage.Cursed;
    public static CurseStage Stage(SoulItem soul,SoulCurse curse)
    {
        var actual=RecordedStage(soul,curse);
        return (soul.LegacyReleased&(1<<(int)curse))!=0 && actual<CurseStage.Released?CurseStage.Released:actual;
    }
    public static string StageName(CurseStage stage)=>stage switch{CurseStage.Cursed=>"诅咒中",CurseStage.Weakened=>"已削弱",CurseStage.Released=>"已解除",_=>"已祝福"};
    // 屏幕尺度（世界单位）：人物到左右边界的距离。射程按它分档，而不是按进度累加。
    // factor 用于祝福等更远的参考距离；无图形环境退回960像素，避免在专用服务器上归零。
    public static float ScreenEdge(Player player,float factor=1)
    {
        // 无头检查或菜单早期可能还没有视图矩阵，缺失时按1倍视野处理。
        float zoom=Main.GameViewMatrix?.Zoom.X??1;
        if(zoom<=0||!float.IsFinite(zoom))zoom=1;
        float edge=Main.screenWidth>0?Main.screenWidth*.5f/zoom:960f;
        return edge*factor;
    }
    // 独行：初始2把，削弱3把（走原生发射点，与原版节奏一致），解除4把起走均匀分布。
    public static int Blades(SoulItem soul)=>BladesAtStage(soul,Stage(soul,SoulCurse.Solitude));
    public static int BladesAtStage(SoulItem soul,CurseStage stage)=>stage switch{
        CurseStage.Cursed=>2,CurseStage.Weakened=>3,CurseStage.Released=>4,
        _=> (soul.Seals&Moon)!=0?10:(soul.Seals&Cultist)!=0?8:(soul.Seals&Temple)!=0?7:(soul.Seals&Flower)!=0?6:4};
    public static int HitLimit(SoulItem soul)=>HitLimitAtStage(Stage(soul,SoulCurse.Bluntness));
    public static int HitLimitAtStage(CurseStage stage)=>stage switch{CurseStage.Cursed=>3,CurseStage.Weakened=>5,_=>int.MaxValue};
    // 阻隔：诅咒态允许穿过约2格厚的实体地形，削弱态约4格，解除后不再阻断。
    public static int TerrainAllowance(SoulItem soul)=>TerrainAllowanceAtStage(Stage(soul,SoulCurse.Barrier));
    public static int TerrainAllowanceAtStage(CurseStage stage)=>stage switch{CurseStage.Cursed=>32,CurseStage.Weakened=>64,_=>int.MaxValue};
    // 只迁移能明确辨认的个人击杀记录；旧邪恶标记无法区分脑和世吞，不分配给任一铭刻。
    public static int FromOldKills(int kills)
    {
        int result=0;
        int[] old=[SoulProgress.Eye,SoulProgress.Skeleton,SoulProgress.Flesh,SoulProgress.Destroyer,SoulProgress.Retinazer,
            SoulProgress.Spazmatism,SoulProgress.Prime,SoulProgress.Flower,SoulProgress.Temple,SoulProgress.Cultist,SoulProgress.Moon];
        int[] fresh=[Eye,Skeleton,Flesh,Destroyer,Retinazer,Spazmatism,Prime,Flower,Temple,Cultist,Moon];
        for(int i=0;i<old.Length;i++)if((kills&old[i])!=0)result|=fresh[i];
        return result;
    }
}

public partial class SoulItem
{
    public int Seals,LegacyReleased;
    public CurseStage Curse(SoulCurse curse)=>SoulCurses.Stage(this,curse);
    public const int CursedFlightMainLimit=4;
    public IEnumerable<Item> OreFlightMains=>Curse(SoulCurse.Switching)>=CurseStage.Released?Active:Active.Take(CursedFlightMainLimit);
    public bool TraitEnabled(int type)=>Curse(SoulCurse.Switching)>=CurseStage.Released || Active.FirstOrDefault()?.type==type;
    public IEnumerable<Item> TraitMains=>Active.Where(i=>TraitEnabled(i.type));
    public IEnumerable<Item> TraitSupports=>Supports.Where(i=>TraitEnabled(i.type));
    public IEnumerable<Item> TraitItems=>TraitMains.Concat(TraitSupports);
    // 两套口径必须分开，别合并：
    // HostedWood＝有效寄宿（主攻槽＋已生效支援槽＋奖励位＋传奇位），**不受切换诅咒过滤**，供阶段/生命/倍率/Full/森林/归冠使用；
    // CombatWood＝特性口径，带 TraitEnabled 过滤（未解切换只认第一把），只用于决定「哪些剑的特性开火」。
    public WoodLoadout HostedWood=>new(WoodTypes.Mask(SoulSupport.FamilyItems(this)),SupportCapacity,WoodCrown.Active(this));
    public WoodLoadout CombatWood=>new(WoodTypes.Mask(SoulSupport.FamilyItems(this).Where(i=>TraitEnabled(i.type))),SupportCapacity,WoodCrown.Active(this));
    public void SaveCurses(TagCompound tag){tag["curseSchema"]=1;tag["seals"]=Seals;tag["legacyReleased"]=LegacyReleased;}
    public void LoadCurses(TagCompound tag)
    {
        if(tag.ContainsKey("curseSchema")){Seals=tag.GetInt("seals")&SoulCurses.All;LegacyReleased=tag.GetInt("legacyReleased")&63;}
        else{Seals=SoulCurses.FromOldKills(Kills);LegacyReleased=Attached?63:0;}
    }
    public void SendCurses(BinaryWriter writer){writer.Write(Seals);writer.Write((byte)LegacyReleased);}
    public void ReceiveCurses(BinaryReader reader){Seals=reader.ReadInt32()&SoulCurses.All;LegacyReleased=reader.ReadByte()&63;}
}

public static class SoulTerrain
{
    public static bool SolidAt(Vector2 point)
    {
        int x=(int)MathF.Floor(point.X/16),y=(int)MathF.Floor(point.Y/16);
        if(!WorldGen.InWorld(x,y,1))return false;
        Tile tile=Main.tile[x,y];
        if(!tile.HasUnactuatedTile || !Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])return false;
        if(tile.IsHalfBlock && point.Y-y*16<8)return false;
        return tile.Slope==0 || Collision.SolidCollision(point-new Vector2(.5f),1,1);
    }
    public static bool Sweep(Vector2 from,Vector2 to,int allowance,ref float depth)
    {
        if(allowance==int.MaxValue)return false;
        float distance=Vector2.Distance(from,to);int steps=Math.Max(1,(int)Math.Ceiling(distance/3));
        for(int i=1;i<=steps;i++)
        {
            if(SolidAt(Vector2.Lerp(from,to,i/(float)steps)))depth+=distance/steps;
            else depth=0;
            if(depth>allowance)return true;
        }
        return false;
    }
}
