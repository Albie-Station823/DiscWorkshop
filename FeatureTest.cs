using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

class FeatureTest {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    [STAThread] static int Main() {
        string work=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results","features-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        var lines=new List<string>();Action<string> log=s=>{lock(lines)lines.Add(s);};
        try {
            string ff=Engine.Find("ffmpeg.exe",@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe"),probe=Engine.Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(ff),"ffprobe.exe"));var token=CancellationToken.None;
            File.WriteAllText(Path.Combine(work,"a.srt"),"1\n00:00:01,000 --> 00:00:03,000\nFIRST CAPTION\n",new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(work,"b.srt"),"1\n00:00:02,000 --> 00:00:04,000\nSECOND CAPTION\n",new UTF8Encoding(false));
            Engine.Run(ff,new[]{"-hide_banner","-nostdin","-f","lavfi","-i","color=black:size=640x360:rate=30","-f","lavfi","-i","sine=frequency=440","-i","a.srt","-i","b.srt","-map","0:v","-map","1:a","-map","2:s","-map","3:s","-metadata:s:s:0","language=eng","-metadata:s:s:1","language=spa","-t","40","-c:v","ffv1","-c:a","pcm_s16le","-c:s","srt","source.mkv"},work,token,log);
            var choices=Engine.SubtitleTracks(probe,Path.Combine(work,"source.mkv"),token);
            Check(choices.Count==2&&choices[1].Stream==1&&choices[1].Description.Contains("spa"),"Embedded subtitle selection failed");
            var cuts=Engine.ManualCuts(new[]{3.123},8,false);Check(Math.Abs(cuts[0].Time-3.123)<0.017&&cuts[0].Manual,"Frame aligned manual cut failed");
            bool rejected=false;try{Engine.ManualCuts(new[]{0.1},8,false);}catch(Exception){rejected=true;}Check(rejected,"Unsafe manual cut accepted");
            Check(Engine.MaximumRate(3600,80,"XVCD")>Engine.MaximumRate(5400,80,"XVCD"),"Per disc bitrate calculation failed");
            foreach(bool pal in new[]{false,true})foreach(string format in new[]{"XVCD","SVCD"}) {
                string dir=Path.Combine(work,format+(pal?"-pal":"-ntsc"));Directory.CreateDirectory(dir);
                var choice=pal?new SubtitleChoice {FilePath=Path.Combine(work,"a.srt")}:choices[1];
                string source=Path.Combine(work,"source.mkv");string prepared=Engine.PrepareSubtitle(ff,source,choice,dir,0,token,log);
                var args=Engine.WithSubtitles(Engine.ConvertArgs(source,"clip.mkv",pal,false,0,true,1800,false,format),choice,prepared);
                Engine.Run(ff,Engine.LosslessArgs(args,pal),dir,token,log);
                // A black source must have visible white burned captions at two seconds.
                Engine.Run(ff,new[]{"-hide_banner","-nostdin","-ss","2.1","-i","clip.mkv","-frames:v","1","-pix_fmt","gray","-f","rawvideo","caption.gray"},dir,token,log);
                Check(File.ReadAllBytes(Path.Combine(dir,"caption.gray")).Count(b=>b>100)>50,"Subtitles were not visibly burned in");
                File.WriteAllText(Path.Combine(dir,"clips.txt"),"file 'clip.mkv'\n",new UTF8Encoding(false));
                double duration=Engine.Duration(probe,"clip.mkv",dir,token,log);
                var selected=Engine.ManualCuts(new[]{3.123},duration,pal);
                // A tiny capacity budget forces the longer second volume to use a lower rate.
                Engine.EncodeVolumes(ff,probe,dir,selected,duration,pal,format,1800,true,1,token,log,(p,s)=>{});
                var rates=File.ReadAllLines(Path.Combine(dir,"DISC-BITRATES.csv")).Skip(1).Select(s=>int.Parse(s.Split(',')[3])).ToArray();
                Check(rates[0]>rates[1],"Distinct per-volume rates were not applied");
                Check(Directory.GetFiles(dir,"disc-*.mpg").Length==2,"Manual encode disc count failed");
                foreach(string disc in Directory.GetFiles(dir,"disc-*.mpg")) {
                    Engine.Run(ff,new[]{"-hide_banner","-nostdin","-v","error","-xerror","-i",disc,"-f","null","-"},dir,token,log);
                    Engine.Author(Engine.Find("vcdimager.exe"),Path.GetFileNameWithoutExtension(disc),dir,token,log,format);
                }
            }
            Application.EnableVisualStyles();
            using(var form=new Workshop()) {
                form.Show();Application.DoEvents();
                var mode=(CheckBox)typeof(Workshop).GetField("lowVision",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                bool original=mode.Checked;mode.Checked=true;Application.DoEvents();
                Check(form.Font.Size==15,"Low vision font failed");
                var config=(Panel)typeof(Workshop).GetField("config",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                Check(config.Height>500,"Scrollable settings layout failed");
                var rows=(FlowLayoutPanel)typeof(Workshop).GetField("optionRows",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                File.WriteAllLines(Path.Combine(work,"layout.txt"),new[]{"form "+form.Size+" config "+config.Bounds+" options "+rows.Bounds+" preferred "+rows.PreferredSize}.Concat(rows.Controls.Cast<Control>().Select(c=>c.Bounds+" preferred "+c.PreferredSize+" "+string.Join(";",c.Controls.Cast<Control>().Select(x=>x.Text)))));
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(work,"low-vision.png"));}
                var scroll=(Panel)typeof(Workshop).GetField("scroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                scroll.AutoScrollPosition=new Point(0,650);Application.DoEvents();
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(work,"low-vision-options.png"));}
                mode.Checked=false;Application.DoEvents();Check(form.Font.Size==10,"Low vision off failed");
                using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(work,"standard.png"));}
                mode.Checked=original;form.Close();
            }
            using(var editor=new BreakEditor(ff,new[]{Path.Combine(work,"source.mkv")},new[]{8.0},new List<double>{3.123},true)) {
                editor.Show();var until=DateTime.UtcNow.AddSeconds(3);while(DateTime.UtcNow<until){Application.DoEvents();Thread.Sleep(20);}
                var picture=(PictureBox)typeof(BreakEditor).GetField("picture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                Check(picture.Image!=null,"Source preview frame failed");
                using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(work,"break-preview.png"));}
                editor.Close();
            }
            File.WriteAllText(Path.Combine(work,"PASS.txt"),"PASS: two embedded subtitle tracks, external captions, visible caption burn-in, frame aligned manual breaks, per-disc bitrate, NTSC/PAL XVCD/SVCD decode and authoring, accessibility toggle and layout.");
            Console.WriteLine(work);return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(work,"FAIL.txt"),ex.ToString());Console.Error.WriteLine(ex);Console.WriteLine(work);return 1;}
        finally{File.WriteAllLines(Path.Combine(work,"test.log"),lines);}
    }
}
