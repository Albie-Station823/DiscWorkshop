using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

public sealed class EncodingOptions {
    public int Width,Height,VideoRate=1800,MaximumRate=2500,AudioRate=224,SampleRate=44100,Channels=2,Gop=15;
    public string FrameRate="30000/1001",Aspect="4:3";
    public double Fps {get {var p=FrameRate.Split('/');return double.Parse(p[0],System.Globalization.CultureInfo.InvariantCulture)/(p.Length==2?double.Parse(p[1],System.Globalization.CultureInfo.InvariantCulture):1);}}
    public EncodingOptions Copy(){return (EncodingOptions)MemberwiseClone();}
    public static EncodingOptions Defaults(bool pal,string format){return new EncodingOptions {Width=format=="SVCD"?480:352,Height=format=="SVCD"?(pal?576:480):(pal?288:240),FrameRate=pal?"25":"30000/1001",VideoRate=format=="SVCD"?2040:1800,Gop=pal?15:18};}
}

sealed class EncodingDialog : Form {
    public EncodingOptions Options;
    readonly Dictionary<string,NumericUpDown> numbers=new Dictionary<string,NumericUpDown>();
    ComboBox fps=new ComboBox(),aspect=new ComboBox();
    public EncodingDialog(EncodingOptions options,bool contrast) {
        Options=options.Copy();AutoScaleMode=AutoScaleMode.None;Text="Advanced XVCD / SVCD parameters";
        Size=new Size(720,750);StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",contrast?15:11,contrast?FontStyle.Bold:FontStyle.Regular);
        BackColor=contrast?Color.Black:Color.White;ForeColor=contrast?Color.White:Color.Black;
        var scroll=new Panel {Dock=DockStyle.Fill,AutoScroll=true};Controls.Add(scroll);
        var rows=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(20)};scroll.Controls.Add(rows);
        rows.Controls.Add(new Label {Text="Custom parameters may require a player that supports nonstandard discs. MPEG-1 / MPEG-2 follows your selected output format.",AutoSize=true,MaximumSize=new Size(620,0)});
        Add(rows,"Picture width",options.Width,16,1920,2);Add(rows,"Picture height",options.Height,16,1080,2);
        Add(rows,"Video bitrate (kb/s)",options.VideoRate,200,15000,50);Add(rows,"Maximum fitting bitrate (kb/s)",options.MaximumRate,200,15000,50);
        Add(rows,"Audio bitrate (kb/s)",options.AudioRate,64,384,32);Add(rows,"Audio channels",options.Channels,1,2,1);
        Add(rows,"GOP length (frames)",options.Gop,1,60,1);
        fps.Items.AddRange(new object[]{"24000/1001","24","25","30000/1001","30","50","60000/1001","60"});fps.SelectedItem=options.FrameRate;fps.DropDownStyle=ComboBoxStyle.DropDownList;
        aspect.Items.AddRange(new object[]{"4:3","16:9"});aspect.SelectedItem=options.Aspect;aspect.DropDownStyle=ComboBoxStyle.DropDownList;
        var sample=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList};sample.Items.AddRange(new object[]{"32000","44100","48000"});sample.SelectedItem=options.SampleRate.ToString();
        Row(rows,"Frame rate",fps);Row(rows,"Display aspect",aspect);Row(rows,"Audio sample rate (Hz)",sample);
        var buttons=new FlowLayoutPanel {AutoSize=true};rows.Controls.Add(buttons);
        var save=new Button {Text="Use parameters",AutoSize=true};buttons.Controls.Add(save);
        var cancel=new Button {Text="Cancel",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(cancel);CancelButton=cancel;
        save.Click+=(s,e)=> {
            if(Value("Picture width")%2!=0||Value("Picture height")%2!=0){MessageBox.Show("Picture width and height must be even numbers.");return;}
            int audio=Value("Audio bitrate (kb/s)");if(!new[]{64,80,96,112,128,160,192,224,256,320,384}.Contains(audio)){MessageBox.Show("Choose an MP2 audio bitrate: 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 or 384 kb/s.");return;}
            Options.Width=Value("Picture width");Options.Height=Value("Picture height");Options.VideoRate=Value("Video bitrate (kb/s)");Options.MaximumRate=Value("Maximum fitting bitrate (kb/s)");Options.AudioRate=audio;Options.Channels=Value("Audio channels");Options.Gop=Value("GOP length (frames)");Options.FrameRate=fps.Text;Options.Aspect=aspect.Text;Options.SampleRate=int.Parse(sample.Text);DialogResult=DialogResult.OK;Close();
        };
        if(contrast)Style(rows);
    }
    int Value(string name){return (int)numbers[name].Value;}
    void Add(Control rows,string label,int value,int min,int max,int step){var n=new NumericUpDown {Minimum=min,Maximum=max,Value=value,Increment=step,Width=135,AccessibleName=label};numbers.Add(label,n);Row(rows,label,n);}
    void Row(Control rows,string label,Control input){var row=new FlowLayoutPanel {AutoSize=true,Margin=new Padding(0,5,0,5)};row.Controls.Add(new Label {Text=label,Width=350,AutoSize=false,Height=40});row.Controls.Add(input);rows.Controls.Add(row);}
    void Style(Control parent){foreach(Control c in parent.Controls){c.BackColor=Color.Black;c.ForeColor=c is Button?Color.Yellow:Color.White;if(c is Button)((Button)c).FlatStyle=FlatStyle.Flat;Style(c);}}
}

public static partial class Engine {
    public static string[] ApplyEncodingOptions(string[] original,EncodingOptions options,string format,int rate) {
        if(options==null)return original;
        var a=original.ToList();int end=a.Count-1;
        a.InsertRange(end,new[]{"-r",options.FrameRate,"-s",options.Width+"x"+options.Height,"-aspect",options.Aspect,"-g",options.Gop.ToString(),"-ac",options.Channels.ToString(),"-ar",options.SampleRate.ToString(),"-b:a",options.AudioRate+"k","-bufsize:v",Math.Max(327680,rate*500).ToString()});
        if(format=="XVCD")a.InsertRange(a.Count-1,new[]{"-muxrate",MuxRate(format,rate,options.AudioRate)});
        return a.ToArray();
    }
}
