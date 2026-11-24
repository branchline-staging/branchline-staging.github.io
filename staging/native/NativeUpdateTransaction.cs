using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Branchline.UpdateCenter
{
    // Transactional replacement core. Not wired into the running launcher yet.
    // A future separate helper must obtain explicit user consent, receive the
    // signed package from its pinned channel, and wait for the launcher to close.
    internal static class NativeUpdateTransaction
    {
        internal static string ApplyClosedInstallation(SignedUpdatePlan plan,bool userConfirmed)
        {
            if(!userConfirmed || plan==null)
                throw new InvalidOperationException("No approved native update.");
            RequireClosed();
            var found=LegacyInstallDetector.Scan();
            if(!LegacyInstallDetector.IsUniqueRecognized(found) ||
                !String.Equals(found[0].Sha256,plan.ExpectedBase,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unknown or ambiguous installation. No update made.");
            return ReplaceVerified(found[0].Path,plan,RequireClosed,null);
        }

        private static void RequireClosed()
        {
            var processes=Process.GetProcessesByName("Branchline Workspace");
            try
            {
                if(processes.Length!=0)
                    throw new InvalidOperationException("Save your work and close Branchline Workspace before installing.");
            }
            finally{foreach(var process in processes)process.Dispose();}
        }

        private static void RequirePlainPath(string target)
        {
            if(!Path.IsPathRooted(target) || Path.GetFileName(target)!="Branchline Workspace.exe" ||
                !File.Exists(target) || (File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Unrecognized target executable.");
            for(var dir=new DirectoryInfo(Path.GetDirectoryName(target));dir!=null;dir=dir.Parent)
                if((dir.Attributes&FileAttributes.ReparsePoint)!=0)
                    throw new IOException("Redirected installation path.");
        }

        // Internal test seam for an injected post-replacement failure. The future
        // production helper uses only ApplyClosedInstallation, with null hook.
        internal static string ReplaceVerified(string target,SignedUpdatePlan plan,Action requireClosed,Action postReplaceTest)
        {
            if(plan==null||requireClosed==null)throw new InvalidOperationException("Missing verified update.");
            RequirePlainPath(target);
            requireClosed();
            if(!String.Equals(LegacyInstallDetector.HashFile(target),plan.ExpectedBase,StringComparison.OrdinalIgnoreCase))
                throw new IOException("Installed executable changed. No update made.");
            string suffix=Guid.NewGuid().ToString("N");
            string staged=target+".pending-"+suffix;
            string backup=target+".backup-"+suffix;
            string failed=target+".failed-"+suffix;
            bool replaced=false, createdStaged=false;
            try
            {
                var bytes=plan.Package();
                using(var file=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    createdStaged=true;
                    file.Write(bytes,0,bytes.Length);file.Flush(true);
                }
                if(!NativeReleasePolicy.VerifyPayload(staged,plan.Release))
                    throw new IOException("Staged package checksum failed.");
                requireClosed();
                RequirePlainPath(target);
                if(!String.Equals(LegacyInstallDetector.HashFile(target),plan.ExpectedBase,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Installed executable changed. No update made.");
                // Same-volume replacement, retaining the original as a backup.
                // If the filesystem cannot support this, do not fall back to deletion.
                File.Replace(staged,target,backup,false);
                replaced=true;
                if(postReplaceTest!=null)postReplaceTest();
                if(!NativeReleasePolicy.VerifyPayload(target,plan.Release) ||
                    !String.Equals(LegacyInstallDetector.HashFile(backup),plan.ExpectedBase,StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Post-install verification failed.");
                return backup;
            }
            catch(Exception original)
            {
                if(replaced)
                {
                    try
                    {
                        RequirePlainPath(target);
                        if(!File.Exists(backup) || (File.GetAttributes(backup)&FileAttributes.ReparsePoint)!=0 ||
                            !String.Equals(LegacyInstallDetector.HashFile(backup),plan.ExpectedBase,StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Original backup could not be verified.");
                        File.Replace(backup,target,failed,false);
                        if(!String.Equals(LegacyInstallDetector.HashFile(target),plan.ExpectedBase,StringComparison.OrdinalIgnoreCase))
                            throw new IOException("Restored executable checksum failed.");
                    }
                    catch(Exception rollback)
                    {
                        // Retain every recoverable file; never erase the backup.
                        throw new AggregateException("Update failed and rollback requires support. Keep installation files intact.",original,rollback);
                    }
                }
                throw;
            }
            finally
            {
                if(createdStaged && File.Exists(staged))File.Delete(staged);
            }
        }
    }
}
