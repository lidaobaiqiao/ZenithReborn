using Microsoft.Xna.Framework;

namespace ZenithReborn;

// 页面身份与主导航只在此定义。旧物取回从配剑提示进入，不占用常驻导航。
public static class SoulUIPages
{
    public const int Weapons=0,Traits=1,Memories=2,Recovery=4,Environments=5;
    public static IReadOnlyList<int> Navigation {get;}=Array.AsReadOnly(new[]{Weapons,Traits,Memories,Environments});
    public static int Resolve(int value,bool hasLegacy)=>Navigation.Contains(value)||value==Recovery&&hasLegacy?value:Weapons;
    public static int Index(int value)
    {for(int i=0;i<Navigation.Count;i++)if(Navigation[i]==value)return i;return -1;}
    public static string Name(int value)=>value switch {
        Weapons=>"武器寄宿",Traits=>"剑魂特性",Memories=>"成长记忆",Recovery=>"旧物取回",_=>"环境之力"};
    public static Color Color(int value)=>value switch {
        Weapons=>new(116,224,244),Traits=>new(194,160,250),Memories=>new(252,207,113),Recovery=>new(227,170,148),_=>new(126,222,151)};
}
