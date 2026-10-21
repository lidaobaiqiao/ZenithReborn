using System.Collections.ObjectModel;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ZenithReborn;

// Stable metadata IDs, matching E01-E21 in the user's environment table.
// This is an independent attribute dimension, never a BladeFamily/WoodKind.
public enum SwordEnvironment : byte
{
    None=0, Forest=1, Underground=2, Caverns=3, Underworld=4, Space=5,
    Snow=6, Ice=7, Desert=8, UndergroundDesert=9, Ocean=10,
    Jungle=11, UndergroundJungle=12, JungleTemple=13, GlowingMushroom=14,
    Corruption=15, UndergroundCorruption=16, Crimson=17, UndergroundCrimson=18,
    Hallow=19, UndergroundHallow=20, Dungeon=21
}

public sealed class EnvironmentAttributeDefinition
{
    public SwordEnvironment Id { get; }
    public string Key { get; }
    public string Name { get; }
    // Intentionally empty: absence is not a tuned zero or an active effect.
    public IReadOnlyDictionary<string,float> Values { get; }
    public IReadOnlyList<string> Traits { get; }
    internal EnvironmentAttributeDefinition(SwordEnvironment id,string key,string name)
    {
        Id=id;Key=key;Name=name;
        Values=new ReadOnlyDictionary<string,float>(new Dictionary<string,float>(StringComparer.Ordinal));
        Traits=Array.AsReadOnly(Array.Empty<string>());
    }
}

public static class EnvironmentAttributes
{
    public static IReadOnlyList<EnvironmentAttributeDefinition> All { get; }=Array.AsReadOnly(new[]{
        Define(SwordEnvironment.Forest,"forest","森林"),
        Define(SwordEnvironment.Underground,"underground","地下层"),
        Define(SwordEnvironment.Caverns,"caverns","洞穴层"),
        Define(SwordEnvironment.Underworld,"underworld","地狱"),
        Define(SwordEnvironment.Space,"space","太空"),
        Define(SwordEnvironment.Snow,"snow","雪原生物群系"),
        Define(SwordEnvironment.Ice,"ice","冰雪生物群系"),
        Define(SwordEnvironment.Desert,"desert","沙漠"),
        Define(SwordEnvironment.UndergroundDesert,"underground_desert","地下沙漠"),
        Define(SwordEnvironment.Ocean,"ocean","海洋"),
        Define(SwordEnvironment.Jungle,"jungle","丛林"),
        Define(SwordEnvironment.UndergroundJungle,"underground_jungle","地下丛林"),
        Define(SwordEnvironment.JungleTemple,"jungle_temple","丛林神庙"),
        Define(SwordEnvironment.GlowingMushroom,"glowing_mushroom","发光蘑菇生物群系"),
        Define(SwordEnvironment.Corruption,"corruption","腐化之地"),
        Define(SwordEnvironment.UndergroundCorruption,"underground_corruption","地下腐化之地"),
        Define(SwordEnvironment.Crimson,"crimson","猩红之地"),
        Define(SwordEnvironment.UndergroundCrimson,"underground_crimson","地下猩红之地"),
        Define(SwordEnvironment.Hallow,"hallow","神圣之地"),
        Define(SwordEnvironment.UndergroundHallow,"underground_hallow","地下神圣之地"),
        Define(SwordEnvironment.Dungeon,"dungeon","地牢")
    });
    private static EnvironmentAttributeDefinition Define(SwordEnvironment id,string key,string name)=>new(id,key,name);
    private static readonly IReadOnlyDictionary<SwordEnvironment,EnvironmentAttributeDefinition> byId=
        new ReadOnlyDictionary<SwordEnvironment,EnvironmentAttributeDefinition>(All.ToDictionary(d=>d.Id));
    public static bool TryGet(SwordEnvironment id,out EnvironmentAttributeDefinition definition)=>byId.TryGetValue(id,out definition);
    public static EnvironmentAttributeDefinition Get(SwordEnvironment id)=>byId.TryGetValue(id,out var d)?d:
        throw new ArgumentOutOfRangeException(nameof(id),id,"Unknown environment attribute");
}

// Authoring data by item TYPE, shared by clones/prefixes/network-loaded items.
// No random/per-instance rolls or save schema are introduced by this foundation.
public static class WeaponEnvironmentAttributes
{
    private static readonly Dictionary<int,IReadOnlyList<EnvironmentAttributeDefinition>> bindings=[];
    private static readonly IReadOnlyList<EnvironmentAttributeDefinition> empty=Array.AsReadOnly(Array.Empty<EnvironmentAttributeDefinition>());
    public static IReadOnlyList<EnvironmentAttributeDefinition> ForType(int type)=>bindings.GetValueOrDefault(type,empty);
    public static IReadOnlyList<EnvironmentAttributeDefinition> For(Item item)=>item==null || item.IsAir?empty:ForType(item.type);

    // Publish only after validation/copying: callers cannot mutate a stored tag
    // list and a bad authoring row cannot partly overwrite an existing binding.
    public static void SetForType(int type,params SwordEnvironment[] environments)
    {
        if(type<=ItemID.None)throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentNullException.ThrowIfNull(environments);
        var definitions=environments.Distinct().OrderBy(id=>(byte)id).Select(EnvironmentAttributes.Get).ToArray();
        if(definitions.Length==0)bindings.Remove(type);
        else bindings[type]=Array.AsReadOnly(definitions);
    }
    internal static void Reset()=>bindings.Clear();
    internal static void RegisterBuiltins()
    {
        // Only themes already explicit in the wooden-sword D2 member table.
        // Other weapons remain unassigned until their environment design exists.
        SetForType(ItemID.WoodenSword,SwordEnvironment.Forest);
        SetForType(ItemID.BorealWoodSword,SwordEnvironment.Snow);
        SetForType(ItemID.PalmWoodSword,SwordEnvironment.Ocean);
        SetForType(ItemID.RichMahoganySword,SwordEnvironment.Jungle);
        SetForType(ItemID.CactusSword,SwordEnvironment.Desert);
        SetForType(ItemID.EbonwoodSword,SwordEnvironment.Corruption);
        SetForType(ItemID.ShadewoodSword,SwordEnvironment.Crimson);
        SetForType(ItemID.AshWoodSword,SwordEnvironment.Underworld);
        SetForType(ItemID.PearlwoodSword,SwordEnvironment.Hallow);
    }
}

public static class ItemEnvironmentAttributeExtensions
{
    public static IReadOnlyList<EnvironmentAttributeDefinition> GetEnvironmentAttributes(this Item item)=>WeaponEnvironmentAttributes.For(item);
}

public readonly record struct HostedEnvironmentAttributes(Item Weapon,bool IsSupport,IReadOnlyList<EnvironmentAttributeDefinition> Attributes);
public partial class SoulItem
{
    // Metadata view, not an effect-enabled list or a new synergy count. These
    // existing source enumerations already exclude paused and retired storage.
    public IEnumerable<HostedEnvironmentAttributes> InspectEnvironmentAttributes()=>
        Active.Select(item=>new HostedEnvironmentAttributes(item,false,item.GetEnvironmentAttributes()))
        .Concat(Supports.Select(item=>new HostedEnvironmentAttributes(item,true,item.GetEnvironmentAttributes())));
}

public class EnvironmentAttributeSystem : ModSystem
{
    public override void Load()
    {WeaponEnvironmentAttributes.Reset();WeaponEnvironmentAttributes.RegisterBuiltins();}
    public override void Unload()=>WeaponEnvironmentAttributes.Reset();
}
