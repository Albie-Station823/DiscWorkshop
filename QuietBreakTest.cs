using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
class QuietBreakTest {
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static int Main(){try{
        var keys=Enumerable.Range(0,15000).Select(x=>x*0.5);
        var black=new List<Engine.BlackInterval>{new Engine.BlackInterval(3600,3602),new Engine.BlackInterval(3650,3652)};
        var quiet=new List<Engine.BlackInterval>{new Engine.BlackInterval(3645,3656)};
        var cut=Engine.PlanCuts(7200,keys,black,4200,300,quiet).Single();
        Check(cut.Black&&cut.Quiet&&cut.Time>=3650,"Quiet black must beat nearer loud black");
        cut=Engine.PlanCuts(7200,keys,new List<Engine.BlackInterval>{black[0]},4200,300,quiet).Single();
        Check(cut.Quiet&&!cut.Black,"Quiet passage must beat loud black");
        Check(cut.Time>=3646&&cut.Time<=3655,"Quiet guards missing");
        cut=Engine.PlanCuts(7200,keys,black,4200,300,new List<Engine.BlackInterval>{new Engine.BlackInterval(3599,3601)}).Single();
        Check(!cut.Quiet&&cut.Black,"Short pause falsely accepted");
        cut=Engine.PlanCuts(7200,keys,new List<Engine.BlackInterval>(),4200,300,new List<Engine.BlackInterval>{new Engine.BlackInterval(4500,4520)}).Single();
        Check(!cut.Quiet&&cut.Time==3600,"Capacity/search window ignored");
        string work=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results","quiet-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        string ff=Engine.Find("ffmpeg.exe",@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe");
        string probe=Path.Combine(Path.GetDirectoryName(ff),"ffprobe.exe");
        using(var log=new StreamWriter(Path.Combine(work,"test.log"))){
            Action<string> write=s=>{lock(log){log.WriteLine(s);log.Flush();}};
            var token=CancellationToken.None;
            Engine.Run(ff,new[]{"-hide_banner","-nostdin","-f","lavfi","-i","color=white:size=352x240:rate=30000/1001","-f","lavfi","-i","sine=frequency=440:sample_rate=44100","-vf","drawbox=color=black:t=fill:enable='between(t,9,10)+between(t,12,13)'","-af","volume=0:enable='between(t,10.5,15.5)'","-t","20","-target","ntsc-vcd","-ac","2","clip.mpg"},work,token,write);
            File.WriteAllText(Path.Combine(work,"clips.txt"),"file 'clip.mpg'\n");
            double duration=Engine.Duration(probe,"clip.mpg",work,token,write);
            var cuts=Engine.AnalyzeCuts(ff,probe,"clips.txt",work,duration,token,write,(p,d)=>{},14,4);
            Check(cuts.Count==1&&cuts[0].Quiet&&cuts[0].Black&&cuts[0].Time>12&&cuts[0].Time<13.5,"Real audio/black detection failed");
            Engine.Run(ff,Engine.PlannedOutputArgs("clips.txt",cuts,false),work,token,write);Engine.ValidateCuts(work,cuts);
            foreach(string disc in Directory.GetFiles(work,"disc-*.mpg"))Engine.Run(ff,new[]{"-v","error","-xerror","-i",disc,"-f","null","-"},work,token,write);
            Engine.WritePlan(work,cuts,duration,false,1150,80,write);
        }
        Console.WriteLine("PASS: quiet ranking, guard intervals, short-pause rejection, capacity/window fallback, real FFmpeg scan, split validation and strict decode. "+work);return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
