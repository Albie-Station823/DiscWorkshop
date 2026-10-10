using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

class UpdateTest {
    static void Check(bool yes,string text){if(!yes)throw new Exception(text);}
    static void Pump(int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(20);}}
    static void Space(BreakEditor editor){var message=new Message();typeof(BreakEditor).GetMethod("ProcessCmdKey",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,new object[]{message,Keys.Space});}
    [STAThread]static int Main() {
        string root=AppDomain.CurrentDomain.BaseDirectory,work=Path.Combine(root,"test-results","update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        var lines=new List<string>();Action<string> log=s=>{lock(lines)lines.Add(s);};
        try {
            var token=CancellationToken.None;string ff=Engine.Find("ffmpeg.exe",@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe"),probe=Engine.Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(ff),"ffprobe.exe"));
            Engine.Run(ff,new[]{"-hide_banner","-nostdin","-f","lavfi","-i","testsrc2=size=640x360:rate=30","-f","lavfi","-i","sine=frequency=440","-t","8","-c:v","libx264","-c:a","aac","source.mp4"},work,token,log);
            foreach(string format in new[]{"XVCD","SVCD"}) {
                var options=EncodingOptions.Defaults(true,format);options.Width=352;options.Height=240;options.FrameRate="25";options.VideoRate=2800;options.MaximumRate=3500;options.AudioRate=128;options.SampleRate=48000;options.Channels=1;options.Gop=12;
                string name=format+".mpg";
                Engine.Run(ff,Engine.ConvertArgs(Path.Combine(work,"source.mp4"),name,true,false,0,false,options.VideoRate,false,format,options),work,token,log);
                string details="";Engine.Run(probe,new[]{"-v","error","-show_entries","stream=codec_name,width,height,r_frame_rate,sample_rate,channels","-of","default=noprint_wrappers=1",name},work,token,s=>details+=s+"\n");
                Check(details.Contains("codec_name="+(format=="SVCD"?"mpeg2video":"mpeg1video"))&&details.Contains("width=352")&&details.Contains("height=240")&&details.Contains("r_frame_rate=25/1")&&details.Contains("sample_rate=48000")&&details.Contains("channels=1"),"Custom parameters failed: "+details);
                Engine.Run(ff,new[]{"-hide_banner","-nostdin","-v","error","-xerror","-i",name,"-f","null","-"},work,token,log);
                Check(Engine.MaximumRate(3600,80,format,options)>Engine.MaximumRate(3600,80,format),"Custom audio was not budgeted");
                Engine.Author(Engine.Find("vcdimager.exe"),format,work,token,log,format);
            }
            Application.EnableVisualStyles();
            foreach(int profile in new[]{2,1})foreach(bool one in new[]{true,false})using(var form=new Workshop()) {
                Func<string,object> field=name=>typeof(Workshop).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                typeof(Workshop).GetField("settings",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,Path.Combine(work,"tools.txt"));
                ((ListBox)field("sources")).Items.Add(Path.Combine(work,"source.mp4"));((TextBox)field("destination")).Text=Path.Combine(work,"ui-"+profile+"-"+one);
                ((ComboBox)field("outputFormat")).SelectedIndex=profile;
                ((CheckBox)field(one?"oneCD":"maxRate")).Checked=true;((NumericUpDown)field("discCount")).Value=2;
                form.Show();var task=(System.Threading.Tasks.Task)typeof(Workshop).GetMethod("Create",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
                var deadline=DateTime.UtcNow.AddSeconds(60);while(!task.IsCompleted&&DateTime.UtcNow<deadline)Pump(40);
                Check(task.IsCompleted,"UI conversion timed out");string job=(string)field("job");
                Check(File.Exists(Path.Combine(job,"COMPLETE.txt")),"UI conversion failed: "+((Label)field("status")).Text);
                Check(Directory.GetFiles(job,"disc-*.cue").Length==(one?1:2),"UI disc count failed");
                string info="";Engine.Run(probe,new[]{"-v","error","-select_streams","v:0","-show_entries","stream=codec_name","-of","default=noprint_wrappers=1",Path.Combine(job,"disc-00.mpg")},work,token,s=>info+=s);
                Check(info.Contains(profile==1?"mpeg2video":"mpeg1video"),"UI profile selected wrong MPEG codec");form.Close();
            }
            using(var time=new MovieTimeInput()){time.Value=3723.456;Check(Math.Abs(time.Value-3723.456)<0.001&&MovieTimeInput.Stamp(time.Value)=="01:02:03.456","HH:MM:SS input failed");}
            using(var editor=new BreakEditor(ff,new[]{Path.Combine(work,"source.mp4")},new[]{8.0},new List<double>(),true)) {
                editor.Show();Pump(1500);
                var playback=(PreviewPlayback)typeof(BreakEditor).GetField("playback",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                Space(editor);Pump(3000);Check(playback.Active&&playback.Position>1,"Space did not play: "+playback.Position+" "+playback.ErrorText);
                Space(editor);Pump(500);double paused=playback.Position;Pump(700);Check(playback.Paused&&Math.Abs(playback.Position-paused)<0.12,"Space did not pause: "+paused+" -> "+playback.Position);
                Space(editor);Pump(700);Check(!playback.Paused&&playback.Position>paused+0.2,"Space did not resume");
                Space(editor);
                var position=(MovieTimeInput)typeof(BreakEditor).GetField("position",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);position.Value=5;
                typeof(BreakEditor).GetMethod("Seek",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);Pump(500);Space(editor);Pump(1500);
                Check(playback.Position>=5.5&&playback.Position<8,"Seeked playback clock failed: "+playback.Position);
                Space(editor);using(var image=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(work,"preview.png"));}
                editor.Close();Check(!playback.Active,"Playback survived closing editor");
            }
            using(var editor=new BreakEditor(ff,new[]{Path.Combine(work,"source.mp4"),Path.Combine(work,"source.mp4")},new[]{8.0,8.0},new List<double>(),false)) {
                editor.Show();Pump(500);var position=(MovieTimeInput)typeof(BreakEditor).GetField("position",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
                position.Value=7.2;typeof(BreakEditor).GetMethod("Seek",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);Space(editor);Pump(3500);
                var playback=(PreviewPlayback)typeof(BreakEditor).GetField("playback",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);Check(playback.Active&&playback.Position>8.5,"Playback did not advance to the next source clip: "+playback.Position);editor.Close();
            }
            File.WriteAllText(Path.Combine(work,"PASS.txt"),"PASS: HH:MM:SS entry, space play/pause/resume, seek clock and process cleanup; custom MPEG-1/MPEG-2 dimensions, frame rate, audio rate/channels, elevated video bitrate, strict decode and authoring.");Console.WriteLine(work);return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(work,"FAIL.txt"),ex.ToString());Console.Error.WriteLine(ex);Console.WriteLine(work);return 1;}
        finally{File.WriteAllLines(Path.Combine(work,"test.log"),lines);}
    }
}
