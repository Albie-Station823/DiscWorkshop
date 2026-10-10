using System;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class PreviewPlayback : IDisposable {
    readonly string executable;readonly Control surface;
    Process player;IntPtr window;bool paused;int generation;double start,clock;
    int volumeSteps;public int VolumeSteps {get{return volumeSteps;}}
    public string ErrorText="";
    public bool Active {get {try{return player!=null&&!player.HasExited;}catch(InvalidOperationException){return false;}}}
    public bool Paused {get{return paused;}}
    public double Position {get {return start+System.Threading.Interlocked.CompareExchange(ref clock,0,0);}}
    public PreviewPlayback(string ffmpeg,Control host) {
        executable=Engine.Find("ffplay.exe",Path.Combine(Path.GetDirectoryName(ffmpeg),"ffplay.exe"));surface=host;
    }
    public void Start(string source,double localStart,double globalStart) {
        Stop();if(!File.Exists(executable))throw new Exception("Playback needs ffplay.exe beside ffmpeg.exe (or in tools / PATH).");
        start=globalStart;clock=0;paused=false;ErrorText="";int version=++generation;
        var psi=new ProcessStartInfo(executable,Engine.Args("-hide_banner","-loglevel","info","-stats","-autoexit","-window_title","Disc Workshop preview","-x",Math.Max(100,surface.Width).ToString(),"-y",Math.Max(100,surface.Height).ToString(),"-ss",Engine.Number(localStart),"-vf","setpts=PTS-STARTPTS","-af","asetpts=PTS-STARTPTS","-i",source)) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        player=new Process {StartInfo=psi};
        player.ErrorDataReceived+=(s,e)=> {
            if(version!=generation||e.Data==null)return;
            var m=Regex.Match(e.Data,@"^\s*(-?\d+(?:\.\d+)?)\s+(?:A-V|M-V|M-A):");double seconds;
            if(m.Success&&double.TryParse(m.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out seconds))System.Threading.Interlocked.Exchange(ref clock,Math.Max(0,seconds));
            else if(e.Data.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0||e.Data.Contains("Failed"))ErrorText=e.Data;
        };
        player.Start();player.BeginErrorReadLine();
    }
    public void Attach() {
        if(!Active)return;
        if(window==IntPtr.Zero) {
            player.Refresh();window=player.MainWindowHandle;if(window==IntPtr.Zero)return;
            long style=GetStyle(window).ToInt64();style=(style & ~0x80C40000L)|0x48000000L;
            SetParent(window,surface.Handle);SetStyle(window,new IntPtr(style));
            if(paused)SendSpace();
            for(int i=0;i<Math.Abs(volumeSteps);i++)SendVolume(volumeSteps>0);
        }
        MoveWindow(window,0,0,Math.Max(1,surface.Width),Math.Max(1,surface.Height),true);
    }
    public void Toggle(){if(!Active)return;paused=!paused;if(window!=IntPtr.Zero)SendSpace();}
    public void Pause(){if(!paused)Toggle();}
    public void ChangeVolume(int direction){int next=Math.Max(-50,Math.Min(50,volumeSteps+Math.Sign(direction)));if(next==volumeSteps)return;volumeSteps=next;if(window!=IntPtr.Zero&&Active)SendVolume(direction>0);}
    void SendVolume(bool up){SendKey(up?0x30:0x39,up?0x0B:0x0A);}
    void SendKey(int key,int scan){PostMessage(window,0x100,new IntPtr(key),new IntPtr((scan<<16)|1));PostMessage(window,0x101,new IntPtr(key),new IntPtr(unchecked((int)0xC0000001)|(scan<<16)));}
    void SendSpace(){PostMessage(window,0x100,new IntPtr(0x20),new IntPtr(0x00390001));PostMessage(window,0x101,new IntPtr(0x20),new IntPtr(unchecked((int)0xC0390001)));}
    public void Stop() {
        generation++;window=IntPtr.Zero;
        if(player!=null){try{if(!player.HasExited){player.Kill();player.WaitForExit(1000);}}catch(InvalidOperationException){}finally{player.Dispose();player=null;}}
    }
    public void Dispose(){Stop();}
    static IntPtr GetStyle(IntPtr h){return IntPtr.Size==8?GetWindowLongPtr(h,-16):new IntPtr(GetWindowLong(h,-16));}
    static void SetStyle(IntPtr h,IntPtr value){if(IntPtr.Size==8)SetWindowLongPtr(h,-16,value);else SetWindowLong(h,-16,value.ToInt32());}
    [DllImport("user32.dll")]static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll")]static extern bool MoveWindow(IntPtr h,int x,int y,int width,int height,bool repaint);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetWindowLong(IntPtr h,int index,int value);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
}

sealed class MovieTimeInput : FlowLayoutPanel {
    NumericUpDown hours=new NumericUpDown(),minutes=new NumericUpDown(),seconds=new NumericUpDown();bool updating;double exactValue;
    public event EventHandler ValueChanged;
    public double Value {get{return exactValue;}set{
        updating=true;value=Math.Max(0,Math.Min(359999.999,value));exactValue=value;hours.Value=(decimal)Math.Floor(value/3600);minutes.Value=(decimal)Math.Floor(value%3600/60);seconds.Value=(decimal)Math.Min(59.999,Math.Round(value%60,3));updating=false;
    }}
    public MovieTimeInput() {
        AutoSize=true;WrapContents=false;
        hours.Maximum=99;minutes.Maximum=59;seconds.Maximum=59.999m;seconds.DecimalPlaces=3;seconds.Increment=0.04m;
        foreach(var n in new[]{hours,minutes,seconds}){n.Width=n==seconds?110:65;n.Margin=new Padding(0,0,2,0);n.ValueChanged+=(s,e)=>{if(!updating){exactValue=(double)hours.Value*3600+(double)minutes.Value*60+(double)seconds.Value;if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}};}
        hours.AccessibleName="Movie hours";minutes.AccessibleName="Movie minutes";seconds.AccessibleName="Movie seconds and milliseconds";
        Controls.Add(hours);Controls.Add(new Label {Text=":",AutoSize=true});Controls.Add(minutes);Controls.Add(new Label {Text=":",AutoSize=true});Controls.Add(seconds);
    }
    public static string Stamp(double seconds){return TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(@"hh\:mm\:ss\.fff");}
}
