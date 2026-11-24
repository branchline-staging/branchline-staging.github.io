using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Branchline.UpdateCenter
{
    // Pure validation: no download, process launch, file modification or permission changes.
    // The integrated launcher must obtain trustedKey from its own pinned build resources,
    // never from a network response, manifest, configuration writable by users, or web content.
    public static class NativeReleasePolicy
    {
        private static readonly Regex Hex=new Regex("^[a-fA-F0-9]{64}$",RegexOptions.CultureInvariant);
        private static readonly Regex FileName=new Regex("^Branchline-Workspace-[0-9]+[.]exe$",RegexOptions.CultureInvariant);
        public sealed class Release
        {
            public readonly int Sequence;
            public readonly string Sha256;
            public readonly Uri Payload;
            internal Release(int sequence,string sha,Uri payload){Sequence=sequence;Sha256=sha;Payload=payload;}
        }
        public static Release Verify(byte[] manifest,byte[] signature,RSAParameters trustedKey,
            string channel,Uri trustedDirectory,int installedSequence,string installedSha256)
        {
            if(manifest==null || manifest.Length==0 || manifest.Length>16384 ||
                signature==null || trustedKey.Modulus==null || trustedKey.Modulus.Length<256 ||
                trustedKey.Modulus.Length>512 || signature.Length!=trustedKey.Modulus.Length ||
                !Hex.IsMatch(installedSha256??"") || installedSequence<0 ||
                (channel!="staging"&&channel!="production") ||
                trustedDirectory==null || !trustedDirectory.IsAbsoluteUri ||
                trustedDirectory.Scheme!="https" || !trustedDirectory.IsDefaultPort ||
                trustedDirectory.UserInfo!="" || trustedDirectory.Query!="" ||
                trustedDirectory.Fragment!="" || !trustedDirectory.AbsolutePath.EndsWith("/"))
                throw new InvalidOperationException("Invalid release verification input.");
            using(var rsa=new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp=false;
                rsa.ImportParameters(trustedKey);
                if(!rsa.VerifyData(manifest,CryptoConfig.MapNameToOID("SHA256"),signature))
                    throw new CryptographicException("Release signature is invalid.");
            }
            var serializer=new JavaScriptSerializer{MaxJsonLength=16384,RecursionLimit=8};
            var data=serializer.DeserializeObject(new UTF8Encoding(false,true).GetString(manifest)) as Dictionary<string,object>;
            if(data==null || data.Count!=7 || !data.ContainsKey("schema") || !data.ContainsKey("channel") ||
                !data.ContainsKey("platform") || !data.ContainsKey("sequence") || !data.ContainsKey("payloadSha256") ||
                !data.ContainsKey("payloadUrl") || !data.ContainsKey("supportedBaseSha256"))
                throw new InvalidOperationException("Unrecognized release schema.");
            if(!(data["schema"] is int) || (int)data["schema"]!=1 ||
                !String.Equals(data["channel"] as string,channel,StringComparison.Ordinal) ||
                !String.Equals(data["platform"] as string,"windows-x64",StringComparison.Ordinal) ||
                !(data["sequence"] is int) || (int)data["sequence"]<=installedSequence)
                throw new InvalidOperationException("Wrong platform, channel, or non-increasing release.");
            string sha=data["payloadSha256"] as string;
            Uri payload;
            string url=data["payloadUrl"] as string;
            if(!Hex.IsMatch(sha??"") || String.Equals(sha,installedSha256,StringComparison.OrdinalIgnoreCase) ||
                url==null || !Uri.TryCreate(url,UriKind.Absolute,out payload) || url!=payload.AbsoluteUri ||
                payload.Scheme!="https" || !payload.IsDefaultPort || payload.UserInfo!="" ||
                payload.Query!="" || payload.Fragment!="" ||
                !String.Equals(payload.Host,trustedDirectory.Host,StringComparison.OrdinalIgnoreCase) ||
                !payload.AbsolutePath.StartsWith(trustedDirectory.AbsolutePath,StringComparison.Ordinal))
                throw new InvalidOperationException("Untrusted release payload.");
            string file=payload.AbsolutePath.Substring(trustedDirectory.AbsolutePath.Length);
            if(!FileName.IsMatch(file) || file!="Branchline-Workspace-"+((int)data["sequence"]).ToString(System.Globalization.CultureInfo.InvariantCulture)+".exe")
                throw new InvalidOperationException("Unexpected release path.");
            var bases=data["supportedBaseSha256"] as object[];
            if(bases==null || bases.Length==0 || bases.Length>64)
                throw new InvalidOperationException("No compatible installation hashes.");
            bool compatible=false;
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(object item in bases)
            {
                string hash=item as string;
                if(!Hex.IsMatch(hash??"") || !seen.Add(hash))
                    throw new InvalidOperationException("Malformed compatibility list.");
                compatible |= String.Equals(hash,installedSha256,StringComparison.OrdinalIgnoreCase);
            }
            if(!compatible)throw new InvalidOperationException("Installed launcher is not listed for this update.");
            return new Release((int)data["sequence"],sha.ToUpperInvariant(),payload);
        }
        // Used after the download and immediately before any future installer action.
        public static bool VerifyPayload(string localPath,Release release)
        {
            return release!=null && String.Equals(LegacyInstallDetector.HashFile(localPath),
                release.Sha256,StringComparison.OrdinalIgnoreCase);
        }
    }
}
