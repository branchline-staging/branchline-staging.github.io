using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Branchline.UpdateCenter
{
    // Verify the exact installed bytes against either the recovered legacy
    // inventory or a signed receipt from a completed native release.
    internal sealed class InstalledIdentity
    {
        internal readonly string Path,Sha,Label;
        internal readonly int Sequence;
        private InstalledIdentity(string path,string sha,string label,int sequence)
        {Path=path;Sha=sha;Label=label;Sequence=sequence;}

        internal static InstalledIdentity Discover(RSAParameters key,string channel,Uri directory)
        {
            return Resolve(LegacyInstallDetector.Scan(),key,channel,directory);
        }
        internal static InstalledIdentity Resolve(List<LegacyInstallDetector.Installation> found,
            RSAParameters key,string channel,Uri directory)
        {
            if(found==null || found.Count!=1)throw new InvalidOperationException("Expected one installation. No update made.");
            var current=found[0];
            string receipt=current.Path+".release-receipt.json";
            if(!File.Exists(receipt))
            {
                if(current.Label==null)throw new InvalidOperationException("Unrecognized installation. No update made.");
                // Reserve all legacy builds below the first centrally signed sequence.
                return new InstalledIdentity(current.Path,current.Sha256,current.Label,6);
            }
            if((File.GetAttributes(receipt)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Redirected release receipt.");
            byte[] envelope;
            using(var file=File.OpenRead(receipt))envelope=NativeReleaseClient.ReadBounded(file,32768);
            var release=VerifyReceipt(envelope,key,channel,directory,current.Sha256);
            return new InstalledIdentity(current.Path,current.Sha256,"Release "+release.Sequence,release.Sequence);
        }
        internal static byte[] MakeReceipt(NativeReleaseClient.Offer offer)
        {
            if(offer==null)throw new InvalidOperationException("Missing verified offer.");
            var fields=new Dictionary<string,object>{
                {"manifest",Convert.ToBase64String(offer.Manifest())},
                {"signature",Convert.ToBase64String(offer.Signature())}
            };
            return Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(fields));
        }
        internal static NativeReleasePolicy.Release VerifyReceipt(byte[] envelope,RSAParameters key,
            string channel,Uri directory,string installedSha)
        {
            if(envelope==null||envelope.Length==0||envelope.Length>32768)
                throw new InvalidOperationException("Invalid release receipt.");
            var parser=new JavaScriptSerializer{MaxJsonLength=32768,RecursionLimit=8};
            var fields=parser.DeserializeObject(new UTF8Encoding(false,true).GetString(envelope)) as Dictionary<string,object>;
            if(fields==null||fields.Count!=2||!fields.ContainsKey("manifest")||!fields.ContainsKey("signature"))
                throw new InvalidOperationException("Invalid receipt fields.");
            var raw=Convert.FromBase64String(fields["manifest"] as string ?? "");
            var sig=Convert.FromBase64String(fields["signature"] as string ?? "");
            if(raw.Length==0||raw.Length>16384||sig.Length<256||sig.Length>512)
                throw new InvalidOperationException("Invalid receipt bounds.");
            // A receipt is untrusted until NativeReleasePolicy verifies its signature.
            // Choose a base from the signed manifest only to reuse that policy;
            // the actual installed SHA must match the signed payload, not the base.
            var data=parser.DeserializeObject(new UTF8Encoding(false,true).GetString(raw)) as Dictionary<string,object>;
            object value;
            var bases=data!=null&&data.TryGetValue("supportedBaseSha256",out value)?value as object[]:null;
            if(bases==null||bases.Length==0)throw new InvalidOperationException("Missing receipt base.");
            var release=NativeReleasePolicy.Verify(raw,sig,key,channel,directory,6,bases[0] as string);
            if(!String.Equals(release.Sha256,installedSha,StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("Installed executable does not match signed receipt.");
            return release;
        }
        internal static void Persist(string target,byte[] envelope)
        {
            string path=target+".release-receipt.json";
            string temp=path+".pending-"+Guid.NewGuid().ToString("N");
            bool created=false;
            try
            {
                using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {created=true;f.Write(envelope,0,envelope.Length);f.Flush(true);}
                if(File.Exists(path))
                {
                    if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                        throw new IOException("Redirected release receipt.");
                    File.Replace(temp,path,path+".backup-"+Guid.NewGuid().ToString("N"),false);
                }
                else File.Move(temp,path);
            }
            finally{if(created&&File.Exists(temp))File.Delete(temp);}
        }
    }
}
