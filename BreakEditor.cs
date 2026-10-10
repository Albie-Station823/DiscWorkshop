using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;


sealed class BreakEditor : Form {
    readonly string ff; readonly string[] inputs; readonly double[] durations; readonly double total;
    PictureBox picture=new PictureBox();TrackBar scrub=new TrackBar();MovieTimeInput position=new MovieTimeInput();
    PreviewPlayback playback;Button play=new Button();Label timeDisplay=new Label();System.Windows.Forms.Timer playbackTick=new System.Windows.Forms.Timer();bool wasPlaying;double playbackEnd;
    ListBox list=new ListBox();Label state=new Label();
    CancellationTokenSource preview;bool syncing;int generation;
    public List<double> Breaks {get {return list.Items.Cast<double>().OrderBy(x=>x).ToList();}}
    public BreakEditor(string encoder,string[] files,double[] lengths,List<double> existing,bool contrast) {
        AutoScaleMode=AutoScaleMode.None;
        ff=encoder;inputs=files;durations=lengths;total=durations.Sum();
        Text="Choose disc breaks • source preview";Size=new Size(1000,850);MinimumSize=new Size(760,640);
        StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",contrast?15:10,contrast?FontStyle.Bold:FontStyle.Regular);
        BackColor=contrast?Color.Black:Color.White;ForeColor=contrast?Color.White:Color.Black;
        var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(16)};
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        foreach(int h in new[]{60,155,110,75,60})root.RowStyles.Add(new RowStyle(SizeType.Absolute,h));Controls.Add(root);
        picture.Dock=DockStyle.Fill;picture.SizeMode=PictureBoxSizeMode.Zoom;picture.BackColor=Color.Black;picture.AccessibleName="Source frame preview";root.Controls.Add(picture);
        scrub.Dock=DockStyle.Fill;scrub.Maximum=1000000;scrub.TickStyle=TickStyle.None;scrub.AccessibleName="Scrub movie timeline";root.Controls.Add(scrub);
        var controls=new FlowLayoutPanel {Dock=DockStyle.Fill};root.Controls.Add(controls);
        controls.Controls.Add(new Label {Text="Movie time (HH:MM:SS)",AutoSize=true});
        position.AccessibleName="Exact break time in hours minutes seconds";controls.Controls.Add(position);
        timeDisplay.AutoSize=true;timeDisplay.Text=MovieTimeInput.Stamp(0)+" / "+MovieTimeInput.Stamp(total);controls.Controls.Add(timeDisplay);controls.SetFlowBreak(timeDisplay,true);
        play.Text="Play / pause (Space)";play.AutoSize=true;play.Click+=(s,e)=>TogglePlayback();controls.Controls.Add(play);
        var add=new Button {Text="Add break here",AutoSize=true};controls.Controls.Add(add);
        var remove=new Button {Text="Remove selected break",AutoSize=true};controls.Controls.Add(remove);
        list.Dock=DockStyle.Fill;list.AccessibleName="Selected disc break times";list.FormattingEnabled=true;list.Format+=(s,e)=>e.Value=MovieTimeInput.Stamp((double)e.ListItem);
        foreach(double t in existing)list.Items.Add(t);root.Controls.Add(list);
        state.Dock=DockStyle.Fill;state.Text="Space: play/pause. Enter HH:MM:SS or scrub the timeline, then add breaks.";root.Controls.Add(state);
        var footer=new FlowLayoutPanel {Dock=DockStyle.Fill};root.Controls.Add(footer);
        var save=new Button {Text="Save breaks",AutoSize=true};save.Click+=(s,e)=>{DialogResult=DialogResult.OK;Close();};footer.Controls.Add(save);
        var cancel=new Button {Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};footer.Controls.Add(cancel);CancelButton=cancel;
        if(contrast)foreach(Control container in new Control[]{controls,footer})foreach(Control c in container.Controls){c.BackColor=Color.Black;c.ForeColor=c is Button?Color.Yellow:Color.White;if(c is Button)((Button)c).FlatStyle=FlatStyle.Flat;}
        add.Click+=(s,e)=> {
            if(playback.Active){playback.Pause();UpdatePlaybackTime();}
            double t=position.Value;
            if(t<1||total-t<1||Breaks.Any(x=>Math.Abs(x-t)<1)){state.Text="Leave at least one second on each side of every break.";return;}
            var values=Breaks;values.Add(t);list.Items.Clear();foreach(double x in values.OrderBy(x=>x))list.Items.Add(x);state.Text="Break added at "+MovieTimeInput.Stamp(t)+".";
        };
        remove.Click+=(s,e)=>{if(list.SelectedIndex>=0)list.Items.RemoveAt(list.SelectedIndex);};
        list.SelectedIndexChanged+=(s,e)=>{if(list.SelectedItem!=null){position.Value=(double)list.SelectedItem;Seek();}};
        scrub.ValueChanged+=(s,e)=> {if(syncing)return;syncing=true;position.Value=total*scrub.Value/1000000;syncing=false;Seek();};
        position.ValueChanged+=(s,e)=> {if(syncing)return;if(position.Value>total)position.Value=total;syncing=true;scrub.Value=Math.Min(scrub.Maximum,(int)(position.Value/total*1000000));syncing=false;Seek();};
        Shown+=async(s,e)=>await RenderFrame();
        playback=new PreviewPlayback(ff,picture);playbackTick.Interval=80;playbackTick.Tick+=(s,e)=> {
            if(playback.Active){playback.Attach();UpdatePlaybackTime();wasPlaying=true;}
            else if(wasPlaying){wasPlaying=false;if(playbackEnd<total-0.1&&string.IsNullOrEmpty(playback.ErrorText)){position.Value=playbackEnd;StartPlayback();return;}state.Text=string.IsNullOrEmpty(playback.ErrorText)?"Playback ended. Space starts playback from the current time.":"Playback error: "+playback.ErrorText;playback.Stop();Schedule();}
        };playbackTick.Start();

        FormClosing+=(s,e)=>{playbackTick.Stop();playback.Stop();generation++;if(preview!=null)preview.Cancel();};
        FormClosed+=(s,e)=>{playbackTick.Dispose();playback.Dispose();if(picture.Image!=null)picture.Image.Dispose();};
    }
    async void Schedule(){
        int version=++generation;if(preview!=null)preview.Cancel();
        // Redraw only the latest seek; do not depend on a queued WinForms timer tick.
        await Task.Delay(250);
        if(version==generation&&!IsDisposed&&!playback.Active)await RenderFrame();
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData){
        if(keyData==Keys.Space){if((msg.LParam.ToInt64()&(1L<<30))==0)TogglePlayback();return true;}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    void TogglePlayback(){try{if(playback.Active)playback.Toggle();else StartPlayback();state.Text=playback.Paused?"Paused • Space resumes playback.":"Playing • Space pauses playback.";}catch(Exception ex){state.Text=ex.Message;}}
    void StartPlayback(){generation++;if(preview!=null)preview.Cancel();double global=Math.Min(position.Value,Math.Max(0,total-0.1)),local=global;int clip=0;while(clip<durations.Length-1&&local>=durations[clip]){local-=durations[clip];clip++;}playbackEnd=durations.Take(clip+1).Sum();playback.Start(inputs[clip],local,global);wasPlaying=true;}
    void Seek(){timeDisplay.Text=MovieTimeInput.Stamp(position.Value)+" / "+MovieTimeInput.Stamp(total);playback.Stop();wasPlaying=false;Schedule();}
    void UpdatePlaybackTime(){syncing=true;position.Value=Math.Min(total,playback.Position);scrub.Value=Math.Min(scrub.Maximum,(int)(position.Value/total*1000000));syncing=false;timeDisplay.Text=MovieTimeInput.Stamp(position.Value)+" / "+MovieTimeInput.Stamp(total);}
    async Task RenderFrame() {
        int version=++generation;double t=Math.Min(position.Value,Math.Max(0,total-0.1));int clip=0;
        while(clip<durations.Length-1&&t>=durations[clip]){t-=durations[clip];clip++;}
        double clipStart=durations.Take(clip).Sum();t=Math.Max(0,t-0.0005);double seek=Math.Max(0,t-2),trim=t-seek;double decodedPts=-1;
        var cancel=new CancellationTokenSource();preview=cancel;
        string folder=Path.Combine(Path.GetTempPath(),"DiscWorkshop-preview-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            await Task.Run(()=>Engine.Run(ff,new[]{"-hide_banner","-nostdin","-v","info","-ss",Engine.Number(seek),"-i",inputs[clip],"-map","0:v:0","-frames:v","1","-vf","trim=start="+Engine.Number(trim)+",setpts=PTS-"+Engine.Number(trim)+"/TB,showinfo,scale=800:450:force_original_aspect_ratio=decrease","-y","preview.jpg"},folder,cancel.Token,s=>{var match=System.Text.RegularExpressions.Regex.Match(s,@"n:\s*0\s+pts:\s*-?\d+\s+pts_time:([\d.eE+-]+)");double pts;if(match.Success&&double.TryParse(match.Groups[1].Value,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out pts))decodedPts=pts;}));
            if(version!=generation||IsDisposed)return;
            using(var loaded=Image.FromFile(Path.Combine(folder,"preview.jpg"))) {var old=picture.Image;picture.Image=new Bitmap(loaded);if(old!=null)old.Dispose();}
            if(decodedPts>=0){syncing=true;position.Value=Math.Min(total,clipStart+t+decodedPts);scrub.Value=Math.Min(scrub.Maximum,(int)(position.Value/total*1000000));syncing=false;timeDisplay.Text=MovieTimeInput.Stamp(position.Value)+" / "+MovieTimeInput.Stamp(total);}
            state.Text="Source frame at "+TimeSpan.FromSeconds((double)position.Value).ToString(@"hh\:mm\:ss\.fff")+" • Add break here to select it.";
        }catch(OperationCanceledException){}catch(Exception ex){if(version==generation&&!IsDisposed)state.Text="Preview unavailable: "+ex.Message;}
        finally {if(preview==cancel)preview=null;cancel.Dispose();try{Directory.Delete(folder,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
