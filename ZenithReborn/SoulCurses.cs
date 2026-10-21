using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

public enum SoulCurse { Binding, Switching, Sheathing, Barrier, Solitude, Bluntness }
public enum CurseStage { Cursed, Weakened, Released, Blessed }

public static class SoulCurses
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
        return count==total?CurseStage.Blessed:count>=total-1?CurseStage.Released:count==1?CurseStage.Weakened:CurseStage.Cursed;
    }
    public static CurseStage Stage(SoulItem soul,SoulCurse curse)
    {
        var actual=RecordedStage(soul,curse);
        return (soul.LegacyReleased&(1<<(int)curse))!=0 && actual<CurseStage.Released?CurseStage.Released:actual;
    }
    public static string StageName(CurseStage stage)=>stage switch{CurseStage.Cursed=>"诅咒中",CurseStage.Weakened=>"已削弱",CurseStage.Released=>"已解除",_=>"已祝福"};
    public static int Blades(SoulItem soul)=>Stage(soul,SoulCurse.Solitude) switch{
        CurseStage.Cursed=>1,CurseStage.Weakened=>2,CurseStage.Released=>3,
        _=> (soul.Seals&Moon)!=0?10:(soul.Seals&Cultist)!=0?8:(soul.Seals&Temple)!=0?7:(soul.Seals&Flower)!=0?6:4};
    public static int HitLimit(SoulItem soul)=>Stage(soul,SoulCurse.Bluntness) switch{CurseStage.Cursed=>1,CurseStage.Weakened=>2,_=>int.MaxValue};
    public static int TerrainAllowance(SoulItem soul)=>Stage(soul,SoulCurse.Barrier) switch{CurseStage.Cursed=>0,CurseStage.Weakened=>32,_=>int.MaxValue};
    public static string Description(SoulItem soul,SoulCurse curse)
    {
        var stage=Stage(soul,curse);bool free=stage>=CurseStage.Released;
        string effect=curse switch{
            SoulCurse.Binding=>free?"御剑射程惩罚已解除，保留正常鼠标选点。":"御剑距离降为65%，至少160像素；仍可鼠标自由选点。",
            SoulCurse.Switching=>free?"所有有效寄宿武器的特性正常生效，全部有效主剑可参与百炼飞剑。":"普通特性仅首把主剑生效；百炼飞剑例外允许前4把有效主剑计入矿材并各自首击积攒，其余主剑仍保留独立伤害。支援仍受普通特性限制。",
            SoulCurse.Sheathing=>free?"新轮不打断旧轮，主剑各自完成攻击。":"开始新轮时，旧轮主剑提前停止攻击并淡出；不锁攻击键。",
            SoulCurse.Barrier=>free?"主剑正常穿越实体地形。":stage==CurseStage.Weakened?"每段连续实体地形可穿过32像素，超出则退出攻击。":"实体地形阻断主剑攻击；背景墙、平台不阻断。",
            SoulCurse.Solitude=>$"每轮最多{Blades(soul)}把主剑，不足留空。",
            _=>free?"同剑按正常8帧间隔重复命中。":$"同剑本趟对同目标最多{HitLimit(soul)}次直接命中，不限制沿途其他敌人。"};
        if(stage==CurseStage.Blessed)effect+=" "+(curse switch{
            SoulCurse.Binding=>"战斗约15秒蓄满御剑条，满条保留；按独立御剑键（默认O）手动开启10秒，视野与操控距离翻倍，按命中距离最高独立增伤50%。",
            SoulCurse.Switching=>"百刃接力：不同寄宿主剑首击同一敌人接力，每层强化后续首击特性10%，最多5层；3秒断连清空，同剑帧伤不叠层。",
            SoulCurse.Sheathing=>"归鞘留锋：主剑完整往返后为自身存1次强化，下次登场首击释放50%自身伤害的短距离斩痕。",
            SoulCurse.Barrier=>"破障：每把主剑穿过实体后，下次命中附加4秒破甲（15护甲穿透），每剑只触发一次。",
            SoulCurse.Bluntness=>"剑痕：同一主剑2秒内命中同一敌人3次，引爆60%自身伤害，每趟每目标一次。",
            _=>""});
        if(stage==CurseStage.Blessed && curse==SoulCurse.Solitude)effect+=" 三王后4把，世花6、石巨人7、教徒8、月总10；只取此剑魂铭刻。";
        if((soul.LegacyReleased&(1<<(int)curse))!=0 && RecordedStage(soul,curse)<CurseStage.Released)effect+=" 旧档能力兼容解咒，不代表补齐Boss铭刻或祝福。";
        return effect;
    }
    public static string Requirement(SoulItem soul,SoulCurse curse)
    {
        int i=(int)curse;string marks=string.Join("、",Groups[i].Select((b,n)=>((soul.Seals&b)==b?"✓":"○")+BossNames[i][n]));
        return marks+"；"+(Groups[i].Length==2?"第一位解除，第二位祝福。":"第一位削弱，第二位解除，第三位祝福。");
    }
    // Only unambiguous PERSONAL records migrate. The old Evil bit cannot tell
    // Brain from Eater and is deliberately not assigned to either seal.
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
    public WoodLoadout CombatWood=>new(WoodTypes.Mask(SoulSupport.SetItems(this).Where(i=>TraitEnabled(i.type))),SupportCapacity);
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
