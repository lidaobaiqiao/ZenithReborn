using System.Collections.ObjectModel;
using Terraria;
using Terraria.ID;

namespace ZenithReborn;

public enum SpiritMemberScope : byte { Regular, Unconventional, Deferred }

// Type metadata only. Scope records the R2 development plan, never CanHost permission.
public sealed class SpiritMember
{
    public string Key { get; }
    public string Name { get; }
    public SpiritMemberScope Scope { get; }
    public IReadOnlyList<int> ItemTypes { get; }
    internal SpiritMember(string key,string name,SpiritMemberScope scope,params int[] types)
    {Key=key;Name=name;Scope=scope;ItemTypes=Array.AsReadOnly((int[])types.Clone());}
}

// Separate from the legacy BladeFamily.Arcane combat profile. No hooks, timers,
// player state, collection progress, support grants or combat effects are added.
public static class SpiritFamily
{
    public const string Key="spirit";
    public const string Name="灵能族";
    public static IReadOnlyDictionary<string,float> Values { get; }=
        new ReadOnlyDictionary<string,float>(new Dictionary<string,float>());
    public static IReadOnlyList<string> Traits { get; }=Array.AsReadOnly(Array.Empty<string>());
    public static IReadOnlyList<int> SupportThresholds { get; }=Array.AsReadOnly(Array.Empty<int>());
    public static IReadOnlyList<string> CollectionRewards { get; }=Array.AsReadOnly(Array.Empty<string>());

    public static IReadOnlyList<SpiritMember> Members { get; }=Array.AsReadOnly(new[]{
        Define("ice_blade","冰雪刃",SpiritMemberScope.Regular,ItemID.IceBlade),
        Define("lights_bane","魔光剑",SpiritMemberScope.Regular,ItemID.LightsBane),
        Define("phaseblade","陨石光剑",SpiritMemberScope.Regular,
            ItemID.BluePhaseblade,ItemID.RedPhaseblade,ItemID.GreenPhaseblade,
            ItemID.PurplePhaseblade,ItemID.WhitePhaseblade,ItemID.YellowPhaseblade,ItemID.OrangePhaseblade),
        Define("starfury","星怒",SpiritMemberScope.Regular,ItemID.Starfury),
        Define("enchanted_sword","附魔剑",SpiritMemberScope.Regular,ItemID.EnchantedSword),
        Define("volcano","火山",SpiritMemberScope.Regular,ItemID.FieryGreatsword),
        Define("phasesaber","晶光刃",SpiritMemberScope.Regular,
            ItemID.BluePhasesaber,ItemID.RedPhasesaber,ItemID.GreenPhasesaber,
            ItemID.PurplePhasesaber,ItemID.WhitePhasesaber,ItemID.YellowPhasesaber,ItemID.OrangePhasesaber),
        Define("brand_of_the_inferno","地狱之剑",SpiritMemberScope.Regular,ItemID.DD2SquireDemonSword),
        Define("frostbrand","寒霜剑",SpiritMemberScope.Regular,ItemID.Frostbrand),
        Define("beam_sword","光束剑",SpiritMemberScope.Regular,ItemID.BeamSword),
        Define("horsemans_blade","无头骑士剑",SpiritMemberScope.Regular,ItemID.TheHorsemansBlade),
        Define("flying_dragon","飞龙",SpiritMemberScope.Regular,ItemID.DD2SquireBetsySword),
        Define("influx_waver","波涌之刃",SpiritMemberScope.Regular,ItemID.InfluxWaver),
        Define("star_wrath","狂星之怒",SpiritMemberScope.Regular,ItemID.StarWrath),
        Define("ice_sickle","冰雪镰刀",SpiritMemberScope.Unconventional,ItemID.IceSickle),
        Define("death_sickle","死神镰刀",SpiritMemberScope.Unconventional,ItemID.DeathSickle),
        Define("starlight","星光",SpiritMemberScope.Unconventional,ItemID.PiercingStarlight),
        Define("shadowflame_knife","暗影焰刀",SpiritMemberScope.Unconventional,ItemID.ShadowFlameKnife),
        Define("paladins_hammer","圣骑士锤",SpiritMemberScope.Unconventional,ItemID.PaladinsHammer),
        Define("sky_dragons_fury","天龙之怒",SpiritMemberScope.Deferred,ItemID.MonkStaffT3),
        Define("daybreak","破晓之光",SpiritMemberScope.Deferred,ItemID.DayBreak),
        Define("solar_eruption","日耀喷发剑",SpiritMemberScope.Deferred,ItemID.SolarEruption)
    });
    private static SpiritMember Define(string key,string name,SpiritMemberScope scope,params int[] types)=>new(key,name,scope,types);
    private static readonly IReadOnlyDictionary<int,SpiritMember> byType=
        new ReadOnlyDictionary<int,SpiritMember>(Members.SelectMany(m=>m.ItemTypes.Select(t=>(Type:t,Member:m)))
            .ToDictionary(p=>p.Type,p=>p.Member));
    public static bool TryGet(int type,out SpiritMember member)=>byType.TryGetValue(type,out member);
    public static SpiritMember ForType(int type)=>byType.GetValueOrDefault(type);
    public static SpiritMember For(Item item)=>item==null || item.IsAir?null:ForType(item.type);
    public static bool IsMember(int type)=>byType.ContainsKey(type);
}

public readonly record struct HostedSpiritMember(Item Weapon,bool IsSupport,SpiritMember Member);
public partial class SoulItem
{
    // Structural inspection, NOT the future effect-enabled/curse-filtered view.
    // Active/Supports already exclude locked, paused and legacy recovery items.
    public IEnumerable<HostedSpiritMember> InspectSpiritMembers()=>
        Active.Select(item=>new HostedSpiritMember(item,false,SpiritFamily.For(item)))
        .Concat(Supports.Select(item=>new HostedSpiritMember(item,true,SpiritFamily.For(item))))
        .Where(entry=>entry.Member!=null);
}
