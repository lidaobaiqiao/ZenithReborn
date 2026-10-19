using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using ZenithReborn;

namespace ZenithPrototypeCheck;
public class ZenithPrototypeCheck : Mod { }
public class Check : ModSystem
{
    private int checks;
    private bool ran;
    private void Assert(bool ok, string message)
    { if (!ok) throw new Exception(message); checks++; }
    // Run on the main thread after the menu is up. PostAddRecipes runs on a
    // loader worker, where Main.mouseX/Y and Main.UIScale can be overwritten
    // by the client's update loop between setting them and clicking a slot.
    public override void PostUpdateEverything()
    {
        if (ran) return;
        ran = true;
        RunChecks();
    }
    // Headless server run: no game window and no PostUpdateEverything menu loop.
    public override void PostAddRecipes()
    {
        if (!Main.dedServ || ran) return;
        ran = true;
        RunChecks();
    }
    private void RunChecks()
    {
        string path = Environment.GetEnvironmentVariable("ZENITH_CHECK_REPORT");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if(Environment.GetEnvironmentVariable("ZENITH_WOOD")=="1")
            {
                StageOneCheck.Run(Assert);
                RibbonCheck.Run(Assert);
                WoodCheck.Run(Assert);
                ProgressionCheck.Run(Assert);
                LunarCheck.Run(Assert);
                File.WriteAllText(path,$"PASS: {checks} focused checks; lunar event/countdown recovery and actual vanilla Moon Lord spawn, persistence and no-repeat/world guards; complete slot progression, actual friendly king spawn/chat, core recovery, wood slots, save/network/reforge, forest stats, DoT and ribbon regressions. No graphical client or live multiplayer validation.\n");
                Console.WriteLine("ZENITH_WOOD_PASS "+checks); Environment.Exit(0); return;
            }
            if(Environment.GetEnvironmentVariable("ZENITH_STAGE_ONE")=="1")
            {
                StageOneCheck.Run(Assert);
                RibbonCheck.Run(Assert);
                File.WriteAllText(path,$"PASS: {checks} focused checks; first-stage progression, save/network, reforge preservation, ribbon bounds, alpha fade, camera translation and teleport rejection. Graphical playtest recorded separately.\n");
                Console.WriteLine("ZENITH_STAGE_ONE_PASS "+checks); Environment.Exit(0); return;
            }
            Terraria.Localization.LanguageManager.Instance.SetLanguage(Terraria.Localization.GameCulture.FromName("zh-Hans"));
            Assert(Terraria.Localization.Language.GetTextValue("Mods.ZenithReborn.Items.DawnCore.DisplayName")=="初光魂芯","Chinese soul-core localization loads");
            var item = new Item(ModContent.ItemType<SoulItem>());
            var soul = (SoulItem)item.ModItem;
            Assert(soul.Capacity == 1 && !soul.Active.Any() && !soul.Awakened,"Empty starting soul cannot attack");
            Assert(soul.SoulId != ((SoulItem)new Item(item.type).ModItem).SoulId,"Independent soul IDs");
            Assert(SoulItem.CanHost(new Item(ItemID.CopperShortsword)),"Copper sword is a valid initial host");
            Assert(!SoulItem.CanHost(new Item(ItemID.CopperPickaxe)) && !SoulItem.CanHost(new Item(ItemID.Zenith)) && !SoulItem.CanHost(item),"Reject tools, Zenith and recursive souls");
            Assert(!SoulItem.CanHost(new Item(ItemID.WoodenBow)),"Reject non-melee hosts");
            int crimsonSigil = ModContent.ItemType<CrimsonSigil>();
            int corruptionLure = ModContent.ItemType<CorruptionLure>();
            Assert(crimsonSigil > 0 && corruptionLure > 0,"Cross-biome summon items registered");
            Assert(new Item(crimsonSigil).Name == "猩红之脊" && new Item(corruptionLure).Name == "腐化之饵","Cross-biome summon names localize");
            // Reset cursor/input state so this focused UI check does not inherit
            // mouse state from an earlier assertion or a previous run.
            Main.mouseLeft=false; Main.mouseLeftRelease=false; Main.mouseItem=new Item();
            // Use the engine's real UI coordinate transform, including the user's
            // 1.6296296 scaling, then click the same handler used by the drawn panel.
            foreach (float scale in new[]{1f,1.25f,1.6296296f,2f})
            {
                // GameInterfaceLayer.Draw already called PlayerInput.SetZoom_UI
                // before the panel draws, so SoulUI sees UI-space mouse
                // coordinates. Feed those same coordinates here. Keeping UIScale
                // non-1 still catches a regression that divides by UIScale twice.
                Main.UIScale=scale; Main.screenWidth=2560; Main.screenHeight=1600;
                Point center=SoulUI.SlotBounds(0).Center;
                Main.mouseX=center.X; Main.mouseY=center.Y;
                var uiSoul=(SoulItem)new Item(item.type).ModItem;
                Main.mouseItem=new Item(ItemID.CopperShortsword); Main.mouseItem.Prefix(PrefixID.Legendary);
                Main.mouseLeft=Main.mouseLeftRelease=true;
                Assert(SoulUI.TryClickHost(uiSoul,out _) && Main.mouseItem.IsAir && uiSoul.Hosts[0].type==ItemID.CopperShortsword,"Visible slot click inserts sword at UI scale "+scale);
                Assert(uiSoul.Hosts[0].prefix==PrefixID.Legendary,"UI insertion preserves prefix: "+scale);
                Main.mouseLeftRelease=true;
                Assert(SoulUI.TryClickHost(uiSoul,out _) && uiSoul.Hosts[0].IsAir && Main.mouseItem.type==ItemID.CopperShortsword,"Click returns the exact host at UI scale "+scale);
                Main.mouseItem=new Item(ItemID.WoodenBow); Main.mouseLeftRelease=true;
                Assert(!SoulUI.TryClickHost(uiSoul,out var error) && error.Length>0 && Main.mouseItem.type==ItemID.WoodenBow && uiSoul.Hosts[0].IsAir,"Rejected item remains on cursor: "+scale);
            }
            Main.UIScale=1f; Main.mouseItem=new Item(); Main.mouseLeft=false;
            soul.Hosts[0] = new Item(ItemID.CopperShortsword); soul.Hosts[0].Prefix(PrefixID.Legendary);
            var copy = (SoulItem)item.Clone().ModItem; copy.Hosts[0].TurnToAir();
            Assert(!soul.Hosts[0].IsAir,"Host inventory clone is deep");
            var tag = new TagCompound(); soul.SaveData(tag);
            var restored = (SoulItem)new Item(item.type).ModItem; restored.LoadData(tag);
            Assert(restored.Hosts[0].type == ItemID.CopperShortsword && restored.Hosts[0].prefix == soul.Hosts[0].prefix && restored.SoulId == soul.SoulId,"Stored weapon and prefix persist");
            using var bytes = new MemoryStream();
            soul.NetSend(new BinaryWriter(bytes)); bytes.Position = 0;
            restored.NetReceive(new BinaryReader(bytes));
            Assert(restored.Hosts[0].type == ItemID.CopperShortsword && restored.SoulId == soul.SoulId,"Soul network serialization");
            var reforgeSoul = (SoulItem)new Item(item.type).ModItem;
            reforgeSoul.Hosts[0] = new Item(ItemID.CopperShortsword);
            reforgeSoul.RefreshStats();
            ItemLoader.PreReforge(reforgeSoul.Item);
            reforgeSoul.Item.ResetPrefix();
            reforgeSoul.Item.Prefix(-2);
            ItemLoader.PostReforge(reforgeSoul.Item);
            var reforged = (SoulItem)reforgeSoul.Item.ModItem;
            Assert(!reforged.Hosts[0].IsAir && reforged.Hosts[0].type == ItemID.CopperShortsword,"Reforge keeps hosted weapon");
            var legendarySoul = (SoulItem)new Item(item.type).ModItem;
            legendarySoul.Hosts[0] = new Item(ItemID.CopperShortsword);
            legendarySoul.RefreshStats();
            ItemLoader.PreReforge(legendarySoul.Item);
            Assert(legendarySoul.Item.CanApplyPrefix(PrefixID.Legendary),"Soul accepts Legendary prefix");
            Assert(legendarySoul.Item.Prefix(PrefixID.Legendary) && legendarySoul.Item.prefix == PrefixID.Legendary,"Soul can be reforged to Legendary");
            ItemLoader.PostReforge(legendarySoul.Item);
            var legendaryAfter = (SoulItem)legendarySoul.Item.ModItem;
            Assert(!legendaryAfter.Hosts[0].IsAir && legendaryAfter.Item.prefix == PrefixID.Legendary,"Legendary reforge keeps hosted weapon and prefix");
            Assert(SoulProgress.Mechs(SoulProgress.Retinazer)==0 && SoulProgress.Mechs(SoulProgress.Retinazer|SoulProgress.Spazmatism)==1,"Both Twins required for one mechanical seal");
            var player = new Player { active = true, whoAmI = 0 }; Main.player[0] = player; Main.myPlayer = 0;
            player.ResetEffects(); player.inventory[0] = item;
            var heldSoul = (SoulItem)new Item(item.type).ModItem;
            heldSoul.Hosts[0] = new Item(ItemID.CopperShortsword);
            heldSoul.Kills = SoulProgress.Eye; heldSoul.Cores = 1;
            Main.mouseItem = heldSoul.Item;
            player.GetModPlayer<SoulPlayer>().PostUpdate();
            Main.mouseItem = new Item();
            var rebuiltItem = new Item();
            rebuiltItem.netDefaults(heldSoul.Item.netID);
            rebuiltItem.Prefix(PrefixID.Legendary);
            var rebuiltSoul = (SoulItem)rebuiltItem.ModItem;
            Assert(!rebuiltSoul.Hosts[0].IsAir && rebuiltSoul.Hosts[0].type == ItemID.CopperShortsword,"HEROsMod prefix rebuild keeps hosted weapon");
            Assert(rebuiltSoul.Kills == SoulProgress.Eye && rebuiltSoul.Cores == 1,"HEROsMod prefix rebuild keeps progression");
            var rebuiltClone = (SoulItem)rebuiltItem.Clone().ModItem;
            Assert(!rebuiltClone.Hosts[0].IsAir,"HEROsMod prefix clone keeps hosted weapon");
            typeof(SoulPlayer).GetField("Flow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player.GetModPlayer<SoulPlayer>(),1f);
            int mask = 0; float previous = 0;
            foreach (int bit in new[]{0,SoulProgress.Destroyer,SoulProgress.Retinazer|SoulProgress.Spazmatism,SoulProgress.Prime})
            {
                mask |= bit; soul.Kills = mask; float speed = soul.UseSpeedMultiplier(player);
                Assert(speed > previous,"Mechanical curse weakens after each unique group"); previous=speed;
            }
            Assert(Math.Abs(previous-1.2f)<.001f,"Three mechanical groups reverse curse to blessing");
            soul.Kills = SoulProgress.All; soul.Cores = 0;
            Assert(soul.Capacity==1,"Boss flags alone do not replace soul cores");
            soul.Cores=63; Assert(soul.Capacity==10,"All progression opens ten slots");
            for(int i=0;i<10;i++) soul.Hosts[i]=new Item(SoulProgress.ZenithWeapons[i]);
            Assert(soul.Awakened,"All ten recipe swords, cores and victories awaken Zenith");
            soul.RefreshStats(); Assert(soul.Item.Name.Contains("复生"),"Awakened form changes item name");
            soul.SaveData(tag); restored.LoadData(tag); Assert(restored.Awakened,"Completed progression persists");
            soul.Kills=0; soul.Cores=0;
            var credit=new SoulBossCredit();
            foreach(int type in new[]{NPCID.TheDestroyer,NPCID.Retinazer,NPCID.Spazmatism,NPCID.SkeletronPrime})
            {
                var npc=new NPC(); npc.SetDefaults(type);
                var p=new Projectile(); p.SetDefaults(ModContent.ProjectileType<SoulBlade>()); p.owner=0;
                p.GetGlobalProjectile<SoulSource>().SoulId=soul.SoulId;
                var modifiers=npc.GetIncomingStrikeModifiers(DamageClass.Melee,1);
                credit.ModifyHitByProjectile(npc,p,ref modifiers); credit.OnKill(npc);
                Assert((soul.Kills & SoulProgress.BossBit(type))!=0,"Sword hit credits mechanical boss: "+type);
            }
            Assert(SoulProgress.Mechs(soul.Kills)==3,"Actual boss-credit hooks clear all mechanical seals");
            Assert(Main.item.Any(i=>i.active && i.type==SoulCore.TypeFor(2)),"Mechanical soul core spawned");
            foreach(int type in SoulProgress.ZenithWeapons)
            {
                int index=Projectile.NewProjectile(player.GetSource_Misc("PrototypeCheck"),player.Center,Vector2.Zero,
                    ModContent.ProjectileType<SoulBlade>(),20,2,0,type,30,0);
                var p=Main.projectile[index]; var blade=(SoulBlade)p.ModProjectile;
                blade.TargetOffset=new Vector2(160,20); p.GetGlobalProjectile<SoulSource>().SoulId=soul.SoulId;
                for(int tick=0;tick<15;tick++) blade.AI();
                Assert(p.active && p.Center.X>player.Center.X,"Sword trajectory and special dispatch: "+type);
                float expectedRotation = (p.Center - player.Center).ToRotation() + MathHelper.PiOver4;
                Assert(Math.Abs(MathHelper.WrapAngle(p.rotation - expectedRotation)) < .05f,"Blade tip points outward: "+type);
                Assert(blade.CanDamage()!=false,"Blade can hit after launch: "+type);
                p.Kill();
            }
            int SigilCount() => Main.item.Where(i => i.active && i.type == crimsonSigil).Sum(i => i.stack);
            int LureCount() => Main.item.Where(i => i.active && i.type == corruptionLure).Sum(i => i.stack);
            WorldGen.crimson = false;
            var eaterNpc = new NPC(); eaterNpc.SetDefaults(NPCID.EaterofWorldsHead);
            var brainNpc = new NPC(); brainNpc.SetDefaults(NPCID.BrainofCthulhu);
            int sigils = SigilCount(), lures = LureCount();
            new CrossSummonDrops().OnKill(eaterNpc);
            Assert(SigilCount() == sigils + 1,"Corruption EoW drops Crimson Sigil");
            new CrossSummonDrops().OnKill(brainNpc);
            Assert(LureCount() == lures,"Corruption Brain drops no lure");
            WorldGen.crimson = true;
            new CrossSummonDrops().OnKill(brainNpc);
            Assert(LureCount() == lures + 1,"Crimson Brain drops Corrupted Lure");
            int sigilsAfter = SigilCount();
            new CrossSummonDrops().OnKill(eaterNpc);
            Assert(SigilCount() == sigilsAfter,"Crimson EoW drops no sigil");
            WorldGen.crimson = false;
            File.WriteAllText(path,$"PASS: {checks} focused prototype checks. Real tModLoader load; UI-space slot click at 100/125/163/200 percent scaling (the GameInterfaceLayer.Draw contract), host insert/retrieve/reject with prefixes, independent IDs, item clone/save/network preservation, ten-slot awakening, grouped mechanical curse and boss credit/core drop, cross-biome evil summons and drops, all ten blade trajectories and special dispatch. No graphical rendering, combat balance or live multiplayer test.\n");
            Console.WriteLine("ZENITH_PROTOTYPE_PASS "+checks); Environment.Exit(0);
        }
        catch(Exception error)
        { File.WriteAllText(path,"FAIL: "+error); Console.WriteLine(error); Environment.Exit(1); }
    }
}
