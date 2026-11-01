using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Reflection.Emit;using System.Security.Cryptography;using System.Text;using ColossalFramework.IO;using HundredTiles.SaveBridge;
namespace HundredTiles.WorkshopCompatibility {
 // HundredTiles only touches ColossalFramework.IO inside ColossalManaged.dll (DataSerializer hooks, EncodedArray reads/writes).
 // Fingerprint that namespace instead of the whole file, so loaders that rewrite unrelated types (StartupAccelerator patches PluginManager) are accepted.
 // Metadata tokens are resolved to names, because adding a method anywhere in the file renumbers them.
 public static class SerializerRuntime {
  public const string Namespace="ColossalFramework.IO";
  public const string ReviewedFingerprint="D593B2813F324E4F9E1A12FBE76732FFE2A28EFEF9FE1A75AA693D947DB3463D";
  public static void Verify(){string fingerprint=Fingerprint(typeof(DataSerializer).Assembly);if(fingerprint!=ReviewedFingerprint)throw new InvalidOperationException("Serializer runtime version mismatch; load ordering requires review. "+Namespace+" fingerprint="+fingerprint);
   // Preserve the independent check of the root manager's AfterDeserialize.
   SerializerHooks.Verify(typeof(SimulationManager).GetNestedType("Data").GetMethod("AfterDeserialize"));
  }
  public static string Fingerprint(Assembly assembly){var text=new StringBuilder();var types=new List<Type>();foreach(Type t in assembly.GetTypes())if(t.Namespace==Namespace)types.Add(t);types.Sort((a,b)=>string.CompareOrdinal(a.FullName,b.FullName));
   foreach(Type t in types)DescribeType(t,text);using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-","");}
  const BindingFlags Declared=BindingFlags.DeclaredOnly|BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static void DescribeType(Type t,StringBuilder text){text.Append("type ").Append(t.FullName).Append(' ').Append((int)t.Attributes).Append(" : ").Append(t.BaseType==null?"":t.BaseType.ToString());foreach(Type i in Sorted(t.GetInterfaces()))text.Append(", ").Append(i);text.Append('\n');
   var lines=new List<string>();foreach(FieldInfo f in t.GetFields(Declared))lines.Add("field "+(int)f.Attributes+" "+f.FieldType+" "+f.Name);lines.Sort(string.CompareOrdinal);foreach(string l in lines)text.Append(l).Append('\n');
   var methods=new List<MethodBase>();methods.AddRange(t.GetConstructors(Declared));methods.AddRange(t.GetMethods(Declared));var described=new List<string>();foreach(MethodBase m in methods){var one=new StringBuilder();DescribeMethod(m,one);described.Add(one.ToString());}described.Sort(string.CompareOrdinal);foreach(string d in described)text.Append(d);}
  static IEnumerable<Type> Sorted(Type[] types){var list=new List<Type>(types);list.Sort((a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));return list;}
  static void DescribeMethod(MethodBase m,StringBuilder text){text.Append("method ").Append((int)m.Attributes).Append(' ').Append(Name(m)).Append('\n');MethodBody body=m.GetMethodBody();if(body==null)return;
   text.Append(" locals");foreach(LocalVariableInfo l in body.LocalVariables)text.Append(' ').Append(l.LocalType).Append(l.IsPinned?"*":"");text.Append('\n');
   foreach(ExceptionHandlingClause c in body.ExceptionHandlingClauses)text.Append(" eh ").Append((int)c.Flags).Append(' ').Append(c.TryOffset).Append(' ').Append(c.TryLength).Append(' ').Append(c.HandlerOffset).Append(' ').Append(c.HandlerLength).Append(' ').Append(c.Flags==ExceptionHandlingClauseOptions.Clause?c.CatchType.ToString():c.Flags==ExceptionHandlingClauseOptions.Filter?c.FilterOffset.ToString():"").Append('\n');
   DescribeIL(m,body.GetILAsByteArray(),text);}
  static string Name(MemberInfo member){if(member==null)return "?";Type d=member.DeclaringType;string owner=d==null?"":d+"::";if(member is FieldInfo)return owner+((FieldInfo)member).FieldType+" "+member.Name;if(member is MethodBase){var m=(MethodBase)member;var s=new StringBuilder(owner);s.Append(m is MethodInfo?((MethodInfo)m).ReturnType+" ":"").Append(m.Name);if(m.IsGenericMethod){s.Append('<');foreach(Type a in m.GetGenericArguments())s.Append(a).Append(',');s.Append('>');}s.Append('(');foreach(ParameterInfo p in m.GetParameters())s.Append(p.ParameterType).Append(',');return s.Append(')').ToString();}return member.ToString();}
  static OpCode[] one,two;
  static void LoadOpCodes(){if(one!=null)return;var a=new OpCode[256];var b=new OpCode[256];foreach(FieldInfo f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static)){var op=(OpCode)f.GetValue(null);ushort v=(ushort)op.Value;if(op.Size==1)a[v]=op;else b[v&0xff]=op;}two=b;one=a;}
  static void DescribeIL(MethodBase m,byte[] il,StringBuilder text){LoadOpCodes();Module module=m.Module;Type[] typeArgs=m.DeclaringType!=null&&m.DeclaringType.IsGenericType?m.DeclaringType.GetGenericArguments():null;Type[] methodArgs=m.IsGenericMethod?m.GetGenericArguments():null;int i=0;
   while(i<il.Length){OpCode op=il[i]==0xFE?two[il[i+1]]:one[il[i]];i+=op.Size;text.Append(' ').Append(op.Name);
    switch(op.OperandType){
     case OperandType.InlineNone:break;
     case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:text.Append(' ').Append(il[i]);i+=1;break;
     case OperandType.InlineVar:text.Append(' ').Append(BitConverter.ToUInt16(il,i));i+=2;break;
     case OperandType.InlineBrTarget:case OperandType.InlineI:case OperandType.ShortInlineR:text.Append(' ').Append(BitConverter.ToInt32(il,i));i+=4;break;
     case OperandType.InlineI8:case OperandType.InlineR:text.Append(' ').Append(BitConverter.ToInt64(il,i));i+=8;break;
     case OperandType.InlineSwitch:{int n=BitConverter.ToInt32(il,i);i+=4;for(int k=0;k<n;k++){text.Append(' ').Append(BitConverter.ToInt32(il,i));i+=4;}break;}
     case OperandType.InlineString:text.Append(" \"").Append(module.ResolveString(BitConverter.ToInt32(il,i))).Append('"');i+=4;break;
     case OperandType.InlineSig:text.Append(" sig ").Append(BitConverter.ToString(module.ResolveSignature(BitConverter.ToInt32(il,i))));i+=4;break;
     default:text.Append(' ').Append(Name(module.ResolveMember(BitConverter.ToInt32(il,i),typeArgs,methodArgs)));i+=4;break;
    }
    text.Append('\n');}}
 }
}
