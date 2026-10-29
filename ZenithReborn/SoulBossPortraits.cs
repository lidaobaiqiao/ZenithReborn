using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace ZenithReborn;

// 图集由模组资源系统缓存和卸载，这里不新建纹理，也不访问网络。
public static partial class SoulBossPortraits
{
    public static string Key(int seal)=>seal switch{
        SoulCurses.Eye=>"Eye",SoulCurses.Brain=>"Brain",SoulCurses.Eater=>"Eater",SoulCurses.Skeleton=>"Skeleton",
        SoulCurses.Cultist=>"Cultist",SoulCurses.Moon=>"Moon",SoulCurses.Bee=>"Bee",SoulCurses.Flower=>"Flower",
        SoulCurses.Temple=>"Temple",SoulCurses.Destroyer=>"Destroyer",SoulCurses.Retinazer|SoulCurses.Spazmatism=>"Twins",
        SoulCurses.Prime=>"Prime",SoulCurses.Flesh=>"Flesh",SoulCurses.Queen=>"Queen",SoulCurses.Empress=>"Empress",_=>""};
    public static bool HasBody(string key)=>key is "Eater" or "Skeleton" or "Destroyer" or "Queen";
    public static void Draw(int seal,Vector2 center,float size,Color tint,double seconds)
    {
        string key=Key(seal);if(!Catalog.TryGetValue(key,out var animation))return;
        const string root="ZenithReborn/Assets/UI/BossPortraits/";
        Texture2D texture=ModContent.Request<Texture2D>(root+key,AssetRequestMode.ImmediateLoad).Value;
        if(HasBody(key))
        {
            var body=ModContent.Request<Texture2D>(root+key+"Body",AssetRequestMode.ImmediateLoad).Value;
            var drift=new Vector2(0,MathF.Sin((float)(seconds%60)*2)*1.5f);
            Main.spriteBatch.Draw(body,center+drift,null,tint,0,new(48),size/96,SpriteEffects.None,0);
            Main.spriteBatch.Draw(texture,center+new Vector2(size*.34f,size*.31f),animation.FrameRectangle(animation.FrameAt(seconds*1000)),tint,0,new(48),size*.29f/96,SpriteEffects.None,0);
        }
        else Main.spriteBatch.Draw(texture,center,animation.FrameRectangle(animation.FrameAt(seconds*1000)),tint,0,new(48),size/96,SpriteEffects.None,0);
    }
}
