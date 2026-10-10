using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

public static partial class Engine {
    public static string Number(double value) { return value.ToString("0.######",CultureInfo.InvariantCulture); }
    public static long DiscCapacity(int minutes) { return minutes*60L*75*2352; }
    public static int FitBitrate(double duration,int minutes,int maximum=1150,int audioRate=224) {
        if(duration<=0 || double.IsNaN(duration) || double.IsInfinity(duration)) throw new Exception("Cannot calculate a one-CD bitrate without a valid movie duration.");
        // Mode 2 Form 2 payload, authoring reserve, and 8% margin for mux overhead.
        double payload=(minutes*60L*75-1200)*2324.0*0.92;
        int rate=(int)Math.Min(maximum,Math.Floor(payload*8/duration/1000-audioRate));
        if(rate<200) throw new Exception("This movie is too long for the selected CD at the minimum supported video bitrate (200 kb/s). Use multiple discs or select an 80-minute CD.");
        return rate;
    }
    public static double Duration(string probe,string input,string work,CancellationToken token,Action<string> log) {
        double result=0;
        Run(probe,new [] {"-v","error","-show_entries","format=duration","-of","default=noprint_wrappers=1:nokey=1",input},work,token,line=> {
            double value;
            if(double.TryParse(line,NumberStyles.Float,CultureInfo.InvariantCulture,out value) && value>0 && !double.IsInfinity(value)) result=value;
            else log(line);
        });
        if(result<=0) throw new Exception("Could not read duration for "+input+". Use an assembled movie file with valid timestamps.");
        return result;
    }
    public sealed class BlackInterval { public double Start,End; public BlackInterval(double start,double end) {Start=start;End=end;} }
    public sealed class DiscCut { public double Time; public bool Black,Quiet,Manual; public double Requested; public DiscCut(double time,bool black,bool quiet=false) {Time=time;Black=black;Quiet=quiet;} }
    public static List<DiscCut> PlanCuts(double duration,IEnumerable<double> keyframes,List<BlackInterval> black,double maxSeconds=4200,double window=300,List<BlackInterval> quiet=null,int requestedCount=0) {
        if(duration<=0 || maxSeconds<=2) throw new Exception("Invalid disc planning duration.");
        var keys=keyframes.Distinct().OrderBy(x=>x).ToArray(); var cuts=new List<DiscCut>(); double start=0;
        int remainingCount=requestedCount>0?requestedCount:(int)Math.Ceiling(duration/maxSeconds);
        while(remainingCount>1) {
            int count=remainingCount;
            double target=start+(duration-start)/count;
            double lower=Math.Max(start+1,duration-(count-1)*maxSeconds);
            double upper=Math.Min(start+maxSeconds-0.1,duration-1);
            var available=keys.Where(k=>k>=lower && k<=upper).ToArray();
            if(available.Length==0) throw new Exception("No safe keyframe was found within disc capacity. Source timestamps may be invalid.");
            Func<double,bool> isBlack=k=>black.Any(b=>k>=b.Start+0.05 && k<=b.End-0.05);
            Func<double,bool> isQuiet=k=>quiet!=null && quiet.Any(b=>b.End-b.Start>=3 && k>=b.Start+1 && k<=b.End-1);
            var preferred=available.Where(k=>Math.Abs(k-target)<=window && (isBlack(k)||isQuiet(k)))
                .OrderByDescending(k=>isQuiet(k)?(isBlack(k)?3:2):1).ThenBy(k=>Math.Abs(k-target)).ToArray();
            double cut=preferred.Length>0?preferred[0]:available.OrderBy(k=>Math.Abs(k-target)).First();
            cuts.Add(new DiscCut(cut,isBlack(cut),isQuiet(cut))); start=cut; remainingCount--;
        }
        return cuts;
    }
    public static List<DiscCut> AnalyzeCuts(string ff,string probe,string list,string work,double duration,CancellationToken token,Action<string> log,Action<int,string> progress,double maxSeconds=4200,double window=300,int requestedCount=0) {
        if(duration<=maxSeconds && requestedCount<2) return new List<DiscCut>();
        var black=new List<BlackInterval>(); var quiet=new List<BlackInterval>(); var keys=new List<double>();
        double? silenceStart=null;
        var tracker=new ConversionProgress(duration);
        progress(-1,"Scanning black screens and sustained quiet audio…");
        Run(ff,new [] {"-hide_banner","-nostdin","-nostats","-progress","pipe:1","-f","concat","-safe","1","-i",list,
            "-map","0:v:0","-map","0:a:0","-vf","blackdetect=d=0.35:pic_th=0.98:pix_th=0.10","-af","silencedetect=noise=-35dB:d=3","-f","null","-"},work,token,line=> {
            log(line); tracker.Observe(line,(p,d)=>progress(p,"Scanning black screens and quiet audio • "+d));
            var m=Regex.Match(line,@"black_start:([\d.]+)\s+black_end:([\d.]+)");
            if(m.Success) black.Add(new BlackInterval(double.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture),double.Parse(m.Groups[2].Value,CultureInfo.InvariantCulture)));
            var a=Regex.Match(line,@"silence_start:\s*(-?[\d.]+)");
            if(a.Success)silenceStart=double.Parse(a.Groups[1].Value,CultureInfo.InvariantCulture);
            var b=Regex.Match(line,@"silence_end:\s*(-?[\d.]+)");
            if(b.Success && silenceStart.HasValue){quiet.Add(new BlackInterval(silenceStart.Value,double.Parse(b.Groups[1].Value,CultureInfo.InvariantCulture)));silenceStart=null;}
        });
        if(silenceStart.HasValue)quiet.Add(new BlackInterval(silenceStart.Value,duration));
        progress(-1,"Finding independently decodable disc boundaries…");
        Run(probe,new [] {"-v","error","-f","concat","-safe","1","-i",list,"-select_streams","v:0","-show_packets","-show_entries","packet=pts_time,flags","-of","csv=p=0"},work,token,line=> {
            var fields=line.Split(','); double value;
            if(fields.Length>=2 && double.TryParse(fields[0],NumberStyles.Float,CultureInfo.InvariantCulture,out value)) {if(fields[1].Contains("K")) keys.Add(value);}
            else log(line);
        });
        return PlanCuts(duration,keys,black,maxSeconds,window,quiet,requestedCount);
    }
    public static string MuxRate(string format,int rate,int audioRate=224) {return format=="SVCD"?"0":format=="VCD"?"1411200":Math.Max(1411200,(rate+audioRate)*1050).ToString(CultureInfo.InvariantCulture);}
    public static double VolumeSeconds(string format,int rate,int minutes,int audioRate=224) {
        if(format=="VCD")return 4200;
        return Math.Floor((minutes*60L*75-1200)*2324.0*0.92*8/((rate+audioRate)*1000.0));
    }
    public static string[] PlannedOutputArgs(string list,List<DiscCut> cuts,bool compact,string format="VCD",int rate=1150) {
        var a=new List<string> {"-hide_banner","-nostdin","-n"};
        if(cuts.Count>0) a.Add("-copyts");
        a.AddRange(new [] {"-f","concat","-safe","1","-i",list,"-map","0:v:0","-map","0:a:0","-c","copy"});
        string mux=format=="SVCD"?"svcd":"vcd", prefix=format=="XVCD"?"padded":"disc";
        if(cuts.Count==0) a.AddRange(new [] {"-f",mux,"-packetsize","2324","-muxrate",MuxRate(format,rate),compact&&format=="VCD"?"one-cd-padded.mpg":prefix+"-00.mpg"});
        else a.AddRange(new [] {"-avoid_negative_ts","disabled","-f","segment","-segment_format",mux,"-segment_format_options","packetsize=2324:muxrate="+MuxRate(format,rate),
            "-segment_times",string.Join(",",cuts.Select(c=>Number(c.Time))),"-segment_time_delta","0.05","-segment_list","actual-splits.csv","-reset_timestamps","1",prefix+"-%02d.mpg"});
        return a.ToArray();
    }
    public static void ValidateCuts(string work,List<DiscCut> cuts) {
        if(cuts.Count==0)return;
        var rows=File.ReadAllLines(Path.Combine(work,"actual-splits.csv"));
        if(rows.Length!=cuts.Count+1) throw new Exception("The segmenter produced a different disc count than planned.");
        for(int i=0;i<cuts.Count;i++) {
            var fields=rows[i+1].Split(',');
            double actual=double.Parse(fields[1],CultureInfo.InvariantCulture);
            if(Math.Abs(actual-cuts[i].Time)>0.051) throw new Exception("A disc boundary moved away from the chosen transition. The job is not ready to burn; inspect actual-splits.csv.");
        }
    }
    public static void Author(string imager,string name,string work,CancellationToken token,Action<string> log,string format="VCD") {
        bool truncated=false;
        var args=new List<string> {"--type="+(format=="SVCD"?"svcd":"vcd2"),"--cue-file="+name+".cue","--bin-file="+name+".bin"};
        if(format=="SVCD")args.Add("--update-scan-offsets");args.Add(name+".mpg");
        Run(imager,args.ToArray(),work,token,line=> {
            log(line);
            if(line.Contains("will be ignored") || line.Contains("bad packet") || line.Contains("requires an audio stream")) truncated=true;
        });
        if(truncated) throw new Exception("VCDImager reported an incomplete or invalid movie stream. The image is not ready to burn.");
    }
    public static void ProfileTest(string ff,string work,Action<string> log) {
        string probe=Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(ff),"ffprobe.exe"));
        var token=CancellationToken.None;
        foreach(bool pal in new [] {false,true}) foreach(string format in new [] {"SVCD","XVCD"}) {
            int rate=format=="SVCD"?2040:pal?700:2500;
            string test=Path.Combine(work,format+(pal?"-pal":"-ntsc")); Directory.CreateDirectory(test);
            Run(ff,ConvertArgs(Path.Combine(work,"source.mp4"),"clip.mpg",pal,false,0,true,rate,false,format),test,token,log);
            string details="";
            Run(probe,new [] {"-v","error","-show_entries","stream=codec_name,width,height,sample_rate","-of","default=noprint_wrappers=1","clip.mpg"},test,token,line=>details+=line+"\n");
            if(!details.Contains("codec_name="+(format=="SVCD"?"mpeg2video":"mpeg1video")) || !details.Contains("width="+(format=="SVCD"?480:352)) || !details.Contains("height="+(format=="SVCD"?(pal?576:480):(pal?288:240))) || !details.Contains("sample_rate=44100")) throw new Exception("Profile codec/dimensions/audio failed: "+format);
            File.WriteAllText(Path.Combine(test,"clips.txt"),"file 'clip.mpg'\n");
            double duration=Duration(probe,"clip.mpg",test,token,log);
            var cuts=AnalyzeCuts(ff,probe,"clips.txt",test,duration,token,log,(p,d)=>{},5,1);
            Run(ff,PlannedOutputArgs("clips.txt",cuts,false,format,rate),test,token,log); ValidateCuts(test,cuts);
            if(format=="XVCD")foreach(string p in Directory.GetFiles(test,"padded-*.mpg"))RemoveEmptyPadding(p,Path.Combine(test,Path.GetFileName(p).Replace("padded-","disc-")),token);
            foreach(string disc in Directory.GetFiles(test,"disc-*.mpg")) {
                Author(Find("vcdimager.exe"),Path.GetFileNameWithoutExtension(disc),test,token,log,format);
                // Decode with error escalation so authoring cannot hide damaged media.
                Run(ff,new [] {"-hide_banner","-nostdin","-v","error","-xerror","-i",disc,"-f","null","-"},test,token,log);
            }
            if(VolumeSeconds(format,rate,74)>4200 && rate>1150)throw new Exception("High bitrate was not budgeted for disc capacity");
        }
        string shortcut=Path.Combine(work,"Disc Workshop test.lnk"); Branding.SaveShortcut(shortcut);
        if(!File.Exists(shortcut))throw new Exception("Shortcut creation failed");
        using(var icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath))if(icon==null)throw new Exception("Embedded icon missing");
        if(Branding.DonationUrl!="https://paypal.me/albie")throw new Exception("Wrong donation destination");
        if(Branding.VenmoUrl!="https://account.venmo.com/u/Albie_Burdge")throw new Exception("Wrong Venmo destination");
    }
    public static void RemoveEmptyPadding(string input,string output,CancellationToken token) {
        // FFmpeg's VCD muxer writes all-zero 2324-byte sectors to pad to 1x CD
        // speed. XVCD omits these; all MPEG packs, SCR/PTS and media stay intact.
        using(var src=File.OpenRead(input)) using(var dst=new FileStream(output,FileMode.CreateNew,FileAccess.Write)) {
            if(src.Length%2324!=0) throw new Exception("Unexpected VCD packet alignment; cannot remove padding safely.");
            var packet=new byte[2324];
            while(src.Position<src.Length) {
                token.ThrowIfCancellationRequested(); int read=0;
                while(read<packet.Length) {int n=src.Read(packet,read,packet.Length-read);if(n==0)throw new EndOfStreamException();read+=n;}
                bool empty=true; for(int i=0;i<packet.Length;i++) if(packet[i]!=0) {empty=false;break;}
                if(!empty) dst.Write(packet,0,packet.Length);
            }
        }
    }
    public static void WritePlan(string work,List<DiscCut> cuts,double duration,bool compact,int rate,int minutes,Action<string> log,string format="VCD",double limit=4200,int audioRate=224) {
        var lines=new List<string> {(compact?"ONE-CD ":"")+format+": "+minutes+"-minute media; video "+rate+" kb/s; audio "+audioRate+" kb/s. "+(compact?"Player compatibility varies.":"Quiet audio and black-screen breaks preferred; maximum "+Number(limit/60)+" minutes per volume.")};
        double start=0;
        for(int i=0;i<=cuts.Count;i++) {
            double end=i<cuts.Count?cuts[i].Time:duration;
            lines.Add("Disc "+(i+1)+": "+TimeSpan.FromSeconds(start).ToString(@"hh\:mm\:ss")+" to "+TimeSpan.FromSeconds(end).ToString(@"hh\:mm\:ss")+
                (i==cuts.Count?" • end of film":cuts[i].Manual?" • MANUAL (requested "+Number(cuts[i].Requested)+" seconds; rounded to output frame)":cuts[i].Quiet?(cuts[i].Black?" • black screen with sustained quiet audio":" • sustained quiet audio; no matching black frame"):cuts[i].Black?" • FALLBACK: black screen; sustained quiet audio NOT found":" • FALLBACK: no suitable quiet/black transition; keyframe near balanced boundary")); start=end;
        }
        File.WriteAllLines(Path.Combine(work,"DISC-PLAN.txt"),lines); foreach(var line in lines)log(line);
    }
    public static void PlanningTest(string ff,string work,Action<string> log) {
        string probe=Find("ffprobe.exe",Path.Combine(Path.GetDirectoryName(ff),"ffprobe.exe"));
        var token=CancellationToken.None;
        var keys=Enumerable.Range(0,15000).Select(x=>x*0.5).ToArray();
        var chosen=PlanCuts(7200,keys,new List<BlackInterval> {new BlackInterval(3590,3592)});
        if(chosen.Count!=1 || !chosen[0].Black || chosen[0].Time<3590 || chosen[0].Time>3592) throw new Exception("Black break planning failed");
        var fallback=PlanCuts(7200,keys,new List<BlackInterval>());
        if(fallback.Count!=1 || fallback[0].Black || fallback[0].Time!=3600) throw new Exception("Fallback planning failed");
        var many=PlanCuts(14500,Enumerable.Range(0,30000).Select(x=>x*0.5),new List<BlackInterval>());
        double last=0; foreach(var cut in many) {if(cut.Time-last>4200) throw new Exception("Disc duration overflow");last=cut.Time;}
        if(14500-last>4200) throw new Exception("Last disc overflow");
        if(PlanCuts(100,keys,new List<BlackInterval>()).Count!=0) throw new Exception("Short movie split unnecessarily");
        int rate=FitBitrate(7200,74);
        if(rate>=1150 || rate<200 || FitBitrate(7200,80)<=rate || FitBitrate(300,74)!=1150) throw new Exception("Bitrate budgeting failed");
        bool rejected=false; try {FitBitrate(100000,74);} catch(Exception) {rejected=true;}
        if(!rejected) throw new Exception("Impossible capacity was accepted");
        string test=Path.Combine(work,"planning"); Directory.CreateDirectory(test);
        Run(ff,new [] {"-hide_banner","-nostdin","-f","lavfi","-i","color=white:size=640x360:rate=30","-f","lavfi","-i","sine=frequency=440","-vf","drawbox=color=black:t=fill:enable='between(t,6.5,7.3)'","-t","14","-c:v","ffv1","-c:a","pcm_s16le","black-source.mkv"},test,token,log);
        Run(ff,ConvertArgs("black-source.mkv","clip.mpg",false,false,0,true),test,token,log);
        File.WriteAllText(Path.Combine(test,"clips.txt"),"file 'clip.mpg'\n");
        double duration=Duration(probe,"clip.mpg",test,token,log);
        var cuts=AnalyzeCuts(ff,probe,"clips.txt",test,duration,token,log,(p,d)=>{},8,3);
        if(cuts.Count!=1 || !cuts[0].Black || cuts[0].Time<6.5 || cuts[0].Time>7.3) throw new Exception("Real black detection / keyframe matching failed");
        Run(ff,PlannedOutputArgs("clips.txt",cuts,false),test,token,log);
        ValidateCuts(test,cuts);
        if(Directory.GetFiles(test,"disc-*.mpg").Length!=2) throw new Exception("Planned disc count failed");
        Run(ff,new [] {"-hide_banner","-nostdin","-i","disc-01.mpg","-frames:v","1","-pix_fmt","gray","-f","rawvideo","boundary.gray"},test,token,log);
        if(File.ReadAllBytes(Path.Combine(test,"boundary.gray")).Average(x=>(double)x)>25) throw new Exception("Actual disc start was not black");
        Run(ff,new [] {"-hide_banner","-nostdin","-i","disc-00.mpg","-vf","reverse","-frames:v","1","-pix_fmt","gray","-f","rawvideo","end-boundary.gray"},test,token,log);
        if(File.ReadAllBytes(Path.Combine(test,"end-boundary.gray")).Average(x=>(double)x)>25) throw new Exception("Previous disc did not end in black");
        string imager=Find("vcdimager.exe");
        Run(imager,new [] {"--type=vcd2","--cue-file=split.cue","--bin-file=split.bin","disc-01.mpg"},test,token,log);
        // Same real video at standard and computed long-film bitrate, including
        // remux and authoring, to catch accidental standard-rate VCD padding.
        Run(ff,ConvertArgs(Path.Combine(work,"source.mp4"),"compact.mpg",false,false,0,true,rate,true),test,token,log);
        File.WriteAllText(Path.Combine(test,"compact.txt"),"file 'compact.mpg'\n");
        string compactWork=Path.Combine(test,"compact-output"); Directory.CreateDirectory(compactWork);
        File.Copy(Path.Combine(test,"compact.mpg"),Path.Combine(compactWork,"compact.mpg"));
        File.Copy(Path.Combine(test,"compact.txt"),Path.Combine(compactWork,"compact.txt"));
        Run(ff,PlannedOutputArgs("compact.txt",new List<DiscCut>(),true),compactWork,token,log);
        RemoveEmptyPadding(Path.Combine(compactWork,"one-cd-padded.mpg"),Path.Combine(compactWork,"disc-00.mpg"),token);
        foreach(string media in new [] {"one-cd-padded","disc-00"}) Run(ff,new [] {"-hide_banner","-nostdin","-i",media+".mpg","-map","0:v:0","-map","0:a:0","-f","framemd5",media+".md5"},compactWork,token,log);
        if(File.ReadAllText(Path.Combine(compactWork,"one-cd-padded.md5"))!=File.ReadAllText(Path.Combine(compactWork,"disc-00.md5"))) throw new Exception("Padding removal changed decoded movie/audio frames or timestamps");
        Author(imager,"disc-00",compactWork,token,log);
        Run(imager,new [] {"--type=vcd2","--cue-file=compact.cue","--bin-file=compact.bin","disc-00.mpg"},compactWork,token,log);
        long compactBytes=new FileInfo(Path.Combine(compactWork,"disc-00.mpg")).Length;
        long standardBytes=new FileInfo(Path.Combine(work,"ntsc.mpg")).Length;
        if(compactBytes>=standardBytes*0.8) throw new Exception("Compact XVCD remained padded to standard VCD size");
        if(Directory.GetFiles(compactWork,"disc-*.mpg").Length!=1) throw new Exception("One-CD mode split the movie");
        if(!File.ReadAllText(Path.Combine(compactWork,"compact.cue")).Contains("TRACK 02 MODE2/2352")) throw new Exception("Compact image has no movie track");
        Run(imager,new [] {"--type=vcd2","--cue-file=standard.cue","--bin-file=standard.bin",Path.Combine(work,"ntsc.mpg")},compactWork,token,log);
        if(new FileInfo(Path.Combine(compactWork,"compact.bin")).Length>=new FileInfo(Path.Combine(compactWork,"standard.bin")).Length*0.9) throw new Exception("Authored compact image did not save space");
        log("Compact MPEG: "+compactBytes+" bytes; standard MPEG: "+standardBytes+" bytes; video rate: "+rate+" kb/s.");
    }
}


