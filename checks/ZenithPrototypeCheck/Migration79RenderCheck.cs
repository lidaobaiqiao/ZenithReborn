using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace ZenithPrototypeCheck;

// 在隔离客户端对比迁移后的着色器与收藏的79着色器。
public class Migration79RenderCheck : ModSystem
{
    private bool ran;
    public override void Load(){if(!Main.dedServ)Main.OnPreDraw+=Capture;}
    public override void Unload()=>Main.OnPreDraw-=Capture;
    private void Capture(GameTime time)
    {
        string output=Environment.GetEnvironmentVariable("ZENITH_MIGRATION_79_RENDER");
        if(ran||string.IsNullOrEmpty(output)||!Main.gameMenu||Main.menuMode!=0)return;
        ran=true;
        Directory.CreateDirectory(output);
        try
        {
            var result=Baseline79RenderCheck.Run(output);
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(result));
        }
        catch(Exception error){File.WriteAllText(Path.Combine(output,"error.txt"),error.ToString());}
        finally{Main.instance.Exit();}
    }
}
