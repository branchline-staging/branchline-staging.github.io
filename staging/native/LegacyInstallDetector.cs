using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Branchline.UpdateCenter
{
    // Read-only compatibility inventory. Recognition does NOT authorize installation.
    public static class LegacyInstallDetector
    {
        private static readonly Dictionary<string,string> Known = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            {"DF991BBEDFFB31D59220CDD859772C09EE4C2ADF6A366597EE8E2786F6AA6E3F", "Original November 23 launcher"},
            {"5F4F99CA151CB89FBA490AC252D9F85E02899481545E3766E600EA6C4DEFDD76", "Navigation R1"},
            {"8CE164CAE547F8BDA5C536C9E16766A95542D3C5D7F135FB6E2D13DFEDCB84E6", "Navigation R2"},
            {"4C32802F4492692C189176B1D7F7DD05115478A6029BFE755DFB30D733504BD0", "Legacy build listed in R5 updater (label unverified)"},
            {"51AFF6C13B4901F8F249BD741C4AC84E73D82401952F6DBC03D10C2A7EE1758D", "Navigation R4"},
            {"BE2286A36BFD7CCB01A30BCCF0DC818BA6E50F912BEA6519BF8822979950C526", "Navigation R5"},
            {"A188F3CFE43773A183DB318FABA09AF81D2BBE54A6BE9FC49035E8204D786F71", "R5 border hotfix"},
            {"CA73CCA221812B80216668AB7B0FB9EB42EB6A994DF7BE2E551D2AEF6654A139", "Navigation R6 candidate"},
            {"B6971C8816F181E428ACCE11D30E412F69F57F7C2FFA1BD411348C033220A187", "R6 follow-up candidate"},
            {"7E8D7CA1B87954D14717BC2980B24F552AEDEAD5E96B0CC91717B2BA1B4CB25E", "R6 role correction pilot (not production approved)"}
        };
        public static string Identify(string sha256)
        {
            string name;
            return sha256 != null && Known.TryGetValue(sha256,out name) ? name : null;
        }
        public static string HashFile(string path)
        {
            using(var file=File.OpenRead(path))
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(file)).Replace("-","");
        }
        public sealed class Installation
        {
            public readonly string Path, Sha256, Label;
            public Installation(string path,string hash)
            { Path=path;Sha256=hash;Label=Identify(hash); }
        }
        public static List<Installation> Scan()
        {
            return Scan(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs","Branchline Workspace"));
        }
        // Exposed for tests; the native launcher must use the fixed local-install root.
        public static List<Installation> Scan(string root)
        {
            var found=new List<Installation>();
            if(!Directory.Exists(root))return found;
            if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Installation root is a reparse point.");
            var directories=new List<string>();directories.Add(root);
            directories.AddRange(Directory.GetDirectories(root));
            foreach(string directory in directories)
            {
                if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Ambiguous installation directory.");
                string candidate=Path.Combine(directory,"Branchline Workspace.exe");
                if(!File.Exists(candidate))continue;
                if((File.GetAttributes(candidate)&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Installation executable is a reparse point.");
                found.Add(new Installation(candidate,HashFile(candidate)));
            }
            return found;
        }
        public static bool IsUniqueRecognized(List<Installation> found)
        {
            return found!=null && found.Count==1 && found[0].Label!=null;
        }
    }
}
