using System.IO;
using Terraria.ModLoader.IO;

namespace ZenithReborn;

// 仅保存旧版遗留数据和网络字段，便于旧档回退；不参与任何玩法。
public partial class SoulItem
{
    public int LegacyMartialCharge;
    public int LegacyMartialTier=1;
    private static int reforgeLegacyMartialCharge,reforgeLegacyMartialTier;
    private void SaveLegacyMartial(TagCompound tag)
    {tag["martialCharge"]=Math.Clamp(LegacyMartialCharge,0,300);tag["martialTier"]=Math.Clamp(LegacyMartialTier,1,3);}
    private void LoadLegacyMartial(TagCompound tag)
    {LegacyMartialCharge=Math.Clamp(tag.GetInt("martialCharge"),0,300);LegacyMartialTier=Math.Clamp(tag.GetInt("martialTier"),1,3);}
    private void SendLegacyMartial(BinaryWriter writer)
    {writer.Write((ushort)Math.Clamp(LegacyMartialCharge,0,300));writer.Write((byte)Math.Clamp(LegacyMartialTier,1,3));}
    private void ReceiveLegacyMartial(BinaryReader reader)
    {LegacyMartialCharge=Math.Clamp((int)reader.ReadUInt16(),0,300);LegacyMartialTier=Math.Clamp((int)reader.ReadByte(),1,3);}
}
