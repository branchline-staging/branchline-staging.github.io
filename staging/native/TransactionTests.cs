using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Branchline.UpdateCenter;

internal static class TransactionTests
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
        using(var stream=new MemoryStream(new byte[17]))
            Reject(()=>NativeReleaseClient.ReadBounded(stream,16),"oversized network response rejected");
        using(var stream=new MemoryStream(new byte[16]))
            Check(NativeReleaseClient.ReadBounded(stream,16).Length==16,"bounded response accepted");
        string folder=Path.Combine(Path.GetTempPath(),"branchline-transaction-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string target=Path.Combine(folder,"Branchline Workspace.exe");
            string data=Path.Combine(folder,"user-data.txt");
            File.WriteAllText(target,"original test bytes; not an executable");
            File.WriteAllText(data,"user data must remain");
            string original=LegacyInstallDetector.HashFile(target);
            byte[] payload=Encoding.UTF8.GetBytes("new test bytes; not an executable");
            string payloadHash;
            using(var sha=SHA256.Create())payloadHash=BitConverter.ToString(sha.ComputeHash(payload)).Replace("-","");
            using(var key=new RSACryptoServiceProvider(2048))
            {
                key.PersistKeyInCsp=false;
                var pub=key.ExportParameters(false);
                var dir=new Uri("https://branchline-staging.github.io/native-releases/");
                var map=new Dictionary<string,object>{
                    {"schema",1},{"channel","staging"},{"platform","windows-x64"},{"sequence",7},
                    {"payloadSha256",payloadHash},{"payloadUrl",new Uri(dir,"Branchline-Workspace-7.exe").AbsoluteUri},
                    {"supportedBaseSha256",new string[]{original}}
                };
                byte[] raw=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(map));
                byte[] sig=key.SignData(raw,CryptoConfig.MapNameToOID("SHA256"));
                var requests=new List<string>();
                Func<Uri,int,byte[]> mock=(uri,limit)=>{
                    requests.Add(uri.AbsolutePath);
                    if(uri.AbsolutePath.EndsWith("/release.json")&&limit==16384)return raw;
                    if(uri.AbsolutePath.EndsWith("/release.sig")&&limit==512)return sig;
                    if(uri.AbsolutePath.EndsWith("/Branchline-Workspace-7.exe")&&limit==NativeReleaseClient.MaxPackageBytes)return payload;
                    throw new Exception("unexpected download");
                };
                var offer=NativeReleaseClient.Check(pub,"staging",dir,6,original,mock);
                Check(requests.Count==2,"check downloads manifest and signature only");
                Reject(()=>NativeReleaseClient.Check(pub,"staging",new Uri("http://example.com/native-releases/"),6,original,mock),"HTTP rejected before network");
                Check(requests.Count==2,"invalid endpoint not contacted");
                Reject(()=>NativeReleaseClient.FetchPackage(offer,pub,"production",dir,6,original,mock),"re-verifies channel before package download");
                Check(requests.Count==2,"wrong channel package not contacted");
                var plan=NativeReleaseClient.FetchPackage(offer,pub,"staging",dir,6,original,mock);
                Check(requests.Count==3,"only verified payload URL requested");
                var copy=plan.Package();copy[0]^=1;
                Check(plan.Package()[0]==payload[0],"verified package cannot be mutated via returned copy");
                Reject(()=>SignedUpdatePlan.Create(raw,sig,pub,"staging",dir,6,original,new byte[]{1,2,3}),"invalid payload refused before disk write");
                Check(LegacyInstallDetector.HashFile(target)==original,"download verification leaves install intact");
                Reject(()=>NativeUpdateTransaction.ReplaceVerified(target,plan,()=>{throw new InvalidOperationException("running");},null),"open application prevents replacement");
                Check(LegacyInstallDetector.HashFile(target)==original,"open app unchanged");
                Reject(()=>NativeUpdateTransaction.ReplaceVerified(target,plan,()=>{},()=>{throw new IOException("simulated post-install failure");}),"post-install failure rolled back");
                Check(LegacyInstallDetector.HashFile(target)==original,"original restored after rollback");
                Check(File.ReadAllText(data)=="user data must remain","user data unchanged after rollback");
                string backup=NativeUpdateTransaction.ReplaceVerified(target,plan,()=>{},null);
                Check(NativeReleasePolicy.VerifyPayload(target,plan.Release),"successful atomic replacement");
                Check(LegacyInstallDetector.HashFile(backup)==original,"original backup preserved");
                Check(File.ReadAllText(data)=="user data must remain","user data unchanged after successful replace");
                Reject(()=>NativeUpdateTransaction.ReplaceVerified(target,plan,()=>{},null),"changed baseline cannot be replaced");
                Check(Directory.GetFiles(folder,"*.pending-*").Length==0,"no incomplete pending files");
            }
        }
        finally{Directory.Delete(folder,true);}
    }
}
