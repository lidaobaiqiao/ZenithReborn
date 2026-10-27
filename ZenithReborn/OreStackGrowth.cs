using System.IO;

namespace ZenithReborn;

// 一组百炼飞剑的成员数就是叠层数；每个成员保存自己的伤害快照。
// +5%与十四层上限是本轮保守初值，集中在此处调节，不增加存档进度。
public sealed class OreStackGrowth
{
    public const float DamagePerExtraStack=.05f;
    public const int MaxStacks=OreFamily.MaterialCount;
    public int Layers {get;private set;}=1;
    public bool Empowered {get;private set;}
    public int BaseDamage {get;private set;}
    public float BaseShare {get;private set;}=OreFamily.BaseFlightDamage;
    public float Multiplier=>Empowered?1+DamagePerExtraStack*(Layers-1):1;
    public int Damage=>Math.Max(1,(int)(BaseDamage*Multiplier));
    public float DamageShare=>BaseShare*Multiplier;
    public float Progress=>Empowered?(Layers-1)/(float)(MaxStacks-1):0;
    public void Reset(){Layers=1;Empowered=false;BaseDamage=0;BaseShare=OreFamily.BaseFlightDamage;}
    public void Capture(int damage,float share){BaseDamage=Math.Max(1,damage);BaseShare=share;}
    public void SetLayers(int layers,bool empowered){Layers=Math.Clamp(layers,1,MaxStacks);Empowered=empowered;}
    public void Send(BinaryWriter writer)
    {writer.Write(BaseDamage);writer.Write(BaseShare);writer.Write((byte)Layers);writer.Write(Empowered);}
    public void Receive(BinaryReader reader)
    {
        Capture(reader.ReadInt32(),reader.ReadSingle());SetLayers(reader.ReadByte(),reader.ReadBoolean());
    }
}
