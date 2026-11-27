using System.IO.Compression;
using System.Security.Cryptography;
using KomuDesk.Core;

internal static class UpdateCases
{
    public static int Run()
    {
        int passed=0;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);passed++;}
        void Reject(Action a,string label){try{a();throw new Exception("accepted: "+label);}catch(InvalidDataException){Check(true,label);}}
        Check(UpdatePackage.ParseTag("v0.1.12-preview")==new Version(0,1,12),"update version parsed");
        Check(UpdatePackage.ParseTag("v0.1.12-preview")>UpdatePackage.ParseTag("v0.1.2-preview"),"numeric version ordering");
        Check(UpdatePackage.ParseTag("v0.1.12")==null&&UpdatePackage.ParseTag("../v1.0.0-preview")==null,"update channel and path rejected");
        var now=new DateTimeOffset(2026,11,27,0,0,0,TimeSpan.Zero);
        var state=UpdateCheckState.Empty;
        Check(state.IsDue(now),"first automatic check allowed");
        state=state.Succeeded(now,"v0.1.3-preview");
        Check(!state.IsDue(now.AddHours(11))&&state.IsDue(now.AddHours(12)),"successful check throttled 12h");
        Check(state.HasNewer(new Version(0,1,2))&&!state.HasNewer(new Version(0,1,3)),"cached notice only for newer version");
        Check(UpdateCheckState.Read(state.ToStorage())==state,"check history roundtrip");
        state=state.Failed(now.AddHours(12));
        Check(!state.IsDue(now.AddHours(12.5))&&state.IsDue(now.AddHours(13)),"failed check backoff 1h");
        Check(state.Success==now&&state.HasNewer(new Version(0,1,2)),"failed check preserves last successful metadata");
        Check(UpdateCheckState.Read("corrupt")==UpdateCheckState.Empty&&state.IsDue(now.AddDays(-1)),"invalid history and clock rollback recover");
        var root=Path.Combine(Path.GetTempPath(),"komu-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var zip=Path.Combine(root,"package.zip");
            void Build(params string[] entries){if(File.Exists(zip))File.Delete(zip);using var z=ZipFile.Open(zip,ZipArchiveMode.Create);foreach(var name in entries){using var s=new StreamWriter(z.CreateEntry(name).Open());s.Write("fictional");}}
            string[] valid={"KOMU-DESK.exe","KOMU-DESK.dll","KOMU-DESK.runtimeconfig.json","ja/KOMU-DESK.resources.dll"};
            Build(valid);
            var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip)));
            Check(UpdatePackage.ParseChecksum(hash+"  "+UpdatePackage.AssetName+"\n")==hash,"checksum parser");
            Reject(()=>UpdatePackage.ParseChecksum(hash+"  other.zip"),"wrong checksum name rejected");
            UpdatePackage.Verify(zip,hash);Check(true,"valid checksum");
            Reject(()=>UpdatePackage.Verify(zip,new string('0',64)),"wrong checksum rejected");
            var data=Path.Combine(root,"data.json");File.WriteAllText(data,"fictional local data");
            var target=Path.Combine(root,"new");
            UpdatePackage.Extract(zip,target);
            Check(File.Exists(Path.Combine(target,"KOMU-DESK.exe"))&&File.ReadAllText(data)=="fictional local data","side by side extraction preserves data");
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();
                var cancelledTarget=Path.Combine(root,"cancelled");
                try{UpdatePackage.Extract(zip,cancelledTarget,cancelled.Token);throw new Exception("accepted cancelled extraction");}
                catch(OperationCanceledException){Check(!Directory.Exists(cancelledTarget),"cancelled extraction removed staging");}
            }
            Build(valid.Append("../escape.dll").ToArray());
            var bad=Path.Combine(root,"bad");
            Reject(()=>UpdatePackage.Extract(zip,bad),"zip traversal rejected");
            Check(!Directory.Exists(bad)&&!File.Exists(Path.Combine(root,"escape.dll")),"failed staging cleaned");
            Build(valid.Append("data.json").ToArray());Reject(()=>UpdatePackage.Extract(zip,bad),"local data in update rejected");
            Build(valid.Append("komu-desk.exe").ToArray());Reject(()=>UpdatePackage.Extract(zip,bad),"case-insensitive duplicate rejected");
            Build("KOMU-DESK.exe");Reject(()=>UpdatePackage.Extract(zip,bad),"incomplete update rejected");
        }
        finally{Directory.Delete(root,true);}
        return passed;
    }
}
