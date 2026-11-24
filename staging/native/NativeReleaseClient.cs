using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Security.Cryptography;

namespace Branchline.UpdateCenter
{
    // Native-only network client. The future launcher supplies compiled-in trust
    // parameters; never accept these from a WebView message or downloaded config.
    internal static class NativeReleaseClient
    {
        internal const int MaxPackageBytes = 64 * 1024 * 1024;

        internal static byte[] ReadBounded(Stream input, int limit)
        {
            if(input==null || limit<1 || limit>MaxPackageBytes)
                throw new InvalidOperationException("Invalid response size limit.");
            using(var result=new MemoryStream())
            {
                var buffer=new byte[32768];
                int count;
                while((count=input.Read(buffer,0,buffer.Length))!=0)
                {
                    if(result.Length+count>limit)
                        throw new IOException("Update response exceeds its size limit.");
                    result.Write(buffer,0,count);
                }
                return result.ToArray();
            }
        }

        private static void RequireHttps(Uri url)
        {
            if(url==null || !url.IsAbsoluteUri || url.Scheme!="https" ||
                !url.IsDefaultPort || url.UserInfo!="" || url.Query!="" || url.Fragment!="")
                throw new InvalidOperationException("Untrusted update endpoint.");
        }

        private static byte[] Download(Uri url,int limit)
        {
            RequireHttps(url);
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.Method="GET";
            request.AllowAutoRedirect=false;
            request.AutomaticDecompression=DecompressionMethods.None;
            request.UseDefaultCredentials=false;
            request.Credentials=null;
            request.PreAuthenticate=false;
            request.Timeout=15000;
            request.ReadWriteTimeout=15000;
            request.CachePolicy=new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
            // Use normal operating-system HTTPS certificate validation. No callback
            // can be installed here to bypass certificate errors.
            using(var response=(HttpWebResponse)request.GetResponse())
            {
                if(response.StatusCode!=HttpStatusCode.OK ||
                    response.ResponseUri.AbsoluteUri!=url.AbsoluteUri ||
                    response.ContentLength>limit)
                    throw new IOException("Unexpected update response.");
                using(var stream=response.GetResponseStream())
                {
                    var bytes=ReadBounded(stream,limit);
                    if(response.ContentLength>=0 && bytes.LongLength!=response.ContentLength)
                        throw new IOException("Incomplete update response.");
                    return bytes;
                }
            }
        }

        internal sealed class Offer
        {
            internal readonly NativeReleasePolicy.Release Release;
            internal readonly string ExpectedBase;
            private readonly byte[] manifest,signature;
            internal Offer(NativeReleasePolicy.Release release,string expectedBase,byte[] raw,byte[] sig)
            {
                Release=release;ExpectedBase=expectedBase;
                manifest=(byte[])raw.Clone();signature=(byte[])sig.Clone();
            }
            internal byte[] Manifest(){return (byte[])manifest.Clone();}
            internal byte[] Signature(){return (byte[])signature.Clone();}
        }

        internal static Offer Check(RSAParameters pinnedKey,string channel,Uri directory,
            int installedSequence,string installedSha)
        {
            return Check(pinnedKey,channel,directory,installedSequence,installedSha,Download);
        }

        // Dependency injection is internal for fixtures only, not a web-facing API.
        internal static Offer Check(RSAParameters pinnedKey,string channel,Uri directory,
            int installedSequence,string installedSha,Func<Uri,int,byte[]> fetch)
        {
            RequireHttps(directory);
            if(!directory.AbsolutePath.EndsWith("/") || fetch==null)
                throw new InvalidOperationException("Invalid release directory.");
            var manifest=fetch(new Uri(directory,"release.json"),16384);
            var signature=fetch(new Uri(directory,"release.sig"),512);
            var release=NativeReleasePolicy.Verify(manifest,signature,pinnedKey,channel,directory,
                installedSequence,installedSha);
            return new Offer(release,installedSha,manifest,signature);
        }

        internal static SignedUpdatePlan FetchPackage(Offer offer,RSAParameters pinnedKey,string channel,
            Uri directory,int installedSequence,string installedSha)
        {
            return FetchPackage(offer,pinnedKey,channel,directory,installedSequence,installedSha,Download);
        }

        internal static SignedUpdatePlan FetchPackage(Offer offer,RSAParameters pinnedKey,string channel,
            Uri directory,int installedSequence,string installedSha,Func<Uri,int,byte[]> fetch)
        {
            if(offer==null || !String.Equals(offer.ExpectedBase,installedSha,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The installed revision changed.");
            // Re-verify before contacting the payload URL. Offers are not a trust root.
            var raw=offer.Manifest();var sig=offer.Signature();
            var release=NativeReleasePolicy.Verify(raw,sig,pinnedKey,channel,directory,
                installedSequence,installedSha);
            var bytes=fetch(release.Payload,MaxPackageBytes);
            return SignedUpdatePlan.Create(raw,sig,pinnedKey,channel,directory,
                installedSequence,installedSha,bytes);
        }
    }

    internal sealed class SignedUpdatePlan
    {
        internal readonly NativeReleasePolicy.Release Release;
        internal readonly string ExpectedBase;
        private readonly byte[] payload;
        private SignedUpdatePlan(NativeReleasePolicy.Release release,string expectedBase,byte[] bytes)
        {
            Release=release;ExpectedBase=expectedBase;payload=(byte[])bytes.Clone();
        }
        internal static SignedUpdatePlan Create(byte[] manifest,byte[] signature,RSAParameters pinnedKey,
            string channel,Uri directory,int installedSequence,string installedSha,byte[] payload)
        {
            var release=NativeReleasePolicy.Verify(manifest,signature,pinnedKey,channel,directory,
                installedSequence,installedSha);
            if(payload==null || payload.Length==0 || payload.Length>NativeReleaseClient.MaxPackageBytes ||
                !String.Equals(Hash(payload),release.Sha256,StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("Update payload checksum mismatch.");
            return new SignedUpdatePlan(release,installedSha,payload);
        }
        internal byte[] Package(){return (byte[])payload.Clone();}
        private static string Hash(byte[] bytes)
        {
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");
        }
    }
}
