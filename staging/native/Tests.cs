using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using Branchline.UpdateCenter;

internal static class Tests
{
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static void Reject(Action action,string name)
    {
        bool rejected=false;
        try{action();}catch(InvalidOperationException){rejected=true;}catch(CryptographicException){rejected=true;}
        Check(rejected,name);
    }
    static int Main()
    {
        string r5="BE2286A36BFD7CCB01A30BCCF0DC818BA6E50F912BEA6519BF8822979950C526";
        string r6="CA73CCA221812B80216668AB7B0FB9EB42EB6A994DF7BE2E551D2AEF6654A139";
        string follow="B6971C8816F181E428ACCE11D30E412F69F57F7C2FFA1BD411348C033220A187";
        Check(LegacyInstallDetector.Identify(r5)=="Navigation R5","R5 known");
        Check(LegacyInstallDetector.Identify(r6)=="Navigation R6 candidate","R6 known");
        Check(LegacyInstallDetector.Identify(follow)=="R6 follow-up candidate","R6 follow-up known");
        Check(LegacyInstallDetector.Identify(new string('0',64))==null,"unknown rejected");
        string folder=Path.Combine(Path.GetTempPath(),"branchline-update-test-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            Check(LegacyInstallDetector.Scan(folder).Count==0,"empty install not recognized");
            string path=Path.Combine(folder,"Branchline Workspace.exe");
            File.WriteAllText(path,"fixture, not an executable");
            string actual=LegacyInstallDetector.HashFile(path);
            var scanned=LegacyInstallDetector.Scan(folder);
            Check(scanned.Count==1 && scanned[0].Sha256==actual && !LegacyInstallDetector.IsUniqueRecognized(scanned),"unknown on disk remains untouched");
            Check(LegacyInstallDetector.IsUniqueRecognized(new List<LegacyInstallDetector.Installation>{new LegacyInstallDetector.Installation(path,r5)}),"single known installation");
            Check(!LegacyInstallDetector.IsUniqueRecognized(new List<LegacyInstallDetector.Installation>{new LegacyInstallDetector.Installation(path,r5),new LegacyInstallDetector.Installation(path,r6)}),"multiple installations rejected");
            using(var signing=new RSACryptoServiceProvider(2048))
            using(var wrong=new RSACryptoServiceProvider(2048))
            {
                signing.PersistKeyInCsp=false;wrong.PersistKeyInCsp=false;
                var trusted=signing.ExportParameters(false);
                var directory=new Uri("https://branchline-staging.github.io/native-releases/");
                var manifest=new Dictionary<string,object>{
                    {"schema",1},{"channel","staging"},{"platform","windows-x64"},{"sequence",7},
                    {"payloadSha256",actual},{"payloadUrl","https://branchline-staging.github.io/native-releases/Branchline-Workspace-7.exe"},
                    {"supportedBaseSha256",new string[]{r5,r6}}
                };
                var serializer=new JavaScriptSerializer();
                Func<byte[]> bytes=()=>Encoding.UTF8.GetBytes(serializer.Serialize(manifest));
                Func<byte[],byte[]> sign=b=>signing.SignData(b,CryptoConfig.MapNameToOID("SHA256"));
                Func<NativeReleasePolicy.Release> verify=()=>{var b=bytes();return NativeReleasePolicy.Verify(b,sign(b),trusted,"staging",directory,6,r6);};
                var release=verify();
                Check(release.Sequence==7 && NativeReleasePolicy.VerifyPayload(path,release),"valid signature and payload hash");
                File.AppendAllText(path,"tampered");
                Check(!NativeReleasePolicy.VerifyPayload(path,release),"modified payload rejected");
                var raw=bytes();
                Reject(()=>NativeReleasePolicy.Verify(raw,new byte[256],trusted,"staging",directory,6,r6),"unsigned rejected");
                Reject(()=>NativeReleasePolicy.Verify(raw,sign(raw),wrong.ExportParameters(false),"staging",directory,6,r6),"wrong signing key rejected");
                var tampered=(byte[])raw.Clone();tampered[0]^=1;
                Reject(()=>NativeReleasePolicy.Verify(tampered,sign(raw),trusted,"staging",directory,6,r6),"tampered manifest rejected");
                Reject(()=>NativeReleasePolicy.Verify(raw,sign(raw),trusted,"staging",directory,7,r6),"downgrade or repeat rejected");
                Reject(()=>NativeReleasePolicy.Verify(raw,sign(raw),trusted,"staging",directory,6,follow),"unsupported build rejected");
                manifest["channel"]="production";Reject(()=>verify(),"wrong channel rejected");manifest["channel"]="staging";
                manifest["platform"]="linux";Reject(()=>verify(),"wrong platform rejected");manifest["platform"]="windows-x64";
                manifest["payloadUrl"]="https://example.com/native-releases/Branchline-Workspace-7.exe";Reject(()=>verify(),"foreign host rejected");
                manifest["payloadUrl"]="https://branchline-staging.github.io/native-releases/../other/Branchline-Workspace-7.exe";Reject(()=>verify(),"path traversal rejected");
            }
        }
        finally{Directory.Delete(folder,true);}
        TransactionTests.Run();
        ReceiptTests.Run();
        return 0;
    }
}
