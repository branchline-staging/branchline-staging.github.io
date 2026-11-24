using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Branchline.UpdateCenter;

internal static class ReceiptTests
{
    static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Reject(Action action,string label)
    {
        bool rejected=false;
        try{action();}catch(InvalidOperationException){rejected=true;}catch(CryptographicException){rejected=true;}catch(IOException){rejected=true;}
        Check(rejected,label);
    }
    internal static void Run()
    {
        using(var key=new RSACryptoServiceProvider(2048))
        {
            key.PersistKeyInCsp=false;var pub=key.ExportParameters(false);
            string baseSha="CA73CCA221812B80216668AB7B0FB9EB42EB6A994DF7BE2E551D2AEF6654A139";
            var directory=new Uri("https://branchline-staging.github.io/native-releases/");
            string folder=Path.Combine(Path.GetTempPath(),"branchline-receipt-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string target=Path.Combine(folder,"Branchline Workspace.exe");File.WriteAllText(target,"simulated future version, never executable");
                var hash=LegacyInstallDetector.HashFile(target);
                var map=new Dictionary<string,object>{
                    {"schema",1},{"channel","staging"},{"platform","windows-x64"},{"sequence",7},
                    {"payloadSha256",hash},{"payloadUrl",new Uri(directory,"Branchline-Workspace-7.exe").AbsoluteUri},
                    {"supportedBaseSha256",new string[]{baseSha}}
                };
                byte[] raw=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(map));
                byte[] sig=key.SignData(raw,CryptoConfig.MapNameToOID("SHA256"));
                var offer=NativeReleaseClient.Check(pub,"staging",directory,6,baseSha,(uri,limit)=>uri.AbsolutePath.EndsWith(".json")?raw:sig);
                byte[] envelope=InstalledIdentity.MakeReceipt(offer);
                var found=LegacyInstallDetector.Scan(folder);
                Reject(()=>InstalledIdentity.Resolve(found,pub,"staging",directory),"unrecognized future bytes without receipt rejected");
                InstalledIdentity.Persist(target,envelope);
                var identity=InstalledIdentity.Resolve(found,pub,"staging",directory);
                Check(identity.Sequence==7&&identity.Sha==hash,"signed future installation recognized");
                Reject(()=>InstalledIdentity.Resolve(found,pub,"production",directory),"receipt for wrong channel rejected");
                File.AppendAllText(target,"tamper");
                Reject(()=>InstalledIdentity.Resolve(LegacyInstallDetector.Scan(folder),pub,"staging",directory),"receipt cannot authorize modified executable");
                File.WriteAllText(target,"simulated future version, never executable");
                map["sequence"]=8;map["payloadUrl"]=new Uri(directory,"Branchline-Workspace-8.exe").AbsoluteUri;map["payloadSha256"]=new string('a',64);map["supportedBaseSha256"]=new string[]{hash};
                var next=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(map));var nextSig=key.SignData(next,CryptoConfig.MapNameToOID("SHA256"));
                Check(NativeReleasePolicy.Verify(next,nextSig,pub,"staging",directory,identity.Sequence,identity.Sha).Sequence==8,"next signed release accepted using receipt sequence");
                Reject(()=>NativeReleasePolicy.Verify(raw,sig,pub,"staging",directory,identity.Sequence,identity.Sha),"older signed release rejected after update");
            }
            finally{Directory.Delete(folder,true);}
        }
    }
}
