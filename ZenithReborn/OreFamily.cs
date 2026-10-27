using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

public enum OreMaterial
{
    Copper, Tin, Iron, Lead, Silver, Tungsten, Gold, Platinum,
    Cobalt, Palladium, Mythril, Orichalcum, Adamantite, Titanium
}

public static class OreFamily
{
    public const int MaterialCount = 14;
    public const int AllMaterials = (1<<MaterialCount)-1;
    public const float BaseFlightDamage=1.5f;

    // 每种材质的默认代表物品，用于飞剑外观。
    public static readonly int[] DefaultType =
    [
        ItemID.CopperBroadsword, ItemID.TinBroadsword, ItemID.IronBroadsword, ItemID.LeadBroadsword,
        ItemID.SilverBroadsword, ItemID.TungstenBroadsword, ItemID.GoldBroadsword, ItemID.PlatinumBroadsword,
        ItemID.CobaltSword, ItemID.PalladiumSword, ItemID.MythrilSword, ItemID.OrichalcumSword,
        ItemID.AdamantiteSword, ItemID.TitaniumSword
    ];

    public static readonly string[] Names =
    [
        "铜", "锡", "铁", "铅", "银", "钨", "金", "铂金",
        "钴", "钯金", "秘银", "山铜", "精金", "钛金"
    ];

    public static readonly Color[] Tints =
    [
        new(196, 112, 72), new(178, 178, 184), new(176, 150, 130), new(128, 140, 160),
        new(214, 214, 222), new(154, 190, 142), new(232, 196, 72), new(186, 206, 224),
        new(76, 128, 220), new(220, 150, 70), new(120, 196, 150), new(206, 112, 180),
        new(210, 100, 92), new(180, 196, 208)
    ];

    private static readonly int[][] MaterialSwords =
    [
        [ItemID.CopperShortsword, ItemID.CopperBroadsword],
        [ItemID.TinShortsword, ItemID.TinBroadsword],
        [ItemID.IronShortsword, ItemID.IronBroadsword],
        [ItemID.LeadShortsword, ItemID.LeadBroadsword],
        [ItemID.SilverShortsword, ItemID.SilverBroadsword],
        [ItemID.TungstenShortsword, ItemID.TungstenBroadsword],
        [ItemID.GoldShortsword, ItemID.GoldBroadsword],
        [ItemID.PlatinumShortsword, ItemID.PlatinumBroadsword],
        [ItemID.CobaltSword],
        [ItemID.PalladiumSword],
        [ItemID.MythrilSword],
        [ItemID.OrichalcumSword],
        [ItemID.AdamantiteSword],
        [ItemID.TitaniumSword]
    ];

    public static int MaterialOf(int type)
    {
        for (int i = 0; i < MaterialSwords.Length; i++)
            if (Array.IndexOf(MaterialSwords[i], type) >= 0) return i;
        return -1;
    }

    public static bool IsOre(int type) => MaterialOf(type) >= 0;

    public static int Mask(IEnumerable<Item> items)
    {
        int mask = 0;
        foreach (Item item in items)
        {
            if (item.IsAir) continue;
            int material = MaterialOf(item.type);
            if (material >= 0) mask |= 1 << material;
        }
        return mask;
    }

    public static int PopCount(int mask) => System.Numerics.BitOperations.PopCount((uint)mask);
    public static float MainDamageMultiplier(SoulItem soul,int weapon) => !IsOre(weapon) || !soul.TraitEnabled(weapon)?1:
        Count(soul)>=4?1.10f:Count(soul)>=2?1.05f:1;
    public static bool TriggerEligible(SoulItem soul,int weapon,int primaryOrdinal=-1)=>
        (primaryOrdinal<0?soul.OreFlightMains.Any(i=>i.type==weapon):
            soul.OreFlightMains.ElementAtOrDefault(primaryOrdinal)?.type==weapon) && Count(soul)>0;
    public static bool CanFlightTrait(SoulItem soul,int weapon)=>Count(soul)>=8 && OreItems(soul).Any(i=>i.type==weapon);

    // 前四把主剑例外只用于百炼飞剑；普通特性和支援仍按切换诅咒过滤。
    public static int Count(SoulItem soul)=>PopCount(Mask(OreItems(soul)));

    public static IEnumerable<Item> OreItems(SoulItem soul)=>soul.OreFlightMains.Concat(soul.TraitSupports);

    public static int[] Materials(SoulItem soul)
    {
        int mask = Mask(OreItems(soul));
        return Enumerable.Range(0, MaterialCount).Where(i => (mask & (1 << i)) != 0).ToArray();
    }

    public static Item Representative(SoulItem soul, int material)
    {
        foreach (var item in soul.Hosts.Take(soul.Capacity).Concat(soul.ForestHosts.Take(soul.BonusCapacity))
            .Concat(soul.Supports))
            if (!item.IsAir && MaterialOf(item.type) == material) return item;
        return new Item(DefaultType[material]);
    }

    public static float DamageFactor(int count,bool mastered=false) =>
        BaseFlightDamage*(1+.05f*Math.Max(0,count-1))*(count>=10?1.5f:1)*(mastered?1.3f:1);
    public static int PerSwordDamage(int sourceDamage,int count,bool mastered=false) =>
        Math.Max(1,(int)(sourceDamage*DamageFactor(count,mastered)));

    public static void OnMainHit(Projectile projectile,NPC target,int damageDone)
    {
        if(Main.netMode==NetmodeID.Server || projectile.owner!=Main.myPlayer ||
            projectile.owner<0 || projectile.owner>=Main.maxPlayers || projectile.ModProjectile is not SoulBlade blade ||
            damageDone<=0 || !HardOrePlayer.ValidTarget(target,false))return;
        var source=projectile.GetGlobalProjectile<SoulSource>();
        var soul=WoodCombat.FindSoul(Main.player[projectile.owner],source.SoulId);
        if(soul==null || !soul.Attached || Main.player[projectile.owner].dead ||
            !ReferenceEquals(Main.player[projectile.owner].HeldItem.ModItem,soul) ||
            source.PrimaryOrdinal<0 || !TriggerEligible(soul,source.SourceWeapon,source.PrimaryOrdinal) ||
            (blade.HitUnit>0 && !OreSwordTraits.EchoStacking(soul)) || !blade.TryConsumeOreHit(blade.HitUnit))return;
        int[] materials=Materials(soul);
        if(materials.Length==0)return;
        NPC root=WoodCombat.Root(target);
        if(!OreSystem.AddSword(soul,root,projectile,materials))return;
    }
}

public class OreSystem : ModSystem
{
    public const uint ProcCooldown=10,FormationTimeout=180;
    internal static readonly Dictionary<string,uint> LastGlobal=[];
    internal static readonly Dictionary<(string Soul,long Target),uint> LastTarget=[];
    internal static readonly Dictionary<string,uint> LastSkyfall=[];
    public const uint SkyfallCooldown=300;
    private sealed class Formation
    {
        public int[] Materials;
        public int WaveId;
        public uint LastHit;
        public readonly List<OreFlyingSword> Members=[];
    }
    private static readonly Dictionary<(string Soul,long Target),Formation> formations=[];
    private static bool Alive(OreFlyingSword s)=>s.Projectile.active &&
        ReferenceEquals(Main.projectile[s.Projectile.whoAmI].ModProjectile,s);
    private static void Release(Formation f)
    {
        var live=f.Members.Where(Alive).ToArray();
        // 成员由不同命中逐个生成，年龄不同；统一安排在最后一把稳定成形后齐射。
        for(int i=0;i<live.Length;i++)
        {
            live[i].Total=live.Length;live[i].RingIndex=i;
            live[i].ReleaseVolley(OreFlyingSword.GrowFrames+OreFlyingSword.ChargeFrames);
        }
    }
    internal static bool AddSword(SoulItem soul,NPC target,Projectile parent,int[] materials)
    {
        var key=(soul.SoulId,target.GetGlobalNPC<HardOreTarget>().Id);
        if(formations.TryGetValue(key,out var old) &&
            (!old.Materials.SequenceEqual(materials) || old.Members.Any(s=>!Alive(s))))
        { Release(old);formations.Remove(key); }
        if(!formations.TryGetValue(key,out var f))
        {
            f=new Formation {Materials=materials,WaveId=parent.identity,LastHit=Main.GameUpdateCount};
            formations[key]=f;
        }
        int ordinal=f.Members.Count,material=materials[ordinal];
        soul.RegisterOreCollection();
        int damage=OreFamily.PerSwordDamage(parent.damage,materials.Length,soul.OreMastered);
        int index=Projectile.NewProjectile(parent.GetSource_FromThis(),target.Center,Vector2.Zero,
            ModContent.ProjectileType<OreFlyingSword>(),damage,0,parent.owner,target.whoAmI,material,materials.Length);
        if(index>=Main.maxProjectiles)return false;
        var shot=Main.projectile[index];var mod=(OreFlyingSword)shot.ModProjectile;
        mod.BindTarget(target);mod.VisualType=OreFamily.Representative(soul,material).type;
        mod.Material=material;mod.Total=materials.Length;mod.RingIndex=ordinal;
        mod.SetCount=materials.Length;
        mod.SourceSoulId=soul.SoulId;mod.Mastered=soul.OreMastered;mod.WaveId=f.WaveId;
        mod.DamageShare=OreFamily.DamageFactor(materials.Length,soul.OreMastered);
        mod.StackGrowth.Capture(damage,mod.DamageShare);
        mod.WaitForVolley();
        if(materials.Length>=8 && WeaponFamilies.Of(mod.VisualType)==BladeFamily.Solid)
            shot.knockBack=Main.player[parent.owner].GetWeaponKnockback(OreFamily.Representative(soul,material))*1.2f;
        shot.CritChance=parent.CritChance;
        shot.GetGlobalProjectile<SoulSource>().SoulId=soul.SoulId;
        shot.GetGlobalProjectile<SoulSource>().SourceWeapon=parent.GetGlobalProjectile<SoulSource>().SourceWeapon;
        shot.netUpdate=true;f.Members.Add(mod);f.LastHit=Main.GameUpdateCount;
        // 层数只取当前阵列真实成员数，不聚合弹幕，也不反复乘已经放大的伤害。
        bool empowered=OreSwordTraits.StackDamage(soul);
        foreach(var member in f.Members)member.SetFormationStacks(f.Members.Count,empowered);
        if(f.Members.Count==materials.Length)
        {
            Release(f);formations.Remove(key);
            if(soul.OreMastered && (!LastSkyfall.TryGetValue(soul.SoulId,out uint sky) || Main.GameUpdateCount-sky>=SkyfallCooldown) &&
                OreSkyfall.Spawn(soul,target,parent))LastSkyfall[soul.SoulId]=Main.GameUpdateCount;
        }
        return true;
    }
    public override void PostUpdateEverything()
    {
        uint now=Main.GameUpdateCount;
        foreach(var pair in formations.ToArray())
        {
            var f=pair.Value;var live=f.Members.Where(Alive).ToArray();
            if(live.Length==0){formations.Remove(pair.Key);continue;}
            int target=live[0].TargetIndex;
            if(now-f.LastHit>=FormationTimeout || target<0 || target>=Main.maxNPCs || !Main.npc[target].active)
            {Release(f);formations.Remove(pair.Key);}
        }
        if(now%120!=0)return;
        foreach(var key in LastGlobal.Where(p=>now-p.Value>=ProcCooldown).Select(p=>p.Key).ToArray())LastGlobal.Remove(key);
        foreach(var key in LastTarget.Where(p=>now-p.Value>=ProcCooldown).Select(p=>p.Key).ToArray())LastTarget.Remove(key);
        foreach(var key in LastSkyfall.Where(p=>now-p.Value>=SkyfallCooldown).Select(p=>p.Key).ToArray())LastSkyfall.Remove(key);
    }
    private static void Clear(){LastGlobal.Clear();LastTarget.Clear();LastSkyfall.Clear();formations.Clear();}
    public override void OnWorldUnload()=>Clear();
    public override void Unload()=>Clear();
}
