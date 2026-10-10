using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

class RevisionTest {
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}}
    static void Key(BreakEditor editor,Keys key){typeof(BreakEditor).GetMethod("ProcessCmdKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,new object[]{new Message(),key});}
    static double Gray(string ff,string input,double time,string work,string name,Action<string> log){Engine.Run(ff,new[]{"-hide_banner","-nostdin","-i",input,"-ss",Engine.Number(time),"-frames:v","1","-pix_fmt","gray","-f","rawvideo",name},work,CancellationToken.None,log);return File.ReadAllBytes(Path.Combine(work,name)).Average(x=>(double)x);}
    static double Rms(string ff,string input,double time,string work,string name,Action<string> log){Engine.Run(ff,new[]{"-hide_banner","-nostdin","-ss",Engine.Number(time),"-i",input,"-t","0.08","-vn","-ac","1","-ar","44100","-f","s16le",name},work,CancellationToken.None,log);byte[] bytes=File.ReadAllBytes(Path.Combine(work,name));return Math.Sqrt(Enumerable.Range(0,bytes.Length/2).Average(i=>{double x=BitConverter.ToInt16(bytes,i*2);return x*x;}));}
    static int Frames(string probe,string file,string work,Action<string> log){int count=0;Engine.Run(probe,new[]{"-v","error","-select_streams","v:0","-count_frames","-show_entries","stream=nb_read_frames","-of","default=noprint_wrappers=1:nokey=1",file},work,CancellationToken.None,line=>{int n;if(int.TryParse(line,out n))count=n;else log(line);});return count;}
    [STAThread]static int Main(){
        string root=AppDomain.CurrentDomain.BaseDirectory,work=Path.Combine(root,"test-results","revision-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        var lines=new List<string>();Action<string> log=line=>{lock(lines)lines.Add(line);};
        try {
            string ff=Engine.Find("ffmpeg.exe",@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe"),probe=Engine.Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(ff),"ffprobe.exe"));var token=CancellationToken.None;
            Environment.SetEnvironmentVariable("PATH",Path.GetDirectoryName(ff)+";"+Environment.GetEnvironmentVariable("PATH"));
            Engine.Run(ff,new[]{"-hide_banner","-nostdin","-f","lavfi","-i","color=white:size=640x360:rate=30","-f","lavfi","-i","sine=frequency=440","-t","8","-c:v","ffv1","-c:a","pcm_s16le","source.mkv"},work,token,log);
            foreach(bool pal in new[]{false,true})foreach(string format in new[]{"XVCD","SVCD"}) {
                string dir=Path.Combine(work,format+(pal?"-pal":"-ntsc"));Directory.CreateDirectory(dir);
                Engine.Run(ff,Engine.LosslessArgs(Engine.ConvertArgs(Path.Combine(work,"source.mkv"),"clip.mkv",pal,false,0,false,1800,false,format),pal),dir,token,log);
                File.WriteAllText(Path.Combine(dir,"clips.txt"),"file 'clip.mkv'\n",new UTF8Encoding(false));double duration=Engine.Duration(probe,"clip.mkv",dir,token,log);var cuts=Engine.ManualCuts(new[]{4.0},duration,pal);
                Engine.EncodeVolumes(ff,probe,dir,cuts,duration,pal,format,1800,false,80,token,log,(p,s)=>{},null,1);
                double first=Engine.Duration(probe,"disc-00.mpg",dir,token,log),second=Engine.Duration(probe,"disc-01.mpg",dir,token,log);
                Check(Math.Abs(first+second-duration)<0.25,"Fades changed movie duration");
                Check(Frames(probe,"disc-00.mpg",dir,log)+Frames(probe,"disc-01.mpg",dir,log)==Frames(probe,"clip.mkv",dir,log),"Fades lost or duplicated video frames");
                Check(Gray(ff,"disc-00.mpg",0.2,dir,"first.gray",log)>140,"Unrequested opening fade");
                Check(Gray(ff,"disc-00.mpg",first-0.08,dir,"out.gray",log)<60,"End of first disc did not fade to black");
                Check(Gray(ff,"disc-01.mpg",0,dir,"in.gray",log)<35,"Start of second disc did not fade from black");
                Check(Gray(ff,"disc-01.mpg",second-0.3,dir,"last.gray",log)>140,"Unrequested ending fade");
                Check(Rms(ff,"disc-01.mpg",0,dir,"quiet.pcm",log)<Rms(ff,"disc-01.mpg",1.5,dir,"loud.pcm",log)*0.2,"Audio did not fade from silence");
                foreach(string file in Directory.GetFiles(dir,"disc-*.mpg")){Engine.Run(ff,new[]{"-hide_banner","-nostdin","-v","error","-xerror","-i",file,"-f","null","-"},dir,token,log);Engine.Author(Engine.Find("vcdimager.exe"),Path.GetFileNameWithoutExtension(file),dir,token,log,format);}
                string padded=Path.Combine(dir,"black");Directory.CreateDirectory(padded);File.Copy(Path.Combine(dir,"clip.mkv"),Path.Combine(padded,"clip.mkv"));File.Copy(Path.Combine(dir,"clips.txt"),Path.Combine(padded,"clips.txt"));
                Engine.EncodeVolumes(ff,probe,padded,cuts,duration,pal,format,1800,true,80,token,log,(p,s)=>{},null,1,4,4);
                foreach(string file in Directory.GetFiles(padded,"disc-*.mpg")){
                    double length=Engine.Duration(probe,file,padded,token,log);Check(Math.Abs(length-12)<0.15,"Black padding duration incorrect");
                    string name=Path.GetFileNameWithoutExtension(file);
                    Check(Gray(ff,file,2,padded,name+"-before.gray",log)<10,"Opening padding was not black");
                    Check(Gray(ff,file,10,padded,name+"-after.gray",log)<10,"Ending padding was not black");
                    Check(Gray(ff,file,6,padded,name+"-movie.gray",log)>140,"Padding removed the movie");
                    Check(Rms(ff,file,2,padded,name+"-before.pcm",log)<10&&Rms(ff,file,10,padded,name+"-after.pcm",log)<10,"Black screens were not silent");
                    Engine.Run(ff,new[]{"-hide_banner","-nostdin","-v","error","-xerror","-i",file,"-f","null","-"},padded,token,log);Engine.Author(Engine.Find("vcdimager.exe"),name,padded,token,log,format);
                }
            }
            Application.EnableVisualStyles();
            using(var editor=new BreakEditor(ff,new[]{Path.Combine(work,"source.mkv")},new[]{8.0},new List<double>(),false)) {
                editor.Show();Pump(700);var position=(MovieTimeInput)typeof(BreakEditor).GetField("position",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                Key(editor,Keys.Right);Pump(700);Check(Math.Abs(position.Value-1.0/30)<0.001,"Right did not move exactly one source frame: "+position.Value);
                Key(editor,Keys.Right);Pump(500);Check(Math.Abs(position.Value-2.0/30)<0.001,"Second frame step skipped a frame: "+position.Value);
                Key(editor,Keys.Left);Pump(500);Check(Math.Abs(position.Value-1.0/30)<0.001,"Left did not move back exactly one source frame: "+position.Value);
                position.Value=6;typeof(BreakEditor).GetMethod("Seek",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);Pump(700);
                Key(editor,Keys.Left);Pump(700);Check(Math.Abs(position.Value-179.0/30)<0.002,"Late seek stepped back into stale early frames: "+position.Value);
                position.Value=1;typeof(BreakEditor).GetMethod("Seek",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);Pump(700);
                Key(editor,Keys.Right);Pump(700);Check(Math.Abs(position.Value-31.0/30)<0.002,"Backward seek stepped into stale late frames: "+position.Value);
                var playback=(PreviewPlayback)typeof(BreakEditor).GetField("playback",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                Key(editor,Keys.Down);Check(playback.VolumeSteps==-1,"Down did not lower preview volume");Key(editor,Keys.Up);Check(playback.VolumeSteps==0,"Up did not raise preview volume");editor.Close();
            }
            Engine.Run(ff,new[]{"-hide_banner","-nostdin","-i","source.mkv","-vf","select='not(eq(mod(n,3),1))'","-fps_mode","vfr","-c:v","ffv1","-c:a","copy","vfr.mkv"},work,token,log);
            using(var editor=new BreakEditor(ff,new[]{Path.Combine(work,"vfr.mkv")},new[]{8.0},new List<double>(),false)) {
                editor.Show();Pump(500);var position=(MovieTimeInput)typeof(BreakEditor).GetField("position",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                Key(editor,Keys.Right);Pump(600);Check(Math.Abs(position.Value-0.067)<0.001,"VFR stepping invented a missing frame: "+position.Value);
                Key(editor,Keys.Right);Pump(400);Check(Math.Abs(position.Value-0.1)<0.001,"VFR next frame failed: "+position.Value);Key(editor,Keys.Left);Pump(400);Check(Math.Abs(position.Value-0.067)<0.001,"VFR previous frame failed: "+position.Value);editor.Close();
            }
            // A real-time wrapper keeps a real FFmpeg conversion active long enough to inspect style changes.
            string wrapper=Path.Combine(root,"RealtimeEncoder.exe");Check(File.Exists(wrapper),"Build RealtimeEncoder before this test");
            using(var form=new Workshop()) {
                Func<string,object> field=name=>typeof(Workshop).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                typeof(Workshop).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,Path.Combine(work,"tools.txt"));
                ((ListBox)field("sources")).Items.Add(Path.Combine(work,"source.mkv"));((TextBox)field("destination")).Text=Path.Combine(work,"ui");((TextBox)field("ff")).Text=wrapper;((CheckBox)field("author")).Checked=false;
                Check(((NumericUpDown)field("beforeSeconds")).Value==4&&((NumericUpDown)field("afterSeconds")).Value==4,"Black-screen defaults were not four seconds");
                ((CheckBox)field("blackBefore")).Checked=true;((CheckBox)field("blackAfter")).Checked=true;((NumericUpDown)field("beforeSeconds")).Value=1.2m;((NumericUpDown)field("afterSeconds")).Value=2.4m;
                typeof(Workshop).GetField("visionSettings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,Path.Combine(work,"accessibility.txt"));
                var mode=(CheckBox)field("lowVision");mode.Checked=true;form.Show();Pump(100);
                var task=(System.Threading.Tasks.Task)typeof(Workshop).GetMethod("Create",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);Pump(1000);
                Check(!task.IsCompleted&&mode.Enabled&&mode.Parent.Enabled,"Accessibility toggle was disabled during conversion");
                var config=(Panel)field("config");Check(config.Enabled&&((TextBox)field("ff")).ReadOnly,"Settings were dimmed or unlocked during conversion");
                Check(((Label)field("status")).ForeColor==Color.White&&!((Label)field("status")).AutoSize,"Low vision progress label is unreadable or unstable");
                mode.Checked=false;Pump(200);Check(form.Font.Size==10&&!task.IsCompleted,"Could not switch off low vision during conversion");mode.Checked=true;Pump(200);Check(form.Font.Size==15&&!task.IsCompleted,"Could not restore low vision during conversion");
                var panel=(Panel)field("scroll");var movie=(TableLayoutPanel)field("movieRoot");Check(movie.Width<=panel.ClientSize.Width+5,"Conversion layout overflowed horizontally");
                var bar=(Control)field("accessibilityBar");Check(panel.Top>=bar.Bottom&&bar.Height>=mode.Height,"Accessibility toggle was covered by the scrolling settings");
                panel.AutoScrollPosition=new Point(0,Math.Max(0,movie.Height-panel.ClientSize.Height));Pump(100);
                Check(((TextBox)field("log")).Height<=180&&!((TextBox)field("log")).AutoSize,"Conversion log expanded and broke the layout");
                using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(work,"converting-low-vision.png"));}
                var until=DateTime.UtcNow.AddSeconds(45);while(!task.IsCompleted&&DateTime.UtcNow<until)Pump(50);
                Check(task.IsCompleted&&File.Exists(Path.Combine((string)field("job"),"COMPLETE.txt")),"Conversion did not finish after accessibility toggles");form.Close();
                string uiJob=(string)field("job");Check(Math.Abs(Engine.Duration(probe,"disc-00.mpg",uiJob,token,log)-11.6)<0.15,"Configured black durations were not applied through the VCD interface");
            }
            File.WriteAllText(Path.Combine(work,"PASS.txt"),"PASS: source-frame Left/Right, preview-volume Up/Down, PAL/NTSC MPEG-1/MPEG-2 picture/audio fades, exact frame retention, strict decode/authoring, low-vision readibility and live toggles during real conversion.");Console.WriteLine(work);return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(work,"FAIL.txt"),ex.ToString());Console.Error.WriteLine(ex);Console.WriteLine(work);return 1;}
        finally{File.WriteAllLines(Path.Combine(work,"test.log"),lines);}
    }
}

class RealtimeEncoder {
    static int Main(string[] args){try{string ff=Engine.Find("ffmpeg.exe",@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe");Engine.Run(ff,new[]{"-re"}.Concat(args).ToArray(),Environment.CurrentDirectory,CancellationToken.None,Console.WriteLine);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
