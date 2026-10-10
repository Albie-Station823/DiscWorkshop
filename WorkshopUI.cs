using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;

partial class Workshop {
    CheckBox lowVision=new CheckBox(),maxRate=new CheckBox(),manualBreaks=new CheckBox();
    NumericUpDown discCount=new NumericUpDown(); ComboBox subtitleTracks=new ComboBox();
    List<double> breakTimes=new List<double>(); string breakSources,subtitleSource;
    FlowLayoutPanel optionRows,accessibilityBar; TableLayoutPanel movieRoot; Panel scroll;
    BusyInputFilter inputLock;CheckBox fadeBreaks=new CheckBox();NumericUpDown fadeDuration=new NumericUpDown();
    CheckBox blackBefore=new CheckBox(),blackAfter=new CheckBox();NumericUpDown beforeSeconds=new NumericUpDown(),afterSeconds=new NumericUpDown();
    string visionSettings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DiscWorkshop","accessibility.txt");
    CheckBox useAdvanced=new CheckBox();EncodingOptions advancedOptions;string advancedIdentity;
    string SelectedProfile {get {return outputFormat.SelectedIndex==1?"SVCD":outputFormat.SelectedIndex==2?"XVCD":"VCD";}}
    void SetupMovieOptions(TableLayoutPanel root) {
        movieRoot=root; root.Parent.Controls.Remove(root);
        scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true}; Controls.Add(scroll);scroll.Controls.Add(root);
        accessibilityBar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=48,Padding=new Padding(18,4,0,4),WrapContents=false};Controls.Add(accessibilityBar);accessibilityBar.SendToBack();
        inputLock=new BusyInputFilter(this);Application.AddMessageFilter(inputLock);FormClosed+=(s,e)=>Application.RemoveMessageFilter(inputLock);
        root.Dock=DockStyle.Top; root.AutoSize=false;
        // Replace fixed coordinate rows with wrapping rows, including the original settings.
        optionRows=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        var old=config.Controls.Cast<Control>().OrderBy(c=>c.Top/34).ThenBy(c=>c.Left).ToArray();
        config.Controls.Clear(); config.Controls.Add(optionRows);
        foreach(var group in old.GroupBy(c=>c.Top/34)) {
            var row=new FlowLayoutPanel {AutoSize=true,WrapContents=true,Margin=new Padding(0,2,0,2)};
            foreach(Control c in group) {c.Dock=DockStyle.None;c.Anchor=AnchorStyles.Top|AnchorStyles.Left; c.Margin=new Padding(3,4,8,4);row.Controls.Add(c);}
            optionRows.Controls.Add(row);
        }
        lowVision.Text="Low vision: large bold text and high contrast";
        lowVision.AccessibleName="Low vision accessibility mode";
        sources.AccessibleName="Movie source files in playback order";images.AccessibleName="Completed disc images";
        ff.AccessibleName="FFmpeg executable path";vcd.AccessibleName="VCDImager executable path";burn.AccessibleName="ImgBurn executable path";
        destination.AccessibleName="Output folder";audio.AccessibleName="Audio stream number";customRate.AccessibleName="XVCD video bitrate in kilobits per second";
        standard.AccessibleName="PAL or NTSC television standard";cdSize.AccessibleName="Blank CD capacity";outputFormat.AccessibleName="Output format";
        subtitleTracks.AccessibleName="Subtitle track to burn into picture";discCount.AccessibleName="Number of discs for maximum bitrate";
        maxRate.Text="Maximum bitrate per disc (VCD switches to XVCD)";
        oneCD.Text="Fit whole movie on one disc (selected MPEG-1 / MPEG-2 format)";
        outputFormat.Items.Clear();outputFormat.Items.AddRange(new object[]{"VCD • MPEG-1 standard","SVCD • MPEG-2","XVCD • MPEG-1 custom"});outputFormat.SelectedIndex=0;
        useAdvanced.Text="Use advanced XVCD / SVCD parameters";
        var parameters=new Button {Text="Encoding parameters…",AutoSize=true};
        parameters.Click+=(s,e)=> {
            if(outputFormat.SelectedIndex==0){MessageBox.Show("Choose XVCD (MPEG-1) or SVCD (MPEG-2) to edit encoding parameters.");return;}
            string identity=SelectedProfile+":"+standard.SelectedIndex;
            var initial=advancedIdentity==identity&&advancedOptions!=null?advancedOptions:EncodingOptions.Defaults(standard.SelectedIndex==1,SelectedProfile);
            using(var dialog=new EncodingDialog(initial,lowVision.Checked))if(dialog.ShowDialog(this)==DialogResult.OK){advancedOptions=dialog.Options;advancedIdentity=identity;useAdvanced.Checked=true;}
        };
        AddOptionRow(useAdvanced,parameters);
        parameters.Enabled=useAdvanced.Enabled=false;
        outputFormat.SelectedIndexChanged+=(s,e)=>{parameters.Enabled=useAdvanced.Enabled=outputFormat.SelectedIndex>0;useAdvanced.Checked=false;customRate.Value=outputFormat.SelectedIndex==1?2040:1800;customRate.Enabled=outputFormat.SelectedIndex>0&&!oneCD.Checked&&!maxRate.Checked;};
        standard.SelectedIndexChanged+=(s,e)=>{useAdvanced.Checked=false;};
        useAdvanced.CheckedChanged+=(s,e)=> {
            if(useAdvanced.Checked&&(advancedOptions==null||advancedIdentity!=SelectedProfile+":"+standard.SelectedIndex)){advancedOptions=EncodingOptions.Defaults(standard.SelectedIndex==1,SelectedProfile);advancedIdentity=SelectedProfile+":"+standard.SelectedIndex;}
            customRate.Enabled=outputFormat.SelectedIndex>0&&!oneCD.Checked&&!maxRate.Checked&&!useAdvanced.Checked;
        };
        discCount.Minimum=1;discCount.Maximum=20;discCount.Value=2;discCount.Width=90;
        accessibilityBar.Controls.Add(lowVision);
        AddOptionRow(maxRate,new Label {Text="Automatic disc count",AutoSize=true},discCount);
        AddOptionRow(new Label {Text="80-minute CD = typical 700 MB blank. Bitrate is limited to 2500 kb/s with space reserved for audio and authoring.",AutoSize=true});
        manualBreaks.Text="Use my chosen disc breaks";
        var edit=new Button {Text="Preview / choose breaks…",AutoSize=true};edit.Click+=async(s,e)=>await EditBreaks();
        AddOptionRow(manualBreaks,edit);
        fadeBreaks.Text="Fade picture and sound out / in at disc breaks";fadeDuration.Minimum=0.1m;fadeDuration.Maximum=10;fadeDuration.DecimalPlaces=1;fadeDuration.Increment=0.1m;fadeDuration.Value=1;fadeDuration.Width=90;fadeDuration.AccessibleName="Disc-break fade duration in seconds";
        AddOptionRow(fadeBreaks,new Label {Text="Seconds per fade",AutoSize=true},fadeDuration);
        blackBefore.Text="Black screen before video on every disc";blackAfter.Text="Black screen after video / fade on every disc";
        foreach(var seconds in new[]{beforeSeconds,afterSeconds}){seconds.Minimum=0.1m;seconds.Maximum=120;seconds.DecimalPlaces=1;seconds.Increment=0.1m;seconds.Value=4;seconds.Width=90;}
        beforeSeconds.AccessibleName="Opening black screen seconds";afterSeconds.AccessibleName="Ending black screen seconds";
        AddOptionRow(blackBefore,new Label {Text="Seconds",AutoSize=true},beforeSeconds);
        AddOptionRow(blackAfter,new Label {Text="Seconds",AutoSize=true},afterSeconds);
        subtitleTracks.DropDownStyle=ComboBoxStyle.DropDownList;subtitleTracks.Width=440;
        subtitleTracks.Items.Add(new SubtitleChoice());subtitleTracks.SelectedIndex=0;
        var scan=new Button {Text="Scan subtitle tracks",AutoSize=true};scan.Click+=async(s,e)=>await ScanSubtitles();
        var browse=new Button {Text="Browse subtitle file…",AutoSize=true};browse.Click+=(s,e)=> {
            using(var d=new OpenFileDialog {Filter="Text subtitles|*.srt;*.ass;*.ssa;*.vtt;*.sub|All files|*.*"})if(d.ShowDialog(this)==DialogResult.OK) {
                var choice=new SubtitleChoice {FilePath=d.FileName,Description="File: "+Path.GetFileName(d.FileName)};
                subtitleTracks.Items.Add(choice);subtitleTracks.SelectedItem=choice;
            }
        };
        AddOptionRow(new Label {Text="Burn subtitles into picture",AutoSize=true},subtitleTracks,scan,browse);
        AddOptionRow(new Label {Text="Subtitles require one assembled movie file. Manual breaks use the full playback order. Preview shows the source picture without subtitle burn-in.",AutoSize=true});
        maxRate.CheckedChanged+=(s,e)=> {if(maxRate.Checked)oneCD.Checked=false;discCount.Enabled=maxRate.Checked&&!manualBreaks.Checked;customRate.Enabled=outputFormat.SelectedIndex>0&&!oneCD.Checked&&!maxRate.Checked&&!useAdvanced.Checked;};
        oneCD.CheckedChanged+=(s,e)=> {if(oneCD.Checked){maxRate.Checked=false;manualBreaks.Checked=false;}};
        manualBreaks.CheckedChanged+=(s,e)=>{if(manualBreaks.Checked)oneCD.Checked=false;discCount.Enabled=maxRate.Checked&&!manualBreaks.Checked;};
        discCount.Enabled=false;
        try{lowVision.Checked=File.Exists(visionSettings)&&File.ReadAllText(visionSettings).Trim()=="1";}catch(IOException){}catch(UnauthorizedAccessException){}
        lowVision.CheckedChanged+=(s,e)=> {ApplyMovieStyle();try {Directory.CreateDirectory(Path.GetDirectoryName(visionSettings));File.WriteAllText(visionSettings,lowVision.Checked?"1":"0");}catch(IOException){}catch(UnauthorizedAccessException){}};
        scroll.Resize+=(s,e)=>LayoutMovieRows();
        Shown+=(s,e)=>LayoutMovieRows();
        ApplyMovieStyle();
    }
    void AddOptionRow(params Control[] controls) {var row=new FlowLayoutPanel {AutoSize=true,WrapContents=true,Margin=new Padding(0,2,0,2)};row.Controls.AddRange(controls);optionRows.Controls.Add(row);}
    void ApplyMovieStyle() {
        SuspendLayout();movieRoot.SuspendLayout();
        Font=new Font("Segoe UI",lowVision.Checked?15:10,lowVision.Checked?FontStyle.Bold:FontStyle.Regular);
        StyleMovieControls(this);LayoutMovieRows();
        movieRoot.ResumeLayout(true);ResumeLayout(true);
        if(IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed)LayoutMovieRows();}));
    }
    void StyleMovieControls(Control parent) {
        foreach(Control c in parent.Controls) {
            c.Font=Font;c.ForeColor=lowVision.Checked?Color.White:Color.FromArgb(20,25,35);
            c.BackColor=lowVision.Checked?Color.Black:Color.FromArgb(243,245,249);
            if(c is Button) {c.ForeColor=lowVision.Checked?Color.Yellow:Color.White;c.BackColor=lowVision.Checked?Color.Black:Color.FromArgb(35,88,150);((Button)c).FlatStyle=FlatStyle.Flat;((Button)c).AutoSizeMode=AutoSizeMode.GrowAndShrink;}
            if(c is CheckBox || c is Button || c is Label) {c.AutoSize=true;c.MinimumSize=new Size(0,lowVision.Checked?38:28);}
            if(c==status){c.AutoSize=false;c.MinimumSize=Size.Empty;}
            if(c==log){c.AutoSize=false;c.Font=new Font("Consolas",lowVision.Checked?13:9,FontStyle.Regular);}
            if(c.Name=="creatorCredit"){c.Font=new Font("Segoe UI",lowVision.Checked?11:9,FontStyle.Regular);c.ForeColor=lowVision.Checked?Color.LightGray:Color.DimGray;c.MinimumSize=Size.Empty;}
            if(c is TextBox || c is ComboBox || c is NumericUpDown)c.Height=lowVision.Checked?38:28;
            if(c is ListBox)((ListBox)c).ItemHeight=lowVision.Checked?32:20;
            if(string.IsNullOrEmpty(c.AccessibleName)&&!(c is Panel))c.AccessibleName=c.Text;
            StyleMovieControls(c);
        }
    }
    void LayoutMovieRows() {
        if(optionRows==null)return;
        int width=Math.Max(650,scroll.ClientSize.Width-70);
        accessibilityBar.Height=lowVision.Checked?62:46;lowVision.MaximumSize=new Size(Math.Max(600,ClientSize.Width-40),0);
        movieRoot.Width=width+44;
        foreach(FlowLayoutPanel row in optionRows.Controls) {
            row.MaximumSize=new Size(width,0);row.MinimumSize=new Size(width,0);
            foreach(Control c in row.Controls) {
                if(c is Label||c is CheckBox)c.MaximumSize=new Size(width-20,0);
                if(c is TextBox)c.Width=Math.Min(width-30,550);
                if(c is ComboBox)c.Width=Math.Min(width-30,c==subtitleTracks?480:c==standard||c==outputFormat?360:205);
            }
        }
        optionRows.PerformLayout();
        movieRoot.RowStyles[0].Height=lowVision.Checked?130:88;
        movieRoot.RowStyles[1].Height=lowVision.Checked?260:170;
        var sourcePanel=(TableLayoutPanel)movieRoot.GetControlFromPosition(0,1);
        sourcePanel.RowStyles[0].Height=lowVision.Checked?70:45;
        sourcePanel.RowStyles[2].Height=lowVision.Checked?75:43;
        foreach(Control c in sourcePanel.Controls)if(c is Label)c.MaximumSize=new Size(width-20,0);
        var header=(TableLayoutPanel)movieRoot.GetControlFromPosition(0,0);
        header.ColumnStyles[3].Width=lowVision.Checked?190:150;
        var title=header.GetControlFromPosition(1,0);
        foreach(Control c in title.Controls)if(c is Label){c.MaximumSize=new Size(Math.Max(200,title.Width-10),0);c.Width=Math.Max(200,title.Width-10);}
        var donate=header.GetControlFromPosition(3,0);donate.BackColor=Color.FromArgb(38,185,103);donate.ForeColor=Color.Black;
        var footer=movieRoot.GetControlFromPosition(0,8);foreach(Control c in footer.Controls)if(c is Label)c.MaximumSize=new Size(width-20,0);
        foreach(Control row in optionRows.Controls)row.PerformLayout();
        movieRoot.RowStyles[2].Height=optionRows.Controls.Cast<Control>().Sum(r=>r.PreferredSize.Height+r.Margin.Vertical)+16;
        movieRoot.RowStyles[3].Height=lowVision.Checked?110:84;
        movieRoot.RowStyles[4].Height=lowVision.Checked?75:55;
        movieRoot.RowStyles[6].Height=lowVision.Checked?160:110;
        var results=(TableLayoutPanel)movieRoot.GetControlFromPosition(0,6);results.ColumnStyles[1].Width=lowVision.Checked?300:240;
        movieRoot.RowStyles[7].SizeType=SizeType.Absolute;movieRoot.RowStyles[7].Height=180;
        movieRoot.RowStyles[8].Height=lowVision.Checked?110:85;
        movieRoot.Height=(int)movieRoot.RowStyles.Cast<RowStyle>().Sum(r=>r.Height)+44;
    }
    async Task ScanSubtitles() {
        if(busy)return;
        if(sources.Items.Count!=1){MessageBox.Show("Choose one assembled movie file to scan its subtitle tracks.");return;}
        string input=sources.Items[0].ToString(),probe=Engine.Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(ff.Text),"ffprobe.exe"));
        busy=true;LockMovieInputs(true);
        try {
            var choices=await Task.Run(()=>Engine.SubtitleTracks(probe,input,CancellationToken.None));
            subtitleTracks.Items.Clear();subtitleTracks.Items.Add(new SubtitleChoice());foreach(var c in choices)subtitleTracks.Items.Add(c);subtitleTracks.SelectedIndex=0;subtitleSource=input;
            status.Text=choices.Count+" subtitle track(s) found. Choose a track to burn into the picture.";
        }catch(Exception ex){MessageBox.Show(ex.Message,"Subtitle scan");}
        finally{busy=false;LockMovieInputs(false);}
    }
    async Task EditBreaks() {
        if(busy)return;
        string[] inputs=sources.Items.Cast<string>().ToArray();
        if(inputs.Length==0){MessageBox.Show("Add movie files first.");return;}
        string encoder=ff.Text,probe=Engine.Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(encoder),"ffprobe.exe"));
        busy=true;LockMovieInputs(true);
        try {
            double[] durations=await Task.Run(()=>inputs.Select(p=>Engine.Duration(probe,p,Path.GetDirectoryName(p),CancellationToken.None,s=>{})).ToArray());
            string identity=string.Join("\n",inputs);
            using(var editor=new BreakEditor(encoder,inputs,durations,identity==breakSources?breakTimes:new List<double>(),lowVision.Checked))if(editor.ShowDialog(this)==DialogResult.OK) {
                breakTimes=editor.Breaks;breakSources=identity;manualBreaks.Checked=breakTimes.Count>0;status.Text=breakTimes.Count+" manual disc break(s) selected.";
            }
        }catch(Exception ex){MessageBox.Show(ex.Message,"Disc break preview");}
        finally{busy=false;LockMovieInputs(false);}
    }
    void LockMovieInputs(bool locked){config.Enabled=true;actions.Enabled=true;create.Enabled=true;create.Text=locked?"Working…":"Create Video CDs";foreach(var box in new[]{ff,vcd,burn,destination})box.ReadOnly=locked;}
    sealed class BusyInputFilter : IMessageFilter {
        readonly Workshop owner;public BusyInputFilter(Workshop form){owner=form;}
        public bool PreFilterMessage(ref Message m){
            if(!owner.busy)return false;Control c=Control.FromChildHandle(m.HWnd);if(c==null)return false;
            bool settings=false;for(Control p=c;p!=null;p=p.Parent)if(p==owner.config||p==owner.actions){settings=true;break;}
            if(!settings||c==owner.ff||c==owner.vcd||c==owner.burn||c==owner.destination)return false;
            if(m.Msg==0x100&&m.WParam.ToInt32()==9)return false;
            return m.Msg==0x100||m.Msg==0x101||m.Msg==0x102||m.Msg==0x104||m.Msg==0x105||m.Msg==0x106||m.Msg==0x201||m.Msg==0x202||m.Msg==0x203||m.Msg==0x204||m.Msg==0x205||m.Msg==0x20A;
        }
    }
}
