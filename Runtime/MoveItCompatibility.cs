using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ColossalFramework;
using ColossalFramework.Plugins;
using HundredTiles.ExperimentalPatch;
using HundredTiles.SaveBridge;

namespace HundredTiles.Runtime
{
    // Reviewed against the installed Move It! 2.10.9. Only integer strides and
    // bounds change: the original floating-point world origins remain intact.
    public static class MoveItCompatibility
    {
        static readonly string[] Reviewed={
            "PropsManager|RaycastHoverInstance|5E7943A22C845D54DD542BF667A2919B032FC683609F6661DD2B7373C8A9FFAD|A1A282CC84B8771F5E7A63A0D2DA9B6735B0E380CC74795569648AE36F325D39",
            "PropsManager|GetMarqueeList|947FBD8C7BFD9026EFFA18682C1507E863A8A6F61F3F8CC534EBD1B127B385AA|4687BC1E196CD32A9D8127970C4B1C064ACFE5390D8ACF04F3D934CBDF3A8268",
            "MoveItTool|RaycastHoverInstance|771005D5BB0634855A1F147B0F5D4618BEFE0CC0E3D10812201DAE70528FC2D1|76303443D20D63AEA5065485DDAC67933F17F9FB4CD9F63AB08BD6FCB36C2827",
            "MoveItTool|GetMarqueeList|13973A8773CF9DDC86AB86B8F59E5B7F0E2138C3C33ABFD218831D5C607B152C|BBB510F6D319A9C4676F5AE633D42002ECB9DB2FD82E8010D6B66A500E8AAF15",
            "MoveItTool|FindOwnerBuilding|33C06E487A630961C7D2A5011E74D91D925502EAC7BE2628E9625D81B6DF4C49|73142754834CF6C596DD8DBE081B6A324449CC8794DE490087919ECD106D110B",
            "MoveItTool|UpdateRender|87A00EF8A9403FCFF2029665DE4C2AC927A5D15953FB056FFE6CF84B758E7966|8919244C33C8B66F97AC2CAE761677D839884F45BCC6CBD3EA95E735AF11EB36",
            "MoveableBuilding|AddToGrid|B4986253CDC18CCE94C36661F956719AD27C4A7348F2E24A251542B879DC7830|8E213A9864D9AA7F02E81ED66F07736793ADBBF13EEF5FEEED94610E0E495B04",
            "MoveableBuilding|RemoveFromGrid|C4E46349080CBC42EC9EA62A957D9B86A6940A2B55E4FBC190622474ECBA2CE8|47FAFDB6DFBC224D471E3AEE29A4CB7E1B7CB29EA048B0B58A29A0B96C39F0CD",
            "NodeState|FindNearestNode|31EDDAE5736221031F685C91788A2FF77E5052F86F682E88F8DEAE47CC396FAD|8AF0343046F776535927D346A64ED5360F114A1EB1CFCDC12B2A1865593E1F04"
        };
        public static void AddHooks(Assembly assembly,IList<HookSpec> hooks)
        {
            string path=assembly.Location;
            if(string.IsNullOrEmpty(path))foreach(var plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                foreach(var loaded in plugin.GetAssemblies())if(loaded==assembly)path=Path.Combine(plugin.modPath,"MoveIt.dll");
            AddReviewedHooks(assembly,path,hooks);
        }
        public static void AddReviewedHooks(Assembly assembly,string path,IList<HookSpec> hooks)
        {
            if(string.IsNullOrEmpty(path) || !File.Exists(path) || PlannedTranspiler.Hash(File.ReadAllBytes(path))!="F31ECBDB14A27195C25CB84175286674064E0E9B7E9D483D8ED5DCD3AB673896")
                throw new InvalidOperationException("Move It! version differs from reviewed 2.10.9 compatibility adapter.");
            foreach(string row in Reviewed)
            {
                string[] parts=row.Split('|');
                var target=assembly.GetType("MoveIt."+parts[0],true).GetMethod(parts[1],BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance);
                if(target==null || target.GetMethodBody()==null || ReviewedMethod.SemanticHash(target)!=parts[3])
                    throw new InvalidOperationException("Move It! spatial helper changed: "+parts[0]+"."+parts[1]);
                hooks.Add(new HookSpec{Target=target,Transpiler=typeof(MoveItCompatibility).GetMethod("SpatialGrid")});
            }
        }
        public static IEnumerable<CodeInstruction> SpatialGrid(IEnumerable<CodeInstruction> instructions)
        {
            var output=new List<CodeInstruction>();int replaced=0;
            foreach(var source in instructions)
            {
                var instruction=new CodeInstruction(source);
                if(instruction.opcode==OpCodes.Ldc_I4 || instruction.opcode==OpCodes.Ldc_I4_S)
                {
                    int value=Convert.ToInt32(instruction.operand),next=value;
                    switch(value){case 269:next=299;break;case 270:next=300;break;case 539:next=599;break;case 540:next=600;break;case 45:next=50;break;}
                    if(next!=value){instruction.opcode=OpCodes.Ldc_I4;instruction.operand=next;replaced++;}
                }
                output.Add(instruction);
            }
            if(replaced==0)throw new InvalidOperationException("Move It! reviewed grid constants missing.");
            return output;
        }
    }
}

