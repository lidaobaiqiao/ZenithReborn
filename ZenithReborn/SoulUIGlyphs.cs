using Microsoft.Xna.Framework;

namespace ZenithReborn;

internal enum SoulGlyph { Orbit, Spark, Memory, Book, Return, World, Leaf, Ingot, Blade, Core, Lock, Check, Arrow, Shield, Chain, BrokenBlade }

public partial class SoulUI
{
    // 同一线宽与留白体系的矢量徽记，不新增贴图；每个页面与信息类别均有独立形状。
    private static void Glyph(SoulGlyph kind,Vector2 at,float size,Color color)
    {
        float k=size/24f;
        void L(float x,float y,float xx,float yy)=>Line(at+new Vector2(x,y)*k,at+new Vector2(xx,yy)*k,color,1.4f*k);
        void A(float radius,float start,float length)=>Arc(at,radius*k,start,length,color,1.25f*k);
        switch(kind)
        {
            case SoulGlyph.Orbit:
                A(8,0,MathHelper.TwoPi);L(0,-12,0,-5);L(0,5,0,12);L(-12,0,-5,0);L(5,0,12,0);break;
            case SoulGlyph.Spark:
                L(0,-11,3,-3);L(3,-3,11,0);L(11,0,3,3);L(3,3,0,11);
                L(0,11,-3,3);L(-3,3,-11,0);L(-11,0,-3,-3);L(-3,-3,0,-11);break;
            case SoulGlyph.Memory:
                A(10,.15f,5.5f);L(-11,-10,-11,-3);L(-11,-3,-4,-3);L(0,-6,0,1);L(0,1,5,4);break;
            case SoulGlyph.Book:
                L(0,-7,0,11);L(-10,-10,0,-7);L(0,-7,10,-10);L(-10,-10,-10,7);L(10,-10,10,7);
                L(-10,7,0,11);L(0,11,10,7);L(-7,-3,-3,-2);L(3,-2,7,-3);break;
            case SoulGlyph.Return:
                L(8,-8,8,5);L(8,5,-8,5);L(-8,5,-2,-1);L(-8,5,-2,11);L(-5,-9,3,-9);break;
            case SoulGlyph.World:
                A(11,0,MathHelper.TwoPi);L(-10,-3,10,-3);L(-10,4,10,4);
                L(-1,-11,-5,-2);L(-5,-2,-1,11);L(2,-11,5,1);L(5,1,2,11);break;
            case SoulGlyph.Leaf:
                L(-8,7,-7,-3);L(-7,-3,3,-10);L(3,-10,10,-10);L(10,-10,9,1);L(9,1,1,8);L(1,8,-8,7);
                L(-10,11,6,-5);L(-2,3,-3,-3);L(2,-1,7,0);break;
            case SoulGlyph.Ingot:
                L(-11,4,-6,-5);L(-6,-5,7,-5);L(7,-5,11,4);L(11,4,7,9);L(7,9,-8,9);L(-8,9,-11,4);
                L(-11,4,11,4);L(-6,-5,-2,4);L(7,-5,3,4);break;
            case SoulGlyph.Blade:
                L(-5,5,4,-10);L(4,-10,10,-12);L(10,-12,9,-5);L(9,-5,-1,8);
                L(-8,1,3,10);L(-8,11,-3,5);L(-10,9,-6,13);break;
            case SoulGlyph.Core:
                L(0,-12,8,-3);L(8,-3,5,7);L(5,7,0,12);L(0,12,-6,5);L(-6,5,-8,-4);L(-8,-4,0,-12);
                L(0,-12,-1,3);L(-1,3,0,12);L(-8,-4,-1,3);L(-1,3,8,-3);break;
            case SoulGlyph.Lock:
                A(7,MathHelper.Pi,MathHelper.Pi);L(-7,0,-7,10);L(-7,10,7,10);L(7,10,7,0);L(-7,0,7,0);L(0,4,0,7);break;
            case SoulGlyph.Check:L(-8,0,-2,6);L(-2,6,9,-7);break;
            case SoulGlyph.Arrow:L(-10,0,10,0);L(4,-6,10,0);L(4,6,10,0);break;
            case SoulGlyph.Shield:
                L(-9,-8,0,-11);L(0,-11,9,-8);L(9,-8,8,3);L(8,3,0,12);L(0,12,-8,3);L(-8,3,-9,-8);
                L(0,-7,0,7);L(-5,0,5,0);break;
            case SoulGlyph.Chain:
                L(-9,2,-11,-3);L(-11,-3,-6,-8);L(-6,-8,-1,-6);L(-1,-6,1,-1);
                L(-1,1,1,6);L(1,6,6,8);L(6,8,11,3);L(11,3,9,-2);L(-4,-3,4,3);break;
            case SoulGlyph.BrokenBlade:
                L(1,-3,5,-10);L(5,-10,10,-12);L(10,-12,9,-5);L(9,-5,5,0);
                L(-5,1,-8,7);L(-8,7,-4,10);L(-4,10,1,4);L(-10,3,0,11);
                L(-1,-1,1,1);L(2,2,4,4);break;
        }
    }
    private static SoulGlyph PageGlyph(int value)=>value switch {
        SoulUIPages.Weapons=>SoulGlyph.Orbit,SoulUIPages.Traits=>SoulGlyph.Spark,SoulUIPages.Memories=>SoulGlyph.Memory,
        SoulUIPages.Recovery=>SoulGlyph.Return,_=>SoulGlyph.World};
    private static SoulGlyph CategoryGlyph(TraitCategory value)=>value switch {
        TraitCategory.Wood=>SoulGlyph.Leaf,TraitCategory.Ore=>SoulGlyph.Ingot,TraitCategory.Sword=>SoulGlyph.Blade,TraitCategory.Heaven=>SoulGlyph.Spark,TraitCategory.Living=>SoulGlyph.Blade,_=>SoulGlyph.Core};
    private static SoulGlyph CurseGlyph(SoulCurse curse)=>curse switch {
        SoulCurse.Binding=>SoulGlyph.Chain,SoulCurse.Switching=>SoulGlyph.Spark,SoulCurse.Sheathing=>SoulGlyph.Return,
        SoulCurse.Barrier=>SoulGlyph.Shield,SoulCurse.Solitude=>SoulGlyph.Orbit,_=>SoulGlyph.BrokenBlade};
}
