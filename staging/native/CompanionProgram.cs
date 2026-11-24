using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Branchline.UpdateCenter
{
    internal static class CompanionProgram
    {
        // The installed production workspace must never consume staging packages.
        private static readonly Uri DirectoryUri=new Uri("https://kzamora19.github.io/first-megasaver-ptg/native-releases/");
        private const string Channel="production";
        [STAThread]
        private static int Main(string[] args)
        {
            bool watch=Array.IndexOf(args,"--watch")>=0;
            try
            {
                int ownerPid=0;
                int pidIndex=Array.IndexOf(args,"--owner-pid");
                if(watch&&(pidIndex<0||pidIndex+1>=args.Length||!Int32.TryParse(args[pidIndex+1],out ownerPid)||ownerPid<=0))return 1;
                var key=LoadPublicKey();
                if(Array.IndexOf(args,"--self-test")>=0){return key.Modulus.Length>=256?0:1;}
                bool created;
                using(var instance=new Mutex(true,"Local\\BranchlineNativeUpdateCenter",out created))
                {
                    if(!created)return 0;
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using(var form=new CompanionForm(key,DirectoryUri,Channel,Array.IndexOf(args,"--launch")>=0,watch,ownerPid))
                        Application.Run(form);
                }
                return 0;
            }
            catch(Exception error)
            {
                if(watch)return 1;
                MessageBox.Show("Update Center could not start. Your existing workspace is unchanged.\n"+error.Message,
                    "Branchline Update Center",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                return 1;
            }
        }
        private static RSAParameters LoadPublicKey()
        {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Branchline.ReleasePublicKey.xml"))
            {
                if(stream==null)throw new InvalidOperationException("No trusted release signing identity.");
                using(var reader=new StreamReader(stream))
                using(var rsa=new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp=false;
                    rsa.FromXmlString(reader.ReadToEnd());
                    if(!rsa.PublicOnly||rsa.KeySize<2048||rsa.KeySize>4096)
                        throw new InvalidOperationException("Only a pinned RSA public key is permitted.");
                    return rsa.ExportParameters(false);
                }
            }
        }
    }

    internal sealed class CompanionForm:Form
    {
        private readonly RSAParameters key;
        private readonly Uri directory;
        private readonly string channel;
        private readonly bool launch,watch;
        private int ownerPid;
        private readonly System.Windows.Forms.Timer ownerTimer=new System.Windows.Forms.Timer();
        private readonly Label status=new Label();
        private readonly Button check=new Button(), install=new Button(), open=new Button(), later=new Button();
        private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
        private InstalledIdentity current;
        private NativeReleaseClient.Offer offer;
        private bool busy;
        private string dismissed;
        internal CompanionForm(RSAParameters key,Uri directory,string channel,bool launch,bool watch,int ownerPid)
        {
            this.key=key;this.directory=directory;this.channel=channel;this.launch=launch;this.watch=watch;this.ownerPid=ownerPid;
            if(watch){ShowInTaskbar=false;Opacity=0;WindowState=FormWindowState.Minimized;}
            Text="Branchline Update Center";StartPosition=FormStartPosition.CenterScreen;
            ClientSize=new Size(470,225);MinimumSize=new Size(430,260);
            Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(16,40,71);ForeColor=Color.White;
            var panel=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=3};
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            panel.Controls.Add(new Label{Text="Branchline Workspace • Software Updates",AutoSize=true,Font=new Font("Segoe UI",13,FontStyle.Bold)},0,0);
            status.Text="Checking your installation…";status.Dock=DockStyle.Fill;panel.Controls.Add(status,0,1);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
            check.Text="Check for updates";check.AutoSize=true;
            install.Text="Update & Restart";install.AutoSize=true;install.Enabled=false;
            open.Text="Open workspace";open.AutoSize=true;open.Enabled=false;
            later.Text="Later";later.AutoSize=true;
            foreach(var button in watch?new[]{install,later,check}:new[]{check,install,open}){button.ForeColor=Color.FromArgb(16,40,71);button.BackColor=Color.White;buttons.Controls.Add(button);}
            panel.Controls.Add(buttons,0,2);Controls.Add(panel);
            check.Click+=async (s,e)=>await Check(true);
            install.Click+=async (s,e)=>await Install();
            open.Click+=(s,e)=>OpenWorkspace();
            later.Click+=(s,e)=>{if(!busy&&watch)Hide();};
            timer.Interval=15*60*1000;timer.Tick+=async (s,e)=>await Check(false);
            Shown+=async (s,e)=>{
                if(watch){Hide();ownerTimer.Start();}
                else if(launch){OpenWorkspace();WindowState=FormWindowState.Minimized;}
                timer.Start();await Check(false);
            };
            ownerTimer.Interval=5000;ownerTimer.Tick+=(s,e)=>{if(watch&&!busy&&!Visible&&!OwnerRunning())Close();};
            NetworkChange.NetworkAvailabilityChanged+=OnNetwork;
            FormClosing+=(s,e)=>{
                if(busy){e.Cancel=true;return;}
                if(watch&&Visible&&OwnerRunning()&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
                timer.Stop();ownerTimer.Stop();
            };
        }

        private bool OwnerRunning()
        {
            try{using(var p=Process.GetProcessById(ownerPid))return !p.HasExited&&p.ProcessName=="Branchline Workspace";}
            catch{return false;}
        }
        private void OnNetwork(object sender,NetworkAvailabilityEventArgs e)
        {
            if(!e.IsAvailable||IsDisposed||!IsHandleCreated)return;
            try{BeginInvoke(new Action(async()=>await Check(false)));}catch(InvalidOperationException){}
        }
        private void OpenWorkspace()
        {
            try
            {
                current=InstalledIdentity.Discover(key,channel,directory);
                if(Process.GetProcessesByName("Branchline Workspace").Length!=0)return;
                using(var process=Process.Start(new ProcessStartInfo(current.Path){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(current.Path)}))
                    if(process!=null)ownerPid=process.Id;
                open.Enabled=true;
            }
            catch(Exception error){status.Text="Workspace was not launched. "+error.Message;WindowState=FormWindowState.Normal;}
        }

        private void SetBusy(bool value)
        {
            busy=value;check.Enabled=!value;later.Enabled=!value;open.Enabled=!value&&current!=null;
            install.Enabled=!value&&offer!=null;
        }
        private async Task Check(bool manual)
        {
            if(busy)return;
            offer=null;current=null;SetBusy(true);status.Text="Checking for a signed compatible update…";
            try
            {
                current=await Task.Run(()=>InstalledIdentity.Discover(key,channel,directory));
                var snapshot=current;
                offer=await Task.Run(()=>NativeReleaseClient.Check(key,channel,directory,snapshot.Sequence,snapshot.Sha));
                status.Text=snapshot.Label+" • Release "+offer.Release.Sequence+" available. Save all work and close Branchline before installing.";
                if(manual||dismissed!=offer.Release.Sha256)
                {
                    dismissed=offer.Release.Sha256;
                    Opacity=1;WindowState=FormWindowState.Normal;Show();Activate();
                }
            }
            catch(Exception)
            {
                status.Text=current==null?"Installation not recognized. No changes made.":
                    current.Label+" • No verified compatible update available, or connection unavailable.";
            }
            finally{SetBusy(false);}
        }

        private async Task Install()
        {
            if(busy||current==null||offer==null)return;
            if(MessageBox.Show("Save all work and close Branchline Workspace completely. Install the verified update now?",
                Text,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            if(Process.GetProcessesByName("Branchline Workspace").Length!=0)
            {status.Text="Branchline is still open. Save and close it, then try again.";return;}
            var snapshot=current;var selected=offer;SetBusy(true);
            try
            {
                status.Text="Downloading and verifying the signed update…";
                var plan=await Task.Run(()=>NativeReleaseClient.FetchPackage(selected,key,channel,directory,snapshot.Sequence,snapshot.Sha));
                status.Text="Installing verified update and preserving original executable…";
                var backup=await Task.Run(()=>NativeUpdateTransaction.ApplyCompanionUpdate(plan,selected,key,channel,directory,true));
                offer=null;current=InstalledIdentity.Discover(key,channel,directory);
                status.Text="Installed "+current.Label+". The original executable has been backed up.";
                MessageBox.Show("Update installed. Branchline will reopen.\nBackup: "+backup,Text);
                OpenWorkspace();
                if(watch&&OwnerRunning())Hide();
            }
            catch(Exception error){offer=null;status.Text="Update could not be completed. "+error.Message;}
            finally{SetBusy(false);}
        }
        protected override void Dispose(bool disposing){if(disposing){NetworkChange.NetworkAvailabilityChanged-=OnNetwork;timer.Dispose();ownerTimer.Dispose();}base.Dispose(disposing);}
    }
}
