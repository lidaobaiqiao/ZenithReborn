using System.IO;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

public partial class SoulItem
{
    public int MartialCharge;
    public int MartialTier=1;
    private static int reforgeMartialCharge,reforgeMartialTier;
    private void SaveMartial(TagCompound tag)
    {tag["martialCharge"]=Math.Clamp(MartialCharge,0,MartialTuning.MaxCharge);tag["martialTier"]=Math.Clamp(MartialTier,1,3);}
    private void LoadMartial(TagCompound tag)
    {MartialCharge=Math.Clamp(tag.GetInt("martialCharge"),0,MartialTuning.MaxCharge);MartialTier=Math.Clamp(tag.GetInt("martialTier"),1,3);}
    private void SendMartial(BinaryWriter writer)
    {writer.Write((ushort)Math.Clamp(MartialCharge,0,MartialTuning.MaxCharge));writer.Write((byte)Math.Clamp(MartialTier,1,3));}
    private void ReceiveMartial(BinaryReader reader)
    {MartialCharge=Math.Clamp((int)reader.ReadUInt16(),0,MartialTuning.MaxCharge);MartialTier=Math.Clamp((int)reader.ReadByte(),1,3);}
}
