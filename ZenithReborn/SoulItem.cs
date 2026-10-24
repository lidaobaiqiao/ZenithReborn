using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

public partial class SoulItem : ModItem
{
    public Item[] Hosts = Enumerable.Range(0, 12).Select(_ => new Item()).ToArray();
    public Item[] SupportHosts = EmptySlots(11), ForestHosts = EmptySlots(2);
    public List<Item> AdditionalSupports=[];
    public Item LegendaryHost = new();
    public const int LegendarySlotId = 34;
    public bool LegendaryOn => Attached && SoulDifficulty.Legendary;
    private IEnumerable<Item> LegendarySlots => LegendaryOn && !LegendaryHost.IsAir ? [LegendaryHost] : [];
    public IEnumerable<Item> AllSupportSlots=>SupportHosts.Take(5).Concat(AdditionalSupports);
    public static int SupportSlotId(int ordinal)
    {int id=ordinal<5?10+ordinal:25+ordinal-5;return id>=LegendarySlotId?id+1:id;}
    public static int SupportOrdinal(int slot)=>slot==LegendarySlotId?-1:slot is >=10 and <15?slot-10:
        slot>=25?(slot>LegendarySlotId?slot-1:slot)-25+5:-1;
    public int VisibleSupportCount=>Math.Max(5,Math.Max(SupportCapacity,5+AdditionalSupports.Count));
    public IEnumerable<int> VisibleSlotIds=>Enumerable.Range(0,17)
        .Concat(Enumerable.Range(5,VisibleSupportCount-5).Select(SupportSlotId))
        .Concat(LegendaryOn || !LegendaryHost.IsAir ? [LegendarySlotId] : []);
    public bool AwakeningMemory;
    public int OreCollectionMask;
    public int YujianCharge;
    private int reforgeYujian;
    public int OreCollected => OreFamily.PopCount(OreCollectionMask & OreFamily.AllMaterials);
    public bool OreMastered => (OreCollectionMask & OreFamily.AllMaterials)==OreFamily.AllMaterials;
    public void RegisterOreCollection()
    {
        if(Attached) OreCollectionMask|=OreFamily.Mask(Active.Concat(Supports));
        OreCollectionMask&=OreFamily.AllMaterials;
    }
    public WoodLoadout Wood => WoodTypes.Evaluate(this);
    public int BaseCapacity => Attached ? Math.Min(10, SoulProgress.Capacity(Kills, Cores)) : 0;
    public int Capacity => BaseCapacity;
    // Compatibility alias only: the common mechanism no longer belongs to wood.
    public int WoodSupportCapacity => SoulSupport.GrantedBy(this,"wood");
    public int SupportCapacity => SoulSupport.Capacity(this);
    // Extended arrays are retained solely to recover items saved by 0.3.9/10.
    // They never grant active slots or contribute to combat/set counts.
    public bool HasLegacyExpansion => Hosts.Skip(10).Concat(SupportHosts.Skip(5)).Any(i => !i.IsAir);
    public int BonusCapacity => Attached && (Wood.Full || OreMastered) ? 2 : 0;
    public IEnumerable<Item> Stored => Hosts.Concat(SupportHosts).Concat(ForestHosts).Concat(AdditionalSupports)
        .Concat(LegendaryHost.IsAir ? [] : [LegendaryHost]);
    public IEnumerable<Item> Supports => AllSupportSlots.Take(SupportCapacity).Where(i => !i.IsAir);
    public Item ItemAt(int slot) =>
        slot < 10 ? Hosts[slot] :
        slot < 15 ? SupportHosts[slot - 10] :
        slot < 17 ? ForestHosts[slot - 15] :
        slot < 19 ? Hosts[10 + slot - 17] :
        slot < 25 ? SupportHosts[5 + slot - 19] :
        slot == LegendarySlotId ? LegendaryHost :
        AdditionalSupports.ElementAtOrDefault(SupportOrdinal(slot)-5)??new Item();
    public Item[] SlotItems() => Enumerable.Range(0,Math.Max(LegendarySlotId+1,SupportSlotId(VisibleSupportCount-1)+1)).Select(ItemAt).ToArray();
    private static Item[] EmptySlots(int count) => Enumerable.Range(0, count).Select(_ => new Item()).ToArray();
    public string SoulId = Guid.NewGuid().ToString("N");
    public int Kills, Cores;
    public bool Attached, KingMemory;
    public IEnumerable<Item> Active => Hosts.Take(Capacity).Concat(ForestHosts.Take(BonusCapacity)).Concat(LegendarySlots)
        .Where(i => !i.IsAir);
    public int Stage => SoulProgress.Milestones(Kills);
    public bool Awakened => AwakeningMemory || CanAwaken;
    private bool CanAwaken => Attached && Cores == 63 && SoulProgress.Has(Kills, SoulProgress.All) &&
        SoulProgress.ZenithWeapons.All(type => Hosts.Take(Capacity).Any(i => i.type == type));
    public float Potency => !Attached ? 0 : Awakened ? 1.1f : .65f + Stage * .04f;
    public override string Texture => "Terraria/Images/Item_" + ItemID.Zenith;
    public override void SetStaticDefaults()=>ItemID.Sets.BonusAttackSpeedMultiplier[Type]=ItemID.Sets.BonusAttackSpeedMultiplier[ItemID.Zenith];
    public static bool CanHost(Item item) => !item.IsAir && item.damage > 0 &&
        item.CountsAsClass(DamageClass.Melee) && item.pick == 0 && item.axe == 0 && item.hammer == 0 &&
        item.ModItem is not SoulItem && item.type != ItemID.Zenith && !item.consumable && item.maxStack == 1;
    public bool TrySwapHost(int slot, ref Item cursor, out string error)
    {
        error = "";
        if (slot < 0 || slot>=25 && slot!=LegendarySlotId &&
            (SupportOrdinal(slot)<5 || SupportOrdinal(slot)>=VisibleSupportCount)) return false;
        if (slot == LegendarySlotId)
        {
            if (!cursor.IsAir && !LegendaryOn)
            { error = "传奇剑位只在实际传奇难度下生效；暂停物品仍可空手取回。"; return false; }
            if (!cursor.IsAir && !CanHost(cursor))
            { error = "请放入不可堆叠的近战武器；工具、消耗武器和天顶剑不可寄宿。"; return false; }
            int incomingLegendaryType = cursor.type;
            if (!cursor.IsAir && Stored.Any(i=>!i.IsAir && i.type==incomingLegendaryType && !ReferenceEquals(i,LegendaryHost)))
            { error = "同种武器最多寄宿一把。"; return false; }
            Item outgoingLegendary = LegendaryHost; LegendaryHost = cursor; cursor = outgoingLegendary;
            RefreshStats(); return true;
        }
        if(slot>=25)
        {
            int ordinal=SupportOrdinal(slot),localExtra=ordinal-5;
            int incomingExtraType=cursor.type;
            if(!cursor.IsAir && (ordinal>=SupportCapacity || !SoulSupport.CanSupport(cursor)))
            {error=ordinal>=SupportCapacity?"这个支援奖励尚未激活；暂停物品仍可取回。":"请放入可寄宿近战武器。";return false;}
            if(!cursor.IsAir && Stored.Any(i=>!i.IsAir && i.type==incomingExtraType && !ReferenceEquals(i,ItemAt(slot))))
            {error="同种武器最多寄宿一把。";return false;}
            while(AdditionalSupports.Count<=localExtra)AdditionalSupports.Add(new Item());
            Item outgoingExtra=AdditionalSupports[localExtra];AdditionalSupports[localExtra]=cursor;cursor=outgoingExtra;
            RefreshStats();return true;
        }
        Item[] slots;
        int local;
        bool unlocked;
        if (slot < 10)
        { slots = Hosts; local = slot; unlocked = local < Math.Max(1, Capacity); }
        else if (slot < 15)
        { slots = SupportHosts; local = slot - 10; unlocked = local < SupportCapacity; }
        else if (slot < 17)
        { slots = ForestHosts; local = slot - 15; unlocked = local < BonusCapacity; }
        else if (slot < 19)
        { slots = Hosts; local = 10 + slot - 17; unlocked = false; }
        else
        { slots = SupportHosts; local = 5 + slot - 19; unlocked = false; }
        if (!cursor.IsAir)
        {
            int incomingType = cursor.type;
            if (!unlocked) error = slot >= 17 ? "旧扩展已停用，只可空手取回，不能再放入。" : "这个槽位尚未解锁；暂停槽中的武器仍可用空鼠标取回。";
            else if (!CanHost(cursor)) error = "请放入不可堆叠的近战武器；工具、消耗武器和天顶剑不可寄宿。";
            else if (((slot >= 10 && slot < 15) || (slot >= 19 && slot < 25)) && !SoulSupport.CanSupport(cursor))
                error = "支援槽可放所有可寄宿的近战武器；工具、消耗武器和天顶剑不可放入。";
            else
            {
                int same = Stored.Count(h => !h.IsAir && h.type == incomingType);
                if (!slots[local].IsAir && slots[local].type == incomingType) same--;
                if (same > 0) error = "同种武器最多寄宿一把。";
            }
            if (error.Length > 0) return false;
        }
        AwakeningMemory |= CanAwaken;
        Item outgoing = slots[local];
        slots[local] = cursor;
        cursor = outgoing;
        RefreshStats();
        return true;
    }
    public override void SetDefaults()
    {
        SoulId = Guid.NewGuid().ToString("N");
        Hosts = Enumerable.Range(0, 12).Select(_ => new Item()).ToArray();
        SupportHosts = EmptySlots(11); ForestHosts = EmptySlots(2); AwakeningMemory = false;
        AdditionalSupports=[]; LegendaryHost = new();
        EnvironmentCollection=EnvironmentBlessings=0;DomainEnvironment=SwordEnvironment.None;
        Kills = Cores = OreCollectionMask = Seals = LegacyReleased = YujianCharge = 0;
        MartialCharge=0;MartialTier=1;
        Attached = KingMemory = false;
        Item.width = Item.height = 40; Item.damage = 20; Item.knockBack = 3;
        var zenith=new Item(ItemID.Zenith);
        Item.DamageType = zenith.DamageType; Item.useTime = zenith.useTime; Item.useAnimation = zenith.useAnimation;
        Item.useStyle = zenith.useStyle; Item.autoReuse = zenith.autoReuse;
        Item.attackSpeedOnlyAffectsWeaponAnimation=zenith.attackSpeedOnlyAffectsWeaponAnimation;
        Item.noMelee = true; Item.noUseGraphic = true;
        Item.shoot = ModContent.ProjectileType<SoulBlade>(); Item.shootSpeed = 1;
        Item.rare = ItemRarityID.Blue; Item.value = 0;
    }
    public override ModItem Clone(Item newEntity)
    {
        var copy = (SoulItem)base.Clone(newEntity);
        copy.Hosts = Hosts.Select(i => i.Clone()).ToArray();
        copy.SupportHosts = SupportHosts.Select(i => i.Clone()).ToArray();
        copy.ForestHosts = ForestHosts.Select(i => i.Clone()).ToArray();
        copy.AdditionalSupports=AdditionalSupports.Select(i=>i.Clone()).ToList();
        copy.LegendaryHost = LegendaryHost.Clone();
        return copy;
    }

    // The soul draws itself as a projectile (noUseGraphic = true), so vanilla's
    // default MeleePrefix() returns false and the reforge pool becomes the
    // AnyWeapon (spear) pool, which has no Legendary. Force the sword pool.
    public override bool MeleePrefix() => true;

    // ResetPrefix() -> Refresh() serializes/reloads the item through ItemIO.
    // Keep the soul's data across that round-trip even if the reload replaces
    // the ModItem instance.
    private static Item[] reforgeHosts;
    private static Item[] reforgeSupports, reforgeForest;
    private static List<Item> reforgeAdditional;
    private static Item reforgeLegendary;
    private static bool reforgeAwakened;
    private static string reforgeSoulId;
    private static int reforgeKills, reforgeCores, reforgeOreCollection;
    private static int reforgeSeals,reforgeLegacy;
    private static int reforgeEnvironments,reforgeEnvironmentBlessings;
    private static SwordEnvironment reforgeDomain;
    private static bool reforgeAttached, reforgeMemory;
    public override void PreReforge()
    {
        RegisterOreCollection();
        RegisterEnvironments();
        reforgeEnvironments=EnvironmentCollection;reforgeEnvironmentBlessings=EnvironmentBlessings;reforgeDomain=DomainEnvironment;
        reforgeHosts = Hosts.Select(i => i.Clone()).ToArray();
        reforgeSupports = SupportHosts.Select(i => i.Clone()).ToArray();
        reforgeForest = ForestHosts.Select(i => i.Clone()).ToArray();
        reforgeAdditional=AdditionalSupports.Select(i=>i.Clone()).ToList();
        reforgeLegendary=LegendaryHost.Clone();
        reforgeAwakened = Awakened;
        reforgeSoulId = SoulId;
        reforgeKills = Kills;
        reforgeCores = Cores;
        reforgeOreCollection = OreCollectionMask;
        reforgeYujian=YujianCharge;
        reforgeMartialCharge=MartialCharge;reforgeMartialTier=MartialTier;
        reforgeSeals=Seals;reforgeLegacy=LegacyReleased;
        reforgeAttached = Attached; reforgeMemory = KingMemory;
        RefreshStats();
        // Reforge rolls reject damage prefixes when the integer result does not
        // change (damage 1-3 + Legendary's 1.15x often rounds back to itself).
        // The soul's real damage is computed in Shoot; this value only feeds the
        // Goblin Tinkerer's prefix roll.
        Item.damage = Math.Max(Item.damage, 20);
    }
    public override void PostReforge()
    {
        if (reforgeHosts != null)
        {
            for (int i = 0; i < Hosts.Length; i++)
                if (Hosts[i].IsAir && !reforgeHosts[i].IsAir)
                    Hosts[i] = reforgeHosts[i].Clone();
            if (!string.IsNullOrEmpty(reforgeSoulId)) SoulId = reforgeSoulId;
            Kills = reforgeKills & SoulProgress.All;
            Cores = reforgeCores & 63;
            Attached = reforgeAttached; KingMemory = reforgeMemory;
            SupportHosts = reforgeSupports.Select(i => i.Clone()).ToArray();
            ForestHosts = reforgeForest.Select(i => i.Clone()).ToArray();
            AdditionalSupports=reforgeAdditional.Select(i=>i.Clone()).ToList();
            LegendaryHost=reforgeLegendary.Clone();
            AwakeningMemory = reforgeAwakened;
            OreCollectionMask = reforgeOreCollection & OreFamily.AllMaterials;
            YujianCharge=reforgeYujian;
            MartialCharge=reforgeMartialCharge;MartialTier=reforgeMartialTier;
            EnvironmentCollection=reforgeEnvironments;EnvironmentBlessings=reforgeEnvironmentBlessings;DomainEnvironment=reforgeDomain;
            NormalizeEnvironments();
            Seals=reforgeSeals;LegacyReleased=reforgeLegacy;
        }
        reforgeHosts = null;
        reforgeSupports = reforgeForest = null;
        reforgeAdditional=null; reforgeLegendary=null;
        reforgeSoulId = null;
        RefreshStats();
    }
    // HEROsMod's prefix editor rebuilds the soul with netDefaults + Prefix, so
    // ApplyPrefix is the only hook that runs on the new empty item.
    public override void ApplyPrefix(int pre)
    {
        if (HeroPrefixCompat.TryRestore(this)) RefreshStats();
    }
    public override void SaveData(TagCompound tag)
    {
        AwakeningMemory |= CanAwaken;
        RegisterOreCollection();
        RegisterEnvironments();
        tag["version"] = 9; tag["id"] = SoulId; tag["kills"] = Kills; tag["cores"] = Cores;
        SaveMartial(tag);
        tag["environments"]=EnvironmentCollection;tag["environmentBlessings"]=EnvironmentBlessings;tag["environmentDomain"]=(int)DomainEnvironment;
        SaveCurses(tag);
        tag["oreCollection"] = OreCollectionMask;
        tag["yujianCharge"]=Math.Clamp(YujianCharge,0,BlessingTuning.ChargeFrames);
        tag["attachment"] = Attached; tag["kingMemory"] = KingMemory;
        tag["hosts"] = Hosts.Select(ItemIO.Save).ToList();
        tag["supports"] = SupportHosts.Select(ItemIO.Save).ToList();
        tag["forestHosts"] = ForestHosts.Select(ItemIO.Save).ToList();
        tag["additionalSupports"]=AdditionalSupports.Select(ItemIO.Save).ToList();
        tag["legendaryHost"]=ItemIO.Save(LegendaryHost);
        tag["awakenedMemory"] = AwakeningMemory;
    }
    public override void LoadData(TagCompound tag)
    {
        SoulId = tag.GetString("id");
        if (string.IsNullOrEmpty(SoulId)) SoulId = Guid.NewGuid().ToString("N");
        Kills = tag.GetInt("kills") & SoulProgress.All; Cores = tag.GetInt("cores") & 63;
        var stored = tag.GetList<TagCompound>("hosts");
        Hosts = Enumerable.Range(0, 12).Select(i => i < stored.Count ? ItemIO.Load(stored[i]) : new Item()).ToArray();
        // Keep older, already-used souls playable; an empty old remnant starts the new prologue.
        Attached = tag.ContainsKey("attachment") ? tag.GetBool("attachment") : Hosts.Any(i=>!i.IsAir) || Kills!=0 || Cores!=0;
        KingMemory = tag.GetBool("kingMemory");
        SupportHosts = LoadSlots(tag, "supports", 11);
        ForestHosts = LoadSlots(tag, "forestHosts", 2);
        AdditionalSupports=tag.GetList<TagCompound>("additionalSupports").Select(ItemIO.Load).ToList();
        LegendaryHost = tag.ContainsKey("legendaryHost") ? ItemIO.Load(tag.GetCompound("legendaryHost")) : new Item();
        AwakeningMemory = tag.GetBool("awakenedMemory") || CanAwaken;
        OreCollectionMask=tag.GetInt("oreCollection") & OreFamily.AllMaterials;
        LoadCurses(tag);
        YujianCharge=Math.Clamp(tag.GetInt("yujianCharge"),0,BlessingTuning.ChargeFrames);
        LoadMartial(tag);
        RegisterOreCollection();
        EnvironmentCollection=tag.GetInt("environments");EnvironmentBlessings=tag.GetInt("environmentBlessings");
        DomainEnvironment=(SwordEnvironment)tag.GetInt("environmentDomain");RegisterEnvironments();NormalizeEnvironments();
    }
    private static Item[] LoadSlots(TagCompound tag, string key, int count)
    {
        var items = tag.GetList<TagCompound>(key);
        return Enumerable.Range(0, count).Select(i => i < items.Count ? ItemIO.Load(items[i]) : new Item()).ToArray();
    }
    public override void NetSend(BinaryWriter writer)
    {
        RegisterOreCollection();
        writer.Write(SoulId); writer.Write(Kills); writer.Write(Cores);
        writer.Write(Attached); writer.Write(KingMemory);
        writer.Write(Awakened);
        foreach (var host in Hosts.Concat(SupportHosts).Concat(ForestHosts)) ItemIO.Send(host, writer, writeStack: true, writeFavorite: true);
        writer.Write(OreCollectionMask);
        SendCurses(writer);
        writer.Write(AdditionalSupports.Count);
        foreach(var item in AdditionalSupports)ItemIO.Send(item,writer,writeStack:true,writeFavorite:true);
        ItemIO.Send(LegendaryHost,writer,writeStack:true,writeFavorite:true);
        writer.Write((ushort)Math.Clamp(YujianCharge,0,BlessingTuning.ChargeFrames));
        RegisterEnvironments();NormalizeEnvironments();writer.Write(EnvironmentCollection);writer.Write(EnvironmentBlessings);writer.Write((byte)DomainEnvironment);
        SendMartial(writer);
    }
    public override void NetReceive(BinaryReader reader)
    {
        SoulId = reader.ReadString(); Kills = reader.ReadInt32() & SoulProgress.All; Cores = reader.ReadInt32() & 63;
        Attached=reader.ReadBoolean();KingMemory=reader.ReadBoolean();
        AwakeningMemory = reader.ReadBoolean();
        Hosts = Enumerable.Range(0, 12).Select(_ => ItemIO.Receive(reader, readStack: true, readFavorite: true)).ToArray();
        SupportHosts = Enumerable.Range(0, 11).Select(_ => ItemIO.Receive(reader, readStack: true, readFavorite: true)).ToArray();
        ForestHosts = Enumerable.Range(0, 2).Select(_ => ItemIO.Receive(reader, readStack: true, readFavorite: true)).ToArray();
        OreCollectionMask=reader.ReadInt32() & OreFamily.AllMaterials;
        ReceiveCurses(reader);
        int count=reader.ReadInt32();if(count<0 || count>4096)throw new InvalidDataException("Invalid support slot count");
        AdditionalSupports=Enumerable.Range(0,count).Select(_=>ItemIO.Receive(reader,readStack:true,readFavorite:true)).ToList();
        LegendaryHost=ItemIO.Receive(reader,readStack:true,readFavorite:true);
        YujianCharge=Math.Clamp((int)reader.ReadUInt16(),0,BlessingTuning.ChargeFrames);
        EnvironmentCollection=reader.ReadInt32();EnvironmentBlessings=reader.ReadInt32();DomainEnvironment=(SwordEnvironment)reader.ReadByte();NormalizeEnvironments();
        ReceiveMartial(reader);
    }
    public override bool AltFunctionUse(Player player) => true;
    public override bool CanUseItem(Player player)
    {
        if (player.altFunctionUse == 2)
        {
            if (player.whoAmI == Main.myPlayer && !SoulUI.IsEditing(this)) SoulUI.Toggle(this);
            return false;
        }
        if (!Attached) return false;
        if (!Active.Any())
        {
            if (player.whoAmI == Main.myPlayer) Main.NewText("残魂无法独自攻击。按P或手持右键，放入一把近战武器。", 190, 210, 255);
            return false;
        }
        RefreshStats();
        return !SoulUI.IsEditing(this);
    }
    public void RefreshStats()
    {
        RegisterOreCollection();
        RegisterEnvironments();
        AwakeningMemory |= CanAwaken;
        var active = Active.ToArray();
        Item.damage = active.Length == 0 ? 1 : Math.Max(1, (int)(active.Average(i => i.damage) * Potency));
        // Set native timing once in SetDefaults. Do not overwrite prefix speed
        // or average the hosts' useTime when refreshing the soul's panel damage.
        Item.SetNameOverride(Awakened ? "复生·天顶剑" : SoulProgress.Mechs(Kills) == 3 ? "复苏的天顶剑魂" : "诅咒的天顶残魂");
        Item.rare = Awakened ? ItemRarityID.Red : Stage >= 4 ? ItemRarityID.Yellow : ItemRarityID.Blue;
    }
    public override void UpdateInventory(Player player) => RefreshStats();
    public override void HoldItem(Player player)
    {
        if(!Attached && !Main.dedServ && player.whoAmI==Main.myPlayer && Main.rand.NextBool(12))
        {
            int dust=Dust.NewDust(player.Center-new Vector2(8,8),16,16,DustID.BlueCrystalShard,0,-.3f,160,default,.65f);
            Main.dust[dust].noGravity=true;
        }
    }
    public override void ModifyWeaponCrit(Player player, ref float crit)
    {
        if (Active.Any()) crit += (float)Active.Average(i => i.crit);
    }
    public override float UseSpeedMultiplier(Player player)
    {
        // The old mechanical slow/flow blessing is replaced by the six curses.
        return (1 + player.GetModPlayer<WoodPlayer>().LayersFor(this) * .02f) /
            (TraitSupports.Any(i => WeaponFamilies.Of(i.type) == BladeFamily.Swift) ? .85f : 1);
    }
    public float Reach(Player player)
    {
        if(!Attached) return 0;
        // 原成长公式保留（魂芯进度、觉醒、武器Range特性），诅咒只做"保底"而不是"封顶"。
        float range = 192 + Stage * 45 + (Awakened ? 150 : 0);
        if(Active.Any() && !TraitSupports.Any(i=>WeaponFamilies.Of(i.type)==BladeFamily.Swift))
            range*=(float)Active.Average(i=>TraitEnabled(i.type)?WeaponFamilies.Range(i.type):1);
        if(TraitSupports.Any(i=>WeaponFamilies.Of(i.type)==BladeFamily.Swift))range*=.85f;
        // 缚身：最少打到屏幕左右中点；解除后最少到屏幕边界；祝福不设上限。
        const float Unbounded=1e6f;
        range=Curse(SoulCurse.Binding) switch{
            CurseStage.Cursed or CurseStage.Weakened=>Math.Max(range,SoulCurses.ScreenEdge(player)*.5f),
            CurseStage.Released=>Math.Max(range,SoulCurses.ScreenEdge(player)),
            _=>Unbounded};
        // 御剑主动技再翻倍；祝福已无上限时不再放大。
        if(range<Unbounded && player.GetModPlayer<BlessingPlayer>().BurstFor(this))range*=2;
        return range*(1+player.GetModPlayer<EnvironmentPlayer>().Stat("Reach"));
    }
    public override void UseAnimation(Player player)
    {if(!player.GetModPlayer<MartialPlayer>().BeginAttack(this))player.GetModPlayer<SoulSwingPlayer>().Begin(this);}
    // Native swing style supplies the pose; ItemCheck supplies the firing clock.
    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position,
        Vector2 velocity, int type, int damage, float knockback)
    { if(!player.GetModPlayer<MartialPlayer>().SuppressNative(this))player.GetModPlayer<SoulSwingPlayer>().FireHost(); return false; }

    public int EmitBlade(Player player, Item host, int ordinal, int hostCount)
    {
        if (player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) return Main.maxProjectiles;
        Vector2 delta = Main.MouseWorld - player.Center;
        float reach = Reach(player);
        if (delta.Length() > reach) delta = Vector2.Normalize(delta) * reach;
        if (delta.LengthSquared() < 2500) delta = delta.SafeNormalize(Vector2.UnitX * player.direction) * 50;
        int baseHostDamage = BladeDamage(player, host);
        int hostDamage = Math.Max(1, (int)(baseHostDamage * (1 + player.GetModPlayer<WoodPlayer>().LayersFor(this) * .03f)));
        float hostKnockback = player.GetWeaponKnockback(host);
        int index = Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.Center, Vector2.Zero, ModContent.ProjectileType<SoulBlade>(),
            hostDamage, hostKnockback*(TraitEnabled(host.type)&&WeaponFamilies.Of(host.type)==BladeFamily.Solid ||
                TraitSupports.Any(h=>WeaponFamilies.Of(h.type)==BladeFamily.Solid)?1.2f:1), player.whoAmI, host.type, 0, 0);
        if (index >= Main.maxProjectiles) return index;
        var blade = (SoulBlade)Main.projectile[index].ModProjectile;
        blade.ConfigureReborn(Awakened);
        blade.TargetOffset = delta.RotatedBy((ordinal - (hostCount - 1) * .5f) * .06f);
        blade.SwordScale = host.scale*(1+player.GetModPlayer<EnvironmentPlayer>().Stat("Size"));
        blade.SpecialDamage = baseHostDamage;
        // Vanilla Zenith 933: one ellipse, ai[0] = Random.Next(-100, 101).
        blade.Vertical = Main.rand.Next(-100, 101);
        Main.projectile[index].CritChance = player.GetWeaponCrit(host);
        Main.projectile[index].GetGlobalProjectile<SoulSource>().SoulId = SoulId;
        Main.projectile[index].GetGlobalProjectile<SoulSource>().SourceWeapon = host.type;
        // Use the actual active host position, not the swing's firing node or
        // item type: a duplicate sword after the fourth must not bypass Switching.
        Main.projectile[index].GetGlobalProjectile<SoulSource>().PrimaryOrdinal =
            Array.FindIndex(Active.ToArray(),item=>ReferenceEquals(item,host));
        Main.projectile[index].GetGlobalProjectile<SoulSource>().HostSlot=VisibleSlotIds.FirstOrDefault(slot=>ReferenceEquals(ItemAt(slot),host),-1);
        blade.Blessings.ReturnArmed=player.GetModPlayer<BlessingPlayer>().ConsumeReturn(Main.projectile[index]);
        Main.projectile[index].netUpdate = true;
        return index;
    }
    public override void ModifyTooltips(List<TooltipLine> tooltips)
    {
        // Keep the inventory tooltip bounded regardless of the hosted loadout.
        // Full effects, conditions and progression live in the scrollable UI.
        tooltips.Add(new(Mod,"SoulControl",Attached && MartialTuning.Enabled?"P打开魂域；G武技，V选档":"按P或手持右键打开剑魂窗口"));
        tooltips.Add(new(Mod,"SoulStats",$"主攻 {Active.Count()}/{Capacity+BonusCapacity+(LegendaryOn?1:0)} · 支援 {Supports.Count()}/{SupportCapacity}"));
        tooltips.Add(new(Mod,"TraitCodex","特性与成长详情请在窗口内查看"));
        if(!Attached) tooltips.Add(new(Mod,"Unstable","尚未附着：与友善史莱姆王对话"));
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.CopperBar,5)
        .AddIngredient(ItemID.FallenStar).AddTile(TileID.WorkBenches).Register();
}
