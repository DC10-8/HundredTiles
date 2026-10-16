using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace HundredTiles.ExperimentalPatch
{
    // Assembly rewriters may renumber metadata without changing instructions.
    // Compare resolved identities, including every operand, rather than tokens.
    public static class ReviewedMethod
    {
        static readonly Dictionary<short,OpCode> codes=Codes();
        static readonly Dictionary<MethodBase,MethodPlan> bound=new Dictionary<MethodBase,MethodPlan>();
        static ReviewedMethod()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve+=delegate(object sender,ResolveEventArgs args) {
                string path=System.IO.Path.Combine(AssemblyFile.ManagedDirectory,new AssemblyName(args.Name).Name+".dll");
                if(System.IO.File.Exists(path))return (Assembly)typeof(Assembly).GetMethod("ReflectionOnlyLoadFrom",new[]{typeof(string)}).Invoke(null,new object[]{path});
                return (Assembly)typeof(Assembly).GetMethod("ReflectionOnlyLoad",new[]{typeof(string)}).Invoke(null,new object[]{args.Name});
            };
        }
        static Dictionary<short,OpCode> Codes(){var result=new Dictionary<short,OpCode>();foreach(var field in typeof(OpCodes).GetFields())if(field.FieldType==typeof(OpCode)){var code=(OpCode)field.GetValue(null);result[code.Value]=code;}return result;}
        public static MethodBase Reference(Assembly assembly,int token)
        {
            var loader=typeof(Assembly).GetMethod("ReflectionOnlyLoadFrom",new[]{typeof(string)});
            var disk=(Assembly)loader.Invoke(null,new object[]{AssemblyFile.Resolve(assembly)});
            return disk.ManifestModule.ResolveMethod(token);
        }
        public static MethodBase Resolve(Assembly assembly,MethodPlan plan)
        {
            var reference=Reference(assembly,plan.Token);
            if(PlannedTranspiler.Hash(reference.GetMethodBody().GetILAsByteArray())!=plan.BodyHash)throw new InvalidOperationException("Reviewed disk method mismatch: "+plan.Name);
            var type=assembly.GetType(reference.DeclaringType.FullName,true);
            var candidates=new List<MethodBase>();
            candidates.AddRange(type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));
            candidates.AddRange(type.GetConstructors(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic));
            foreach(var method in candidates)if(Identity(method)==Identity(reference))
            {
                if(!Equivalent(reference,method))throw new InvalidOperationException("Reviewed method changed: "+plan.Name+" (resolved by signature) "+Difference(reference,method));
                lock(bound)bound[method]=plan;
                return method;
            }
            throw new MissingMethodException("Reviewed method signature missing: "+plan.Name);
        }
        public static MethodPlan Plan(MethodBase method,PatchPlan catalog)
        {
            lock(bound){MethodPlan result;if(bound.TryGetValue(method,out result))return result;}
            MethodPlan plan;
            if(!catalog.Methods.TryGetValue(method.MetadataToken,out plan) || PlannedTranspiler.Hash(method.GetMethodBody().GetILAsByteArray())!=plan.BodyHash)throw new InvalidOperationException("Method not bound to reviewed catalog.");
            return plan;
        }
        static string Identity(MemberInfo member)
        {
            var type=member as Type;
            if(type!=null)
            {
                if(type.IsByRef)return Identity(type.GetElementType())+"&";
                if(type.IsPointer)return Identity(type.GetElementType())+"*";
                if(type.IsArray)return Identity(type.GetElementType())+"["+type.GetArrayRank()+"]";
                if(type.IsGenericParameter)return (type.DeclaringMethod==null?"!":"!!")+type.GenericParameterPosition;
                string assembly=type.Assembly.GetName().Name;if(assembly=="mscorlib" || assembly=="System.Core" || assembly=="System")assembly="Framework";
                if(type.IsGenericType){var args=new List<string>();foreach(var arg in type.GetGenericArguments())args.Add(Identity(arg));return assembly+":"+type.GetGenericTypeDefinition().FullName+"<"+string.Join(",",args.ToArray())+">";}
                return assembly+":"+type.FullName;
            }
            var method=member as MethodBase;
            if(method!=null)
            {
                string name=method.Name;if(name=="FpsBoosterUpdate" || name=="FpsBoosterLateUpdate" || name=="FpsBoosterFixedUpdate")name=name.Substring(10);
                var args=new List<string>();foreach(var arg in method.GetParameters())args.Add(Identity(arg.ParameterType));
                var generics=new List<string>();if(method.IsGenericMethod)foreach(var arg in method.GetGenericArguments())generics.Add(Identity(arg));
                var info=method as MethodInfo;
                return Identity(member.DeclaringType)+":"+name+"<"+string.Join(",",generics.ToArray())+">("+string.Join(",",args.ToArray())+")->"+(info==null?"constructor":Identity(info.ReturnType));
            }
            var field=member as FieldInfo;
            if(field!=null)return Identity(member.DeclaringType)+":"+field.Name+":"+Identity(field.FieldType);
            throw new NotSupportedException("Unreviewed metadata member: "+member.MemberType);
        }
        public static string Difference(MethodBase first,MethodBase second)
        {
            var a=first.GetMethodBody();var b=second.GetMethodBody();
            var left=Normalize(first);var right=Normalize(second);
            for(int i=0;i<Math.Min(left.Count,right.Count);i++)if(left[i]!=right[i])return "Instruction "+i+": "+left[i]+" => "+right[i];
            return "Locals "+a.LocalVariables.Count+"/"+b.LocalVariables.Count+"; clauses "+a.ExceptionHandlingClauses.Count+"/"+b.ExceptionHandlingClauses.Count+"; init "+a.InitLocals+"/"+b.InitLocals+"; IL count "+left.Count+"/"+right.Count;
        }
        public static bool Equivalent(MethodBase first,MethodBase second)
        {
            var a=first.GetMethodBody();var b=second.GetMethodBody();
            if(a==null || b==null || a.InitLocals!=b.InitLocals || a.LocalVariables.Count!=b.LocalVariables.Count || a.ExceptionHandlingClauses.Count!=b.ExceptionHandlingClauses.Count)return false;
            for(int i=0;i<a.LocalVariables.Count;i++)if(a.LocalVariables[i].IsPinned!=b.LocalVariables[i].IsPinned || Identity(a.LocalVariables[i].LocalType)!=Identity(b.LocalVariables[i].LocalType))return false;
            for(int i=0;i<a.ExceptionHandlingClauses.Count;i++)
            {
                var x=a.ExceptionHandlingClauses[i];var y=b.ExceptionHandlingClauses[i];
                if(x.Flags!=y.Flags || x.TryOffset!=y.TryOffset || x.TryLength!=y.TryLength || x.HandlerOffset!=y.HandlerOffset || x.HandlerLength!=y.HandlerLength)return false;
                if(x.Flags==ExceptionHandlingClauseOptions.Clause && Identity(x.CatchType)!=Identity(y.CatchType))return false;
                if(x.Flags==ExceptionHandlingClauseOptions.Filter && x.FilterOffset!=y.FilterOffset)return false;
            }
            var left=Normalize(first);var right=Normalize(second);
            if(left.Count!=right.Count)return false;
            for(int i=0;i<left.Count;i++)if(left[i]!=right[i])return false;
            return true;
        }
        public static string SemanticHash(MethodBase method)
        {return PlannedTranspiler.Hash(System.Text.Encoding.UTF8.GetBytes(SemanticDescription(method)));}
        // Accept only a reviewed FPS Booster lifecycle prefix. The entire
        // remaining body (including locals, operands and exception regions)
        // still has to produce the original catalog hash.
        public static bool TryFpsLifecycleHash(MethodInfo method,out string hash)
        {
            hash=null;
            if(method.IsStatic || method.ReturnType!=typeof(void) || method.GetParameters().Length!=0)return false;
            var body=method.GetMethodBody();if(body==null)return false;
            var bytes=body.GetILAsByteArray();int count=0,offset=0;
            if(method.Name=="Start" && bytes.Length>=7 && bytes[0]==2 && bytes[1]==23 && bytes[2]==125)
            {
                var field=method.Module.ResolveField(BitConverter.ToInt32(bytes,3));
                if(field.IsStatic || field.FieldType!=typeof(bool) || field.Name!="fps_updateStarted" ||
                   field.DeclaringType.FullName!="ColossalFramework.UI.UIComponent" || field.DeclaringType.Assembly.GetName().Name!="ColossalManaged")return false;
                count=3;offset=7;
            }
            else if((method.Name=="OnEnable" || method.Name=="OnDisable") && bytes.Length>=6 && bytes[0]==2 && bytes[1]==40)
            {
                var target=method.Module.ResolveMethod(BitConverter.ToInt32(bytes,2)) as MethodInfo;
                if(target==null || !target.IsStatic || target.ReturnType!=typeof(void) || target.GetParameters().Length!=1 ||
                   target.DeclaringType.Assembly.GetName().Name!="BehaviourUpdater")return false;
                string type=target.DeclaringType.FullName;
                bool custom=type=="BehaviourUpdater.CustomBehaviourRegistry";
                if(!custom && type!="BehaviourUpdater.BehaviourRegistry")return false;
                bool enable=method.Name=="OnEnable";
                if(enable ? target.Name!="RegisterBehaviour" && (!custom || (target.Name!="RegisterUiBehaviour" && target.Name!="RegisterBehaviourWithStart")) :
                   target.Name!="UnRegisterBehaviour" && (!custom || target.Name!="UnRegisterUiBehaviour"))return false;
                count=2;offset=6;
            }
            else return false;
            foreach(var clause in body.ExceptionHandlingClauses)
                if(clause.TryOffset<offset || clause.HandlerOffset<offset || (clause.Flags==ExceptionHandlingClauseOptions.Filter && clause.FilterOffset<offset))return false;
            hash=PlannedTranspiler.Hash(System.Text.Encoding.UTF8.GetBytes(SemanticDescription(method,count,offset)));
            return true;
        }
        public static string SemanticDescription(MethodBase method)
        {return SemanticDescription(method,0,0);}
        static string SemanticDescription(MethodBase method,int skipInstructions,int skipBytes)
        {
            var body=method.GetMethodBody();
            var rows=new List<string>();
            rows.Add(Identity(method));rows.Add(method.IsStatic.ToString());rows.Add(body.InitLocals.ToString());
            foreach(var local in body.LocalVariables)rows.Add("local:"+local.IsPinned+":"+Identity(local.LocalType));
            foreach(var clause in body.ExceptionHandlingClauses)
                rows.Add("clause:"+clause.Flags+":"+(clause.TryOffset-skipBytes)+":"+clause.TryLength+":"+(clause.HandlerOffset-skipBytes)+":"+clause.HandlerLength+":"+(clause.Flags==ExceptionHandlingClauseOptions.Clause?Identity(clause.CatchType):clause.Flags==ExceptionHandlingClauseOptions.Filter?(clause.FilterOffset-skipBytes).ToString():""));
            var instructions=Normalize(method);if(skipInstructions!=0)instructions.RemoveRange(0,skipInstructions);rows.AddRange(instructions);
            // Length framing keeps embedded strings and metadata descriptions unambiguous.
            var text=new System.Text.StringBuilder();foreach(string row in rows)text.Append(row.Length).Append(':').Append(row);
            return text.ToString();
        }
        static List<string> Normalize(MethodBase method)
        {
            var bytes=method.GetMethodBody().GetILAsByteArray();var output=new List<string>();int at=0;
            while(at<bytes.Length)
            {
                short value=bytes[at++];if(value==254)value=(short)(0xfe00|bytes[at++]);
                var code=codes[value];string operand="";int size;
                switch(code.OperandType)
                {
                    case OperandType.InlineNone:size=0;break;
                    case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:size=1;break;
                    case OperandType.InlineVar:size=2;break;
                    case OperandType.InlineI8:case OperandType.InlineR:size=8;break;
                    case OperandType.InlineSwitch:size=4+4*BitConverter.ToInt32(bytes,at);break;
                    case OperandType.InlineField:case OperandType.InlineMethod:case OperandType.InlineType:case OperandType.InlineTok:
                        size=4;operand=Identity(method.Module.ResolveMember(BitConverter.ToInt32(bytes,at),method.DeclaringType.GetGenericArguments(),method.IsGenericMethod?method.GetGenericArguments():null));break;
                    case OperandType.InlineString:size=4;operand="string:"+method.Module.ResolveString(BitConverter.ToInt32(bytes,at));break;
                    default:size=4;break;
                }
                if(operand.Length==0 && size>0)operand=BitConverter.ToString(bytes,at,size);
                output.Add(code.Value+":"+operand);at+=size;
            }
            return output;
        }
    }
}
