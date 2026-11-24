using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace Branchline.UpdateCenter
{
    // One-time installer, not a second application. All code and release bytes
    // are embedded; the existing verified transaction does the replacement.
    internal static class BootstrapProgram
    {
        private const string Channel="production";
        private static readonly Uri DirectoryUri=new Uri("https://kzamora19.github.io/first-megasaver-ptg/native-releases/");
        private const string Pilot="7E8D7CA1B87954D14717BC2980B24F552AEDEAD5E96B0CC91717B2BA1B4CB25E";
        private static byte[] Resource(string name,int maximum)
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if(stream==null)throw new InvalidOperationException("Missing installer resource.");
                return NativeReleaseClient.ReadBounded(stream,maximum);
            }
        }
        private static RSAParameters Key()
        {
            using(var rsa=new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp=false;
                rsa.FromXmlString(new System.Text.UTF8Encoding(false,true).GetString(Resource("Branchline.ReleasePublicKey.xml",16384)));
                if(!rsa.PublicOnly||rsa.KeySize<2048||rsa.KeySize>4096)throw new CryptographicException("Invalid pinned public key.");
                return rsa.ExportParameters(false);
            }
        }
        [STAThread]
        private static int Main(string[] args)
        {
            bool selfTest=Array.IndexOf(args,"--self-test")>=0;
            try
            {
                var key=Key();
                var manifest=Resource("Branchline.ReleaseManifest.json",16384);
                var sig=Resource("Branchline.ReleaseSignature.bin",512);
                var payload=Resource("Branchline.LauncherPayload.exe",NativeReleaseClient.MaxPackageBytes);
                if(selfTest)
                {
                    // CI supplies a fixture manifest and dummy payload; never
                    // inspect or modify a real installation in this mode.
                    SignedUpdatePlan.Create(manifest,sig,key,Channel,DirectoryUri,6,Pilot,payload);
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var current=InstalledIdentity.Discover(key,Channel,DirectoryUri);
                if(current.Sequence>=7)throw new InvalidOperationException("This installation already has signed update support. Use its normal update mechanism.");
                var plan=SignedUpdatePlan.Create(manifest,sig,key,Channel,DirectoryUri,current.Sequence,current.Sha,payload);
                var offer=new NativeReleaseClient.Offer(plan.Release,current.Sha,manifest,sig);
                if(MessageBox.Show("Upgrade the existing Branchline Workspace ("+current.Label+") to signed release "+plan.Release.Sequence+"?\n\nSave your work and close Branchline first. This replaces only the original executable, retains its backup, account data and existing shortcut, and enables future online updates.",
                    "Branchline Workspace Upgrade",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
                var backup=NativeUpdateTransaction.ApplyCompanionUpdate(plan,offer,key,Channel,DirectoryUri,true);
                var installed=InstalledIdentity.Discover(key,Channel,DirectoryUri);
                MessageBox.Show("The existing Branchline Workspace was upgraded.\nBackup: "+backup+"\nSelect OK to reopen Branchline.",
                    "Branchline Workspace Upgrade",MessageBoxButtons.OK,MessageBoxIcon.Information);
                using(var process=Process.Start(new ProcessStartInfo(installed.Path){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(installed.Path)})){}
                return 0;
            }
            catch(Exception error)
            {
                if(!selfTest)MessageBox.Show("No upgrade completed. Keep the original installation and backups intact.\n"+error.Message,
                    "Branchline Workspace Upgrade",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return 1;
            }
        }
    }
}
