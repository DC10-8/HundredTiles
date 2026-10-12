using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HundredTiles.Core;
using HarmonyLib;

namespace HundredTiles.ExperimentalPatch
{
    public static class ResourceScheduleTranspiler
    {
        public static IList<CodeInstruction> Transform(IEnumerable<CodeInstruction> instructions)
        {
            var input=new List<CodeInstruction>(instructions);
            string[] prefix=("ldarg.1 ldc.i4 and stloc.0 ldloc.0 ldc.i4 mul ldc.i4.s shr stloc.1 ldloc.0 ldc.i4 mul ldc.i4 mul ldc.i4.s shr ldc.i4 and stloc.2 ldloc.0 ldc.i4.1 add ldc.i4 mul ldc.i4 mul ldc.i4.s shr ldc.i4.1 sub ldc.i4 and stloc.3 ldc.i4.m1 stloc.s ldc.i4.m1 stloc.s ldc.i4.m1 stloc.s ldc.i4.m1 stloc.s ldloca.s ldloc.1 conv.r4 ldc.r4 sub ldc.r4 add ldc.r4 mul stfld ldloca.s ldc.r4 stfld ldc.i4.s stloc.s ldloc.1 ldloc.s mul ldc.i4.s shr ldc.i4 ldloc.s mul mul ldloc.s ldloc.2 mul add stloc.s").Split(' ');
            if(input.Count<prefix.Length)throw new InvalidOperationException("Resource schedule is truncated.");
            for(int i=0;i<prefix.Length;i++)if(input[i].opcode.Name!=prefix[i])throw new InvalidOperationException("Resource schedule instruction changed at "+i);
            foreach(var pair in new Dictionary<int,int>{{1,1023},{5,512},{7,10},{11,512},{13,512},{15,10},{17,511},{23,512},{25,512},{27,10},{31,511},{55,18},{60,10},{62,512}})
                if(Convert.ToInt32(input[pair.Key].operand)!=pair.Value)throw new InvalidOperationException("Resource schedule constant changed.");
            foreach(var pair in new Dictionary<int,int>{{35,4},{37,5},{39,6},{41,7},{42,8},{52,8},{56,9},{58,9},{63,9},{66,9},{70,10}})
                if(Local(input[pair.Key].operand)!=pair.Value)throw new InvalidOperationException("Resource schedule local changed.");
            if(input.Count<72 || input[1].opcode!=OpCodes.Ldc_I4 || Convert.ToInt32(input[1].operand)!=1023 || input[3].opcode!=OpCodes.Stloc_0 ||
                input[62].opcode!=OpCodes.Ldc_I4 || Convert.ToInt32(input[62].operand)!=512 || input[63].opcode!=OpCodes.Ldloc_S || Convert.ToInt32(input[63].operand is LocalVariableInfo ? ((LocalVariableInfo)input[63].operand).LocalIndex : input[63].operand)!=9)
                throw new InvalidOperationException("Resource schedule differs from reviewed IL.");
            var output=new List<CodeInstruction>();foreach(var code in input)output.Add(new CodeInstruction(code));
            Replace(output,1,"Phase");Nop(output,2,2);
            Replace(output,5,"Row");Nop(output,6,8);
            Replace(output,11,"StartX");Nop(output,12,18);
            Replace(output,21,"EndX");Nop(output,22,32);
            Replace(output,58,"RowBase");Nop(output,59,65);
            // The remaining 512 in this method indexes the raw source grid.
            int strides=0;
            for(int i=71;i<output.Count;i++)
                if(output[i].opcode==OpCodes.Ldc_I4 && Convert.ToInt32(output[i].operand)==512)
                {output[i].operand=ResourceGrid.Side;strides++;}
            if(strides!=1)throw new InvalidOperationException("Resource source stride differs from reviewed IL.");
            return output;
        }
        static void Replace(List<CodeInstruction> codes,int index,string name)
        {codes[index].opcode=OpCodes.Call;codes[index].operand=typeof(ResourceGrid).GetMethod(name);}
        static void Nop(List<CodeInstruction> codes,int start,int end)
        {for(int i=start;i<=end;i++){codes[i].opcode=OpCodes.Nop;codes[i].operand=null;}}
        static int Local(object value) {var local=value as LocalVariableInfo;return local==null?Convert.ToInt32(value):local.LocalIndex;}
    }
}
