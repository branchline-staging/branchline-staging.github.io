using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;

const string exactPilot="7E8D7CA1B87954D14717BC2980B24F552AEDEAD5E96B0CC91717B2BA1B4CB25E";
const string resourceName="Branchline.IntegratedUpdater.exe";
const string placeholder="BRANCHLINE_EMBEDDED_COMPANION_SHA256_PLACEHOLDER";
try
{
    bool fixture=args.Length==5&&args[0]=="--fixture";
    if(args.Length!=4&&!fixture)throw new Exception("Expected [--fixture] original.exe helper.exe template.dll output.exe");
    var input=Path.GetFullPath(args[fixture?1:0]);
    var helper=Path.GetFullPath(args[fixture?2:1]);
    var template=Path.GetFullPath(args[fixture?3:2]);
    var output=Path.GetFullPath(args[fixture?4:3]);
    if(File.Exists(output)||String.Equals(input,output,StringComparison.OrdinalIgnoreCase))throw new Exception("Output must be a new file.");
    if(!fixture&&Hash(File.ReadAllBytes(input))!=exactPilot)throw new Exception("Expected exact R6 role correction pilot, not an older launcher.");
    byte[] helperBytes=File.ReadAllBytes(helper);
    if(helperBytes.Length==0||helperBytes.Length>8*1024*1024)throw new Exception("Invalid embedded helper size.");
    string digest=Hash(helperBytes);
    using var module=ModuleDefinition.ReadModule(input,new ReaderParameters{InMemory=true});
    using var source=ModuleDefinition.ReadModule(template,new ReaderParameters{InMemory=true});
    if(fixture&&module.Assembly.Name.Name!="BranchlinePatchFixture")throw new Exception("Fixture mode is limited to named test assembly.");
    if(module.EntryPoint==null||module.EntryPoint.Name!="Main"||!module.EntryPoint.IsStatic||module.EntryPoint.Parameters.Count!=1||
        module.EntryPoint.Parameters[0].ParameterType.FullName!="System.String[]"||module.Assembly.Name.HasPublicKey)
        throw new Exception("Unexpected managed launcher entry point.");
    if(module.Resources.Any(r=>r.Name==resourceName))throw new Exception("Already patched.");
    var originals=module.Resources.OfType<EmbeddedResource>().ToDictionary(r=>r.Name,r=>Hash(r.GetResourceData()));
    var from=source.Types.Single(t=>t.Name=="IntegratedHook").Methods.Single(m=>m.Name=="Start");
    var entry=module.EntryPoint;
    var method=new MethodDefinition("StartIntegratedUpdateCenter",MethodAttributes.Private|MethodAttributes.Static|MethodAttributes.HideBySig,module.TypeSystem.Void);
    method.Parameters.Add(new ParameterDefinition("args",ParameterAttributes.None,module.ImportReference(from.Parameters[0].ParameterType)));
    entry.DeclaringType.Methods.Add(method);
    method.Body.InitLocals=from.Body.InitLocals;
    method.Body.MaxStackSize=from.Body.MaxStackSize;
    foreach(var v in from.Body.Variables)method.Body.Variables.Add(new VariableDefinition(module.ImportReference(v.VariableType)));
    var map=new Dictionary<Instruction,Instruction>();
    foreach(var i in from.Body.Instructions){var copy=Instruction.Create(OpCodes.Nop);copy.OpCode=i.OpCode;map.Add(i,copy);method.Body.Instructions.Add(copy);}
    int replacements=0;
    foreach(var i in from.Body.Instructions)
    {
        object operand=i.Operand;
        if(operand is string text&&text==placeholder){operand=digest;replacements++;}
        else if(operand is Instruction target)operand=map[target];
        else if(operand is Instruction[] targets)operand=targets.Select(t=>map[t]).ToArray();
        else if(operand is VariableDefinition v)operand=method.Body.Variables[v.Index];
        else if(operand is ParameterDefinition p)operand=method.Parameters[p.Index];
        else if(operand is MethodReference mr)operand=module.ImportReference(mr);
        else if(operand is FieldReference fr)operand=module.ImportReference(fr);
        else if(operand is TypeReference tr)operand=module.ImportReference(tr);
        else if(operand!=null&&operand is not string&&operand is not int&&operand is not long&&operand is not float&&operand is not double&&operand is not byte&&operand is not sbyte)
            throw new Exception("Unexpected hook operand "+operand.GetType().FullName);
        map[i].Operand=operand;
    }
    if(replacements<1)throw new Exception("Missing embedded helper hash stamp.");
    Instruction Map(Instruction i)=>i==null?null:map[i];
    foreach(var h in from.Body.ExceptionHandlers)
        method.Body.ExceptionHandlers.Add(new ExceptionHandler(h.HandlerType){
            TryStart=Map(h.TryStart),TryEnd=Map(h.TryEnd),HandlerStart=Map(h.HandlerStart),HandlerEnd=Map(h.HandlerEnd),
            FilterStart=Map(h.FilterStart),CatchType=h.CatchType==null?null:module.ImportReference(h.CatchType)});
    module.Resources.Add(new EmbeddedResource(resourceName,ManifestResourceAttributes.Private,helperBytes));
    var first=entry.Body.Instructions.First();var il=entry.Body.GetILProcessor();
    il.InsertBefore(first,Instruction.Create(OpCodes.Ldarg_0));il.InsertBefore(first,Instruction.Create(OpCodes.Call,method));
    module.Write(output);
    using var result=ModuleDefinition.ReadModule(output,new ReaderParameters{InMemory=true});
    foreach(var r in originals)
    {
        var restored=result.Resources.OfType<EmbeddedResource>().Single(x=>x.Name==r.Key);
        if(Hash(restored.GetResourceData())!=r.Value)throw new Exception("Original managed resource changed: "+r.Key);
    }
    var embedded=result.Resources.OfType<EmbeddedResource>().Single(r=>r.Name==resourceName);
    if(Hash(embedded.GetResourceData())!=digest||result.EntryPoint.Body.Instructions[0].OpCode!=OpCodes.Ldarg_0||
        result.EntryPoint.Body.Instructions[1].OpCode!=OpCodes.Call)
        throw new Exception("Integrated output verification failed.");
    Console.WriteLine("PASS original managed resources and entry point preserved; embedded helper "+digest);
    return 0;
}
catch(Exception e){Console.Error.WriteLine(e);return 1;}
static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
