using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

// Copied as a private method into the exact R6 managed entry point. Never
// execute network-supplied code or an unverified neighboring executable.
public static class IntegratedHook
{
    public static void Start(string[] args)
    {
        string temporary=null;
        try
        {
            if(args!=null&&(Array.IndexOf(args,"--verify")>=0||Array.IndexOf(args,"--verify-shell")>=0))return;
            const string expected="BRANCHLINE_EMBEDDED_COMPANION_SHA256_PLACEHOLDER";
            var assembly=Assembly.GetExecutingAssembly();
            string directory=Path.GetDirectoryName(assembly.Location);
            for(var d=new DirectoryInfo(directory);d!=null;d=d.Parent)
                if((d.Attributes&FileAttributes.ReparsePoint)!=0)return;
            string target=Path.Combine(directory,"Branchline.UpdateCenter."+expected.Substring(0,16)+".exe");
            if(!File.Exists(target))
            {
                using(var resource=assembly.GetManifestResourceStream("Branchline.IntegratedUpdater.exe"))
                {
                    if(resource==null||resource.Length<1||resource.Length>8*1024*1024)return;
                    temporary=target+".pending-"+Guid.NewGuid().ToString("N");
                    using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    {resource.CopyTo(output);output.Flush(true);}
                }
                using(var stream=File.OpenRead(temporary))
                using(var sha=SHA256.Create())
                    if(!String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),expected,StringComparison.Ordinal))return;
                try{File.Move(temporary,target);}catch(IOException){if(!File.Exists(target))return;}
            }
            if((File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)return;
            using(var stream=File.OpenRead(target))
            using(var sha=SHA256.Create())
            {
                if(!String.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",""),expected,StringComparison.Ordinal))return;
                using(var self=Process.GetCurrentProcess())
                using(var child=Process.Start(new ProcessStartInfo(target,"--watch --owner-pid "+self.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))
                {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory})){}
            }
        }
        catch(Exception){/* Optional update component must never prevent workspace startup. */}
        finally{try{if(temporary!=null&&File.Exists(temporary))File.Delete(temporary);}catch(Exception){}}
    }
}
