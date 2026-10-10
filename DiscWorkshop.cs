using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

static class Program {
    [STAThread] static void Main(string[] args) {
        if (args.Length > 0 && args[0] == "--self-test") {
            try { Engine.SelfTest(); } catch(Exception e) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results","FAIL.txt"),e.ToString()); Environment.ExitCode=1; }
            return;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new Workshop());
    }
}

public static partial class Engine {
    // Windows CommandLineToArgvW quoting; no shell is involved.
    public static string Q(string value) {
        var b = new StringBuilder("\""); int slashes = 0;
        foreach(char c in value) {
            if(c == '\\') { slashes++; continue; }
            if(c == '"') { b.Append('\\', slashes * 2 + 1); b.Append(c); }
            else { b.Append('\\', slashes); b.Append(c); }
            slashes = 0;
        }
        b.Append('\\', slashes * 2); return b.Append('"').ToString();
    }
    public static string Args(params string[] values) { return string.Join(" ", values.Select(Q)); }
    public static void Run(string exe, string[] args, string cwd, CancellationToken token, Action<string> log) {
        token.ThrowIfCancellationRequested();
        var psi = new ProcessStartInfo(exe, Args(args)) { UseShellExecute=false, CreateNoWindow=true,
            RedirectStandardError=true, RedirectStandardOutput=true, WorkingDirectory=cwd };
        using(var p = new Process { StartInfo=psi }) {
            p.OutputDataReceived += (s,e) => { if(e.Data != null) log(e.Data); };
            p.ErrorDataReceived += (s,e) => { if(e.Data != null) log(e.Data); };
            log(Path.GetFileName(exe) + " " + Args(args));
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            while(!p.WaitForExit(150)) {
                if(token.IsCancellationRequested) { try { p.Kill(); } catch(InvalidOperationException) {} p.WaitForExit(); token.ThrowIfCancellationRequested(); }
            }
            p.WaitForExit(); token.ThrowIfCancellationRequested();
            if(p.ExitCode != 0) throw new Exception(Path.GetFileName(exe) + " failed (exit " + p.ExitCode + "). See the log below.");
        }
    }
    public static string[] ConvertArgs(string input, string output, bool pal, bool hdr, int audio, bool squeeze = false, int videoKbps = 1150, bool compact = false, string format = "VCD",EncodingOptions options=null) {
        bool svcd=format=="SVCD";
        int w=svcd?480:352, h=svcd?(pal?576:480):(pal?288:240);
        if(options!=null){w=options.Width;h=options.Height;}
        // Normalize to square pixels first so anamorphic DVD inputs keep their display aspect.
        string filter = "bwdif=mode=send_frame:parity=auto:deint=interlaced,";
        if(hdr) filter += "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,";
        // Squeeze a 16:9 display canvas into the 4:3 encoded frame. A TV's
        // full-width 16:9 mode reverses this. Scope/4:3 content keeps its proportions.
        int canvasHeight = squeeze || (options!=null&&options.Aspect=="16:9") ? w*9/16 : w*3/4;
        filter += "scale=trunc(iw*sar/2)*2:ih,setsar=1,scale="+w+":"+canvasHeight+":force_original_aspect_ratio=decrease:force_divisible_by=2,pad="+w+":"+canvasHeight+":(ow-iw)/2:(oh-ih)/2,scale="+w+":" + h;
        var args = new List<string> { "-hide_banner", "-nostdin", "-nostats", "-progress", "pipe:1", "-n", "-i", input, "-map", "0:v:0", "-map", "0:a:"+audio,
            "-sn", "-dn", "-vf", filter, "-target", (pal?"pal-":"ntsc-")+(svcd?"svcd":"vcd"), "-aspect", "4:3", "-ac", "2" };
        if(compact || format!="VCD") args.AddRange(new [] {"-b:v",videoKbps+"k","-minrate:v",videoKbps+"k","-maxrate:v",videoKbps+"k"});
        if(svcd) args.AddRange(new [] {"-bf","0","-flags","+cgop","-sc_threshold","1000000000"});
        if(format=="XVCD") args.AddRange(new [] {"-muxrate",MuxRate(format,videoKbps)});
        args.Add(output); return ApplyEncodingOptions(args.ToArray(),options,format,videoKbps);
    }
    public sealed class ConversionProgress {
        double duration; int previous = -2; readonly object gate = new object();
        public ConversionProgress(double knownDuration=0) {duration=knownDuration;}
        public void Observe(string line, Action<int,string> update) {
            lock(gate) {
                var match=Regex.Match(line,@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
                if(match.Success && duration == 0) duration=double.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture)*3600+double.Parse(match.Groups[2].Value,CultureInfo.InvariantCulture)*60+double.Parse(match.Groups[3].Value,CultureInfo.InvariantCulture);
                if(!line.StartsWith("out_time_us=")) return;
                double micros; if(!double.TryParse(line.Substring(12),NumberStyles.Float,CultureInfo.InvariantCulture,out micros) || double.IsNaN(micros) || double.IsInfinity(micros)) return;
                double seconds=Math.Max(0,micros/1000000);
                int percent=duration>0 ? (int)Math.Min(99,Math.Max(0,seconds/duration*100)) : -1;
                if(percent>=0 && percent==previous) return;
                previous=percent;
                update(percent,duration>0 ? percent+"% (estimated)" : TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss")+" encoded • duration unknown");
            }
        }
    }
    public static string[] SegmentArgs(string list, string output, int seconds) {
        return new [] { "-hide_banner", "-nostdin", "-n", "-f", "concat", "-safe", "1", "-i", list,
            "-map", "0:v:0", "-map", "0:a:0", "-c", "copy", "-f", "segment", "-segment_format", "vcd",
            "-segment_format_options", "packetsize=2324:muxrate=1411200", "-segment_time", seconds.ToString(), "-reset_timestamps", "1", output };
    }
    public static string Find(string name, params string[] candidates) {
        foreach(string p in new [] { Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", name) }.Concat(candidates)
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').Select(p => Path.Combine(p.Trim('"'), name))))
            if(File.Exists(p)) return p;
        return "";
    }
    public static void SelfTest() {
        if(Q("a b") != "\"a b\"" || Q("C:\\test\\") != "\"C:\\test\\\\\"" || Q("a\"b") != "\"a\\\"b\"") throw new Exception("Quoting failed");
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results"); Directory.CreateDirectory(root);
        File.Delete(Path.Combine(root,"PASS.txt")); File.Delete(Path.Combine(root,"FAIL.txt"));
        string run = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(run);
        string ff = Find("ffmpeg.exe", @"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe");
        var log = new List<string>(); Action<string> write = s => { lock(log) log.Add(s); };
        try {
            var parsed=new ConversionProgress(); int parsedPercent=-9;
            parsed.Observe("out_time_us=1000000",(p,d)=>parsedPercent=p);
            if(parsedPercent!=-1) throw new Exception("Unknown-duration progress failed");
            parsed.Observe("  Duration: 00:00:08.00, start: 0",(p,d)=>parsedPercent=p);
            parsed.Observe("out_time_us=4000000",(p,d)=>parsedPercent=p);
            if(parsedPercent!=50) throw new Exception("Progress midpoint failed");
            parsed.Observe("out_time_us=9000000",(p,d)=>parsedPercent=p);
            if(parsedPercent!=99) throw new Exception("Progress completion guard failed");
            Run(ff, new [] {"-hide_banner","-nostdin","-f","lavfi","-i","testsrc2=size=640x360:rate=30","-f","lavfi","-i","sine=frequency=440:sample_rate=48000","-t","8","-c:v","libx264","-c:a","aac","source.mp4"}, run, CancellationToken.None, write);
            foreach(bool pal in new [] {false,true}) {
                string name=pal ? "pal" : "ntsc";
                Run(ff, ConvertArgs("source.mp4", name+".mpg",pal,false,0),run,CancellationToken.None,write);
                File.WriteAllText(Path.Combine(run,name+".txt"),"file '"+name+".mpg'\nfile '"+name+".mpg'\n",new UTF8Encoding(false));
                Run(ff,SegmentArgs(name+".txt",name+"-%02d.mpg",3),run,CancellationToken.None,write);
                if(Directory.GetFiles(run,name+"-*.mpg").Length < 2) throw new Exception("Segmentation failed");
                string imager=Find("vcdimager.exe");
                if(!File.Exists(imager)) throw new Exception("VCDImager is required for end-to-end tests.");
                foreach(string segment in Directory.GetFiles(run,name+"-*.mpg")) {
                    string stem=Path.GetFileNameWithoutExtension(segment);
                    Run(imager,new [] {"--type=vcd2","--cue-file="+stem+".cue","--bin-file="+stem+".bin",Path.GetFileName(segment)},run,CancellationToken.None,write);
                    if(!File.ReadAllText(Path.Combine(run,stem+".cue")).Contains("TRACK 02 MODE2/2352")) throw new Exception("Missing VCD movie track");
                    if(new FileInfo(Path.Combine(run,stem+".bin")).Length % 2352 != 0) throw new Exception("Bad raw-sector image size");
                }
            }
            // White 16:9 anamorphic DVD-style source: squeeze must reach the
            // top/bottom of the encoded picture; letterbox must retain black bars.
            Run(ff,new [] {"-hide_banner","-nostdin","-f","lavfi","-i","color=white:size=720x480:rate=30","-f","lavfi","-i","sine=frequency=440","-vf","setsar=32/27","-t","2","-c:v","ffv1","-c:a","pcm_s16le","wide.mkv"},run,CancellationToken.None,write);
            foreach(bool pal in new [] {false,true}) foreach(bool squeezed in new [] {false,true}) {
                string stem=(pal?"pal":"ntsc")+(squeezed?"-squeeze":"-letterbox");
                int progressEvents=0; var live=new ConversionProgress();
                Run(ff,ConvertArgs("wide.mkv",stem+".mpg",pal,false,0,squeezed),run,CancellationToken.None,line=> {write(line);live.Observe(line,(p,d)=>progressEvents++);});
                if(progressEvents==0) throw new Exception("FFmpeg emitted no usable progress");
                Run(ff,new [] {"-hide_banner","-nostdin","-i",stem+".mpg","-frames:v","1","-pix_fmt","gray","-f","rawvideo",stem+".gray"},run,CancellationToken.None,write);
                byte[] pixels=File.ReadAllBytes(Path.Combine(run,stem+".gray")); int height=pal?288:240;
                if(pixels.Length!=352*height || pixels[(height/2)*352+176]<200) throw new Exception("Bad decoded VCD dimensions/content");
                if(squeezed ? pixels[176]<200 : pixels[176]>30) throw new Exception("Anamorphic/letterbox pixel geometry failed");
                Run(Find("vcdimager.exe"),new [] {"--type=vcd2","--cue-file="+stem+".cue","--bin-file="+stem+".bin",stem+".mpg"},run,CancellationToken.None,write);
            }
            Run(ff,new [] {"-hide_banner","-nostdin","-f","lavfi","-i","testsrc2=size=3840x2160:rate=24","-f","lavfi","-i","sine=frequency=440:sample_rate=48000","-t","1","-c:v","ffv1","-pix_fmt","yuv420p10le","-color_primaries","bt2020","-color_trc","smpte2084","-colorspace","bt2020nc","-c:a","pcm_s16le","hdr.mkv"},run,CancellationToken.None,write);
            Run(ff,ConvertArgs("hdr.mkv","hdr.mpg",false,true,0),run,CancellationToken.None,write);
            using(var stop=new CancellationTokenSource()) {
                stop.CancelAfter(350);
                bool cancelled=false;
                try {Run(ff,new [] {"-re","-f","lavfi","-i","testsrc2","-t","60","-f","null","-"},run,stop.Token,write);} catch(OperationCanceledException) {cancelled=true;}
                if(!cancelled) throw new Exception("Cancellation failed");
            }
            PlanningTest(ff,run,write);
            ProfileTest(ff,run,write);
            File.WriteAllText(Path.Combine(root,"PASS.txt"),"PASS v1.8.3: VCD regression suite; NTSC/PAL SVCD and custom XVCD encoding, dimensions, audio, splitting, authoring and strict decoding; bitrate/capacity planning; icon extraction and shortcut creation; donation destination.\r\n"+run);
        } finally { File.WriteAllLines(Path.Combine(run,"test.log"),log); }
    }
}

partial class Workshop : Form {
    ListBox sources=new ListBox(), images=new ListBox();
    TextBox ff=new TextBox(), vcd=new TextBox(), burn=new TextBox(), destination=new TextBox(), log=new TextBox();
    ComboBox standard=new ComboBox(); NumericUpDown audio=new NumericUpDown();
    CheckBox hdr=new CheckBox(), archive=new CheckBox(), author=new CheckBox(), squeeze=new CheckBox(), oneCD=new CheckBox();
    ComboBox cdSize=new ComboBox(), outputFormat=new ComboBox(); NumericUpDown customRate=new NumericUpDown();
    ProgressBar progress=new ProgressBar();
    Button create=new Button(), cancel=new Button(); Label status=new Label();
    FlowLayoutPanel actions=new FlowLayoutPanel(); Panel config=new Panel();
    CancellationTokenSource cancellation; bool busy; string job;
    string settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DiscWorkshop","tools.txt");
    public Workshop() {
        AutoScaleMode=AutoScaleMode.None;
        Text="Disc Workshop 1.8.3 • VCD / SVCD / XVCD"; MinimumSize=new Size(800,600); Size=new Size(1080,900);
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(243,245,249); StartPosition=FormStartPosition.CenterScreen;
        var root=new TableLayoutPanel { Dock=DockStyle.Fill, Padding=new Padding(22), ColumnCount=1,RowCount=9 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int h in new [] {88,147,333,44,30,24,92}) root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,28)); Controls.Add(root);
        var header=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=4,RowCount=1}; root.Controls.Add(header);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,54));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
        header.Controls.Add(new PictureBox {Image=Branding.Draw(256),SizeMode=PictureBoxSizeMode.Zoom,Dock=DockStyle.Fill,Margin=new Padding(0,0,10,10)},0,0);
        var titlePanel=new Panel {Dock=DockStyle.Fill};header.Controls.Add(titlePanel,1,0);
        titlePanel.Controls.Add(new Label {Left=0,Top=0,Width=620,Height=34,Text="DISC WORKSHOP",Font=new Font("Segoe UI",20,FontStyle.Bold)});
        titlePanel.Controls.Add(new Label {Left=2,Top=37,Width=620,Height=26,Text="VCD / SVCD / XVCD"});
        header.Controls.Add(new DonationArrow {Dock=DockStyle.Top,Height=36,Margin=new Padding(0,10,0,0)},2,0);
        var donate=new Button {Text="Please donate",Dock=DockStyle.Top,Height=36,Margin=new Padding(5,10,0,0),BackColor=Color.FromArgb(22,128,61),ForeColor=Color.White,FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false};
        donate.FlatAppearance.BorderSize=0;donate.FlatAppearance.MouseOverBackColor=Color.FromArgb(21,108,53);donate.FlatAppearance.MouseDownBackColor=Color.FromArgb(16,88,43);
        header.Controls.Add(donate,3,0);
        var donationMenu=Branding.CreateDonationMenu(this);
        donate.Click+=(s,e)=>donationMenu.Show(donate,new Point(0,donate.Height));
        var sourcePanel=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};
        sourcePanel.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); sourcePanel.RowStyles.Add(new RowStyle(SizeType.Percent,100)); sourcePanel.RowStyles.Add(new RowStyle(SizeType.Absolute,43));
        sourcePanel.Controls.Add(new Label {Text="1  Add movie files in playback order (DVD: VTS_XX_1.VOB onward; Blu-ray: selected STREAM clips)",AutoSize=true});
        sources.Dock=DockStyle.Fill; sources.HorizontalScrollbar=true; sourcePanel.Controls.Add(sources);
        actions.Dock=DockStyle.Fill; sourcePanel.Controls.Add(actions); root.Controls.Add(sourcePanel);
        AddButton(actions,"Add files…",()=>AddFiles()); AddButton(actions,"Remove",()=> { if(sources.SelectedIndex>=0) sources.Items.RemoveAt(sources.SelectedIndex); });
        AddButton(actions,"Move up",()=>MoveClip(-1)); AddButton(actions,"Move down",()=>MoveClip(1));
        AddButton(actions,"Source help",()=>MessageBox.Show("DVD: open VIDEO_TS and add one title set's numbered VOBs, excluding _0 menu files.\nBlu-ray/UHD: open BDMV\\STREAM and select the movie's M2TS clips in correct order.\n\nThis version does not parse DVD titles or Blu-ray MPLS playlists. Seamless branching and multi-angle titles require a preassembled movie file. It does not decrypt discs.\n\nEach clip must have video and the selected audio stream. To burn subtitles, use one assembled movie file and choose an embedded track or external text subtitle file.","Choosing a movie"));
        config.Dock=DockStyle.Fill; root.Controls.Add(config);
        ToolRow("FFmpeg",ff,0); ToolRow("VCDImager",vcd,34); ToolRow("ImgBurn",burn,68);
        var outLabel=new Label {Text="Output folder",Left=0,Top=108,Width=110}; config.Controls.Add(outLabel);
        destination.SetBounds(115,104,650,28); destination.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; config.Controls.Add(destination);
        var browse=new Button {Text="Browse…",Left=775,Top=103,Width=120,Height=30,Anchor=AnchorStyles.Top|AnchorStyles.Right}; config.Controls.Add(browse);
        browse.Click+=(s,e)=> { using(var d=new FolderBrowserDialog()) if(d.ShowDialog()==DialogResult.OK) destination.Text=d.SelectedPath; };
        standard.Items.AddRange(new object[]{"NTSC (North America / Japan)","PAL (Europe / other regions)"}); standard.SelectedIndex=0; standard.DropDownStyle=ComboBoxStyle.DropDownList; standard.SetBounds(0,145,285,28); config.Controls.Add(standard);
        config.Controls.Add(new Label {Text="Audio stream (0 = first)",Left=300,Top=149,Width=172}); audio.SetBounds(475,145,55,28); audio.Maximum=31; config.Controls.Add(audio);
        hdr.Text="HDR → SDR (HDR10 / HLG sources)"; hdr.SetBounds(545,143,340,32); config.Controls.Add(hdr);
        archive.Text="Keep copies of source files"; archive.SetBounds(0,181,270,30); config.Controls.Add(archive);
        author.Text="Author burnable BIN/CUE images (requires VCDImager)"; author.Checked=true; author.SetBounds(285,181,580,30); config.Controls.Add(author);
        squeeze.Text="Anamorphic squeeze for 16:9 TV (set TV to Full / Wide; off = 4:3 letterbox)";
        squeeze.Checked=true; squeeze.SetBounds(0,217,875,30); config.Controls.Add(squeeze);
        oneCD.Text="Fit whole film on one CD (automatic lower bitrate; off by default)";
        oneCD.Checked=false; oneCD.SetBounds(0,253,650,30); config.Controls.Add(oneCD);
        cdSize.Items.AddRange(new object[] {"74-minute CD","80-minute CD"}); cdSize.SelectedIndex=0;
        cdSize.DropDownStyle=ComboBoxStyle.DropDownList; cdSize.SetBounds(665,253,205,28); config.Controls.Add(cdSize);
        outputFormat.Items.AddRange(new object[] {"VCD","SVCD","XVCD"}); outputFormat.SelectedIndex=0; outputFormat.DropDownStyle=ComboBoxStyle.DropDownList;
        config.Controls.Add(new Label {Text="Output format",Left=0,Top=291,Width=110}); outputFormat.SetBounds(115,287,140,28); config.Controls.Add(outputFormat);
        config.Controls.Add(new Label {Text="Video bitrate (kb/s)",Left=275,Top=291,Width=145}); customRate.SetBounds(425,287,95,28); customRate.Minimum=200; customRate.Maximum=2500; customRate.Value=1800; customRate.Increment=50; customRate.Enabled=false; config.Controls.Add(customRate);
        config.Controls.Add(new Label {Text="VCD: standard • SVCD: MPEG-2 • XVCD: custom",Left=535,Top=291,Width=395});
        outputFormat.SelectedIndexChanged+=(s,e)=>customRate.Enabled=outputFormat.SelectedIndex>0&&!oneCD.Checked&&!maxRate.Checked&&!useAdvanced.Checked;
        oneCD.CheckedChanged+=(s,e)=>customRate.Enabled=outputFormat.SelectedIndex>0&&!oneCD.Checked&&!maxRate.Checked&&!useAdvanced.Checked;
        var runPanel=new FlowLayoutPanel {Dock=DockStyle.Fill}; root.Controls.Add(runPanel);
        create.Text="Create Video CDs"; create.Size=new Size(190,36); create.BackColor=Color.FromArgb(35,88,204); create.ForeColor=Color.White; create.FlatStyle=FlatStyle.Flat; create.Click+=async(s,e)=>await Create(); runPanel.Controls.Add(create);
        cancel.Text="Cancel"; cancel.Size=new Size(100,36); cancel.Enabled=false; cancel.Click+=(s,e)=>{ if(cancellation!=null) { cancellation.Cancel(); status.Text="Cancelling…"; } }; runPanel.Controls.Add(cancel);
        AddButton(runPanel,"Open output",()=> { if(Directory.Exists(job ?? destination.Text)) Process.Start(new ProcessStartInfo(job ?? destination.Text) {UseShellExecute=true}); });
        AddButton(runPanel,"Create shortcut…",()=>Branding.Shortcut(this));
        status.Text="Ready • Choose output format and CD size • Quiet audio + black-screen disc breaks enabled"; status.Dock=DockStyle.Fill; root.Controls.Add(status);
        progress.Dock=DockStyle.Fill; progress.Maximum=100; progress.AccessibleName="Current stage progress"; root.Controls.Add(progress);
        var results=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2}; results.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); results.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,195));
        images.Dock=DockStyle.Fill; images.HorizontalScrollbar=true; results.Controls.Add(images); var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill}; results.Controls.Add(buttons); root.Controls.Add(results);
        AddButton(buttons,"Burn selected CD…",()=>Burn()); AddButton(buttons,"Open existing CUE…",()=> { using(var d=new OpenFileDialog {Filter="VCD cue sheet|*.cue"}) if(d.ShowDialog()==DialogResult.OK) {images.Items.Add(d.FileName); images.SelectedIndex=images.Items.Count-1;} });
        log.Dock=DockStyle.Fill; log.Multiline=true; log.ReadOnly=true; log.ScrollBars=ScrollBars.Both; log.WordWrap=false; log.Font=new Font("Consolas",9); root.Controls.Add(log);
        var footer=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        footer.Controls.Add(new Label {AutoSize=true,Text="4K and HD are downscaled to SD. Player must support Video CD/CD-R. Burning opens ImgBurn for drive selection and final Write."});
        footer.Controls.Add(new Label {Name="creatorCredit",AutoSize=true,Text="Made by Albert Mark Burdge"});root.Controls.Add(footer);
        ff.Text=Engine.Find("ffmpeg.exe",@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe");
        vcd.Text=Engine.Find("vcdimager.exe"); burn.Text=Engine.Find("ImgBurn.exe",@"C:\Program Files (x86)\ImgBurn\ImgBurn.exe",@"C:\Program Files\ImgBurn\ImgBurn.exe");
        destination.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"DiscWorkshop");
        try {if(File.Exists(settings)) { var a=File.ReadAllLines(settings); if(a.Length>=3) { if(File.Exists(a[0])) ff.Text=a[0]; if(File.Exists(a[1])) vcd.Text=a[1]; if(File.Exists(a[2])) burn.Text=a[2]; } }}catch(IOException){}catch(UnauthorizedAccessException){}
        SetupMovieOptions(root);
        FormClosing+=(s,e)=> { if(busy) {e.Cancel=true; MessageBox.Show("Cancel the conversion and wait for it to stop before closing.");} };
    }
    void AddButton(Control parent,string text,Action action) {var b=new Button {Text=text,AutoSize=true,Height=32,Margin=new Padding(0,4,8,4)}; b.Click+=(s,e)=>{try {action();} catch(Exception ex) {MessageBox.Show(ex.Message,"Disc Workshop");}}; parent.Controls.Add(b);}
    void ToolRow(string label,TextBox box,int y) {
        config.Controls.Add(new Label {Text=label,Left=0,Top=y+6,Width=110}); box.SetBounds(115,y,650,28); box.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right; config.Controls.Add(box);
        var b=new Button {Text="Locate .exe…",Left=775,Top=y,Width=120,Height=29,Anchor=AnchorStyles.Top|AnchorStyles.Right}; config.Controls.Add(b);
        b.Click+=(s,e)=> {using(var d=new OpenFileDialog {Filter="Executable|*.exe"}) if(d.ShowDialog()==DialogResult.OK) box.Text=d.FileName;};
    }
    void AddFiles() {using(var d=new OpenFileDialog {Multiselect=true,Filter="Video files|*.vob;*.m2ts;*.mts;*.mp4;*.mkv;*.avi;*.mov;*.mpg;*.mpeg;*.ts|All files|*.*"}) if(d.ShowDialog()==DialogResult.OK) foreach(var p in d.FileNames.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)) sources.Items.Add(p);}
    void MoveClip(int delta) {int i=sources.SelectedIndex,j=i+delta; if(i<0||j<0||j>=sources.Items.Count)return; object x=sources.Items[i]; sources.Items.RemoveAt(i); sources.Items.Insert(j,x); sources.SelectedIndex=j;}
    void Log(string value) { if(IsDisposed)return; BeginInvoke((Action)(()=> {if(log.TextLength>120000) log.Text=log.Text.Substring(log.TextLength-60000); log.AppendText(value+Environment.NewLine);})); }
    void Progress(int percent,string message) {
        if(IsDisposed)return;
        BeginInvoke((Action)(()=> {
            if(cancellation!=null && cancellation.IsCancellationRequested)return;
            progress.Style=percent<0 ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            progress.Value=Math.Max(0,Math.Min(100,percent)); status.Text=message;
        }));
    }
    async Task Create() {
        if(busy)return;
        if(sources.Items.Count==0) {MessageBox.Show("Add the movie's video files first.");return;}
        if(!File.Exists(ff.Text) || (author.Checked&&!File.Exists(vcd.Text))) {MessageBox.Show("Locate FFmpeg and VCDImager above. You can uncheck authoring to create MPEG files only. See README for setup links.");return;}
        string[] inputs=sources.Items.Cast<string>().ToArray();
        if(inputs.Any(p=>!File.Exists(p))) {MessageBox.Show("A source is missing. Insert the source disc or select files again.");return;}
        string encoder=ff.Text,imager=vcd.Text; bool pal=standard.SelectedIndex==1,tone=hdr.Checked,copy=archive.Checked,makeImages=author.Checked,anamorphic=squeeze.Checked,compact=oneCD.Checked; int track=(int)audio.Value,minutes=cdSize.SelectedIndex==1?80:74;
        bool maximize=maxRate.Checked,manual=manualBreaks.Checked; int requestedCount=(int)discCount.Value;
        double[] requested=breakTimes.ToArray(); SubtitleChoice subtitle=subtitleTracks.SelectedItem as SubtitleChoice;
        if(manual && (requested.Length==0 || breakSources!=string.Join("\n",inputs))) {MessageBox.Show("Choose disc breaks for the current source files first.");return;}
        if(subtitle!=null && subtitle.Enabled && inputs.Length!=1) {MessageBox.Show("For subtitle burning, select one assembled movie file so caption times and track selection stay aligned.");return;}
        if(subtitle!=null && subtitle.Enabled && subtitleSource!=inputs[0] && string.IsNullOrEmpty(subtitle.FilePath)) {MessageBox.Show("Scan subtitle tracks for the current movie first.");return;}
        string format=SelectedProfile;EncodingOptions encoding=useAdvanced.Checked&&format!="VCD"?advancedOptions.Copy():null;
        int selectedRate=encoding!=null?encoding.VideoRate:format=="VCD"?1150:(int)customRate.Value;
        if((compact||maximize) && format=="VCD") format="XVCD";
        double fades=fadeBreaks.Checked?(double)fadeDuration.Value:0;
        double opening=blackBefore.Checked?(double)beforeSeconds.Value:0,ending=blackAfter.Checked?(double)afterSeconds.Value:0;
        bool reencode=maximize||manual||compact||encoding!=null||fades>0||opening>0||ending>0;
        string probe=Engine.Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(encoder),"ffprobe.exe"));
        if(!File.Exists(probe)) {MessageBox.Show("FFprobe.exe is needed for timing and disc planning. Put it beside ffmpeg.exe or in the app's tools folder.");return;}
        try {
            if(!Path.IsPathRooted(destination.Text)) throw new Exception("Choose an absolute output folder.");
            Directory.CreateDirectory(destination.Text);
            job=Path.Combine(destination.Text,"VCD-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)); Directory.CreateDirectory(job);
            try{Directory.CreateDirectory(Path.GetDirectoryName(settings)); File.WriteAllLines(settings,new [] {ff.Text,vcd.Text,burn.Text});}catch(IOException){}catch(UnauthorizedAccessException){}
        } catch(Exception ex) {MessageBox.Show(ex.Message);return;}
        string work=job; busy=true; LockMovieInputs(true); cancel.Enabled=true; images.Items.Clear(); log.Clear();
        cancellation=new CancellationTokenSource(); var token=cancellation.Token; progress.Value=0; progress.Style=ProgressBarStyle.Marquee; status.Text="Preparing conversion…";
        try {
            string[] ready=await Task.Run(()=> {
                using(var writer=new StreamWriter(Path.Combine(work,"job.log"),false,new UTF8Encoding(false))) {
                    Action<string> report=line=>{lock(writer){writer.WriteLine(line);writer.Flush();} Log(line);};
                    int videoRate=selectedRate;
                    if(compact) {
                        Progress(-1,"Measuring film duration for one-CD bitrate…");
                        double sourceDuration=inputs.Sum(p=>Engine.Duration(probe,p,work,token,report));
                        videoRate=Engine.MaximumRate(sourceDuration+opening+ending,minutes,format,encoding);
                        report("One-CD "+format+" enabled: estimated duration "+Engine.Number(sourceDuration)+" seconds; video "+videoRate+" kb/s, audio "+(encoding==null?224:encoding.AudioRate)+" kb/s; "+minutes+"-minute media.");
                    }
                    var normalized=new List<string>();
                    for(int i=0;i<inputs.Length;i++) {
                        token.ThrowIfCancellationRequested(); string input=inputs[i];
                        if(copy) {
                            string stored=Path.Combine(work,"source-"+i.ToString("D3")+Path.GetExtension(input));
                            report("Copying source " +(i+1)+" / "+inputs.Length);
                            Progress(-1,"Copying source " +(i+1)+" / "+inputs.Length);
                            using(var src=File.OpenRead(input)) using(var dst=new FileStream(stored,FileMode.CreateNew,FileAccess.Write)) {var buffer=new byte[1024*1024]; int n; while((n=src.Read(buffer,0,buffer.Length))>0) {token.ThrowIfCancellationRequested();dst.Write(buffer,0,n);}}
                            input=stored;
                        }
                        string encoded="clip-"+i.ToString("D3")+(reencode?".mkv":".mpg"); report("Converting clip " +(i+1)+" / "+inputs.Length);
                        string stage="Converting clip "+(i+1)+" / "+inputs.Length+" • ";
                        Progress(-1,stage+"reading duration…");
                        var tracker=new Engine.ConversionProgress();
                        string captions=Engine.PrepareSubtitle(encoder,input,subtitle,work,i,token,report);
                        var conversion=Engine.WithSubtitles(Engine.ConvertArgs(input,encoded,pal,tone,track,anamorphic,videoRate,compact,format,encoding),subtitle,captions);
                        if(reencode)conversion=Engine.LosslessArgs(conversion,pal,encoding);
                        Engine.Run(encoder,conversion,work,token,line=> {
                            report(line); tracker.Observe(line,(percent,detail)=>Progress(percent,stage+detail));
                        });
                        Progress(100,stage+"100%"); normalized.Add(encoded);
                    }
                    File.WriteAllLines(Path.Combine(work,"clips.txt"),normalized.Select(n=>"file '"+n+"'"),new UTF8Encoding(false));
                    double duration=normalized.Sum(p=>Engine.Duration(probe,p,work,token,report));
                    double limit=Engine.VolumeSeconds(format,videoRate,minutes,encoding==null?224:encoding.AudioRate)-opening-ending;
                    var cuts=manual?Engine.ManualCuts(requested,duration,pal,encoding==null?0:encoding.Fps):compact?new List<Engine.DiscCut>():Engine.AnalyzeCuts(encoder,probe,"clips.txt",work,duration,token,report,Progress,maximize?duration:limit,300,maximize?requestedCount:0);
                    Engine.WritePlan(work,cuts,duration,compact,videoRate,minutes,report,format,limit,encoding==null?224:encoding.AudioRate);
                    if(maximize||compact){string note="Calculated rates are recorded in DISC-BITRATES.csv ("+(encoding==null?2500:encoding.MaximumRate)+" kb/s ceiling).";report(note);File.AppendAllText(Path.Combine(work,"DISC-PLAN.txt"),note+Environment.NewLine);}
                    if(encoding!=null){string note="Custom encoding: "+encoding.Width+"x"+encoding.Height+", "+encoding.FrameRate+" fps, "+encoding.Aspect+", audio "+encoding.AudioRate+" kb/s / "+encoding.SampleRate+" Hz / "+encoding.Channels+" channel(s), GOP "+encoding.Gop+".";report(note);File.AppendAllText(Path.Combine(work,"DISC-PLAN.txt"),note+Environment.NewLine);}
                    Progress(-1,compact?"Preparing one-CD "+format+"…":"Splitting at selected disc boundaries…");
                    if(fades>0&&cuts.Count>0){string note="Disc-break picture/audio fades: "+Engine.Number(fades)+" seconds per side, shortened on small volumes; no frames or running time removed.";report(note);File.AppendAllText(Path.Combine(work,"DISC-PLAN.txt"),note+Environment.NewLine);}
                    if(opening>0||ending>0){string note="Every disc: "+Engine.Number(opening)+" seconds of silent black before video; "+Engine.Number(ending)+" seconds after video / fade. Included in capacity and bitrate calculations.";report(note);File.AppendAllText(Path.Combine(work,"DISC-PLAN.txt"),note+Environment.NewLine);}
                    if(reencode)Engine.EncodeVolumes(encoder,probe,work,cuts,duration,pal,format,videoRate,maximize||compact,minutes,token,report,Progress,encoding,fades,opening,ending);
                    else {Engine.Run(encoder,Engine.PlannedOutputArgs("clips.txt",cuts,compact,format,videoRate),work,token,report);Engine.ValidateCuts(work,cuts);}
                    if(!reencode && format!="SVCD" && (compact || format=="XVCD")) {
                        Progress(-1,"Removing empty VCD padding for one-CD XVCD…");
                        foreach(string padded in Directory.GetFiles(work,"padded-*.mpg")) Engine.RemoveEmptyPadding(padded,Path.Combine(work,Path.GetFileName(padded).Replace("padded-","disc-")),token);
                    }
                    var discs=Directory.GetFiles(work,"disc-*.mpg").OrderBy(x=>x).ToArray();
                    if(discs.Length==0) throw new Exception("No disc segments were created.");
                    if(discs.Length!=cuts.Count+1) throw new Exception("Disc splitting did not match the planned boundaries. Check the log and source timestamps.");
                    if(compact && new FileInfo(discs[0]).Length/2324.0*2352+1200L*2352>Engine.DiscCapacity(minutes))
                        throw new Exception("The finished video exceeds the selected CD capacity, possibly because the source duration was inaccurate. Choose larger media or use multiple discs. No ready-to-burn result has been marked complete.");
                    var results=new List<string>();
                    if(makeImages) foreach(string disc in discs) {
                        token.ThrowIfCancellationRequested(); string name=Path.GetFileNameWithoutExtension(disc);
                        Progress(-1,"Authoring CD "+(results.Count+1)+" / "+discs.Length+"…");
                        Engine.Author(imager,name,work,token,report,format);
                        string bin=Path.Combine(work,name+".bin"),cue=Path.Combine(work,name+".cue");
                        if(!File.Exists(cue)||!File.Exists(bin)||new FileInfo(bin).Length==0) throw new Exception("Authoring produced no usable image.");
                        if(new FileInfo(bin).Length > Engine.DiscCapacity(minutes)) throw new Exception("The authored image exceeds the selected CD capacity. Use larger media or multiple discs; this image is not ready to burn.");
                        results.Add(cue);
                    }
                    File.WriteAllText(Path.Combine(work,"COMPLETE.txt"),makeImages ? "Authored successfully. Burn each CUE with its matching BIN in the same folder. Physical burning is a separate step." : "MPEG conversion completed. Author these files with VCDImager before burning a Video CD.");
                    File.WriteAllText(Path.Combine(work,"PLAYBACK.txt"),anamorphic ? "Anamorphic squeeze: set the TV to Full / Wide 16:9 stretch (not zoom). Encoded as 4:3; automatic switching is not expected. Scope films retain top/bottom bars to preserve their original aspect ratio." : "Standard 4:3 letterbox: display as 4:3 without widescreen stretching.");
                    return results.ToArray();
                }
            });
            images.Items.AddRange(ready); if(ready.Length>0) images.SelectedIndex=0;
            progress.Style=ProgressBarStyle.Continuous; progress.Value=100;
            status.Text=makeImages ? "Ready to burn • "+ready.Length+" CD image(s). Select each disc and click Burn selected CD." : "MPEG files created • authoring was disabled. Open output to view them.";
        } catch(OperationCanceledException) {status.Text="Cancelled • partial files retained in output; do not burn partial images.";}
        catch(Exception ex) {status.Text="Failed • inspect job.log in the output folder"; Log(ex.ToString()); MessageBox.Show(ex.Message,"Conversion failed");}
        finally {progress.Style=ProgressBarStyle.Continuous; busy=false; LockMovieInputs(false); cancel.Enabled=false; cancellation.Dispose(); cancellation=null;}
    }
    void Burn() {
        if(busy) throw new Exception("Wait for the conversion to finish.");
        if(images.SelectedItem==null) throw new Exception("Select a finished CUE image first.");
        if(!File.Exists(burn.Text)) throw new Exception("Locate ImgBurn.exe above first. See README for its official download page.");
        string cue=images.SelectedItem.ToString(); if(!File.Exists(cue)) throw new Exception("The CUE file is missing.");
        Process.Start(new ProcessStartInfo(burn.Text,Engine.Args("/MODE","WRITE","/SRC",cue,"/VERIFY","YES")) {UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(cue)});
        status.Text="ImgBurn opened • insert blank CD-R, select writer, then click Write. Verify stays enabled.";
    }
}





