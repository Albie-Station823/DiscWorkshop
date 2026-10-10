using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Web.Script.Serialization;

public sealed class SubtitleChoice {
    public int Stream=-1;
    public string FilePath,Codec,Description="No subtitles";
    public bool Bitmap {get {return Codec=="dvd_subtitle"||Codec=="hdmv_pgs_subtitle"||Codec=="dvb_subtitle";}}
    public bool Enabled {get {return Stream>=0||!string.IsNullOrEmpty(FilePath);}}
    public override string ToString(){return Description;}
}
public static partial class Engine {
    public static List<SubtitleChoice> SubtitleTracks(string probe,string source,CancellationToken token) {
        var text=new System.Text.StringBuilder();
        Run(probe,new[]{"-v","error","-select_streams","s","-show_entries","stream=index,codec_name:stream_tags=language,title","-of","json",source},Path.GetDirectoryName(source),token,s=>{lock(text){if(!s.StartsWith("ffprobe.exe "))text.AppendLine(s);}});
        var json=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(text.ToString());
        var result=new List<SubtitleChoice>(); int relative=0;
        foreach(var item in (System.Collections.IEnumerable)json["streams"]) {
            var stream=(Dictionary<string,object>)item; string codec=Convert.ToString(stream["codec_name"]),language="und",title="";
            if(stream.ContainsKey("tags")){var tags=(Dictionary<string,object>)stream["tags"];if(tags.ContainsKey("language"))language=Convert.ToString(tags["language"]);if(tags.ContainsKey("title"))title=Convert.ToString(tags["title"]);}
            result.Add(new SubtitleChoice{Stream=relative,Codec=codec,Description="Track "+(relative+1)+" • "+language+" • "+title+" ("+codec+")"});relative++;
        }
        return result;
    }
    // Only generated simple filenames enter libavfilter; user paths are passed as separate process arguments.
    public static string PrepareSubtitle(string ff,string source,SubtitleChoice choice,string work,int index,CancellationToken token,Action<string> log) {
        if(choice==null||!choice.Enabled||choice.Bitmap)return null;
        string name="subtitles-"+index.ToString("D3")+".ass";
        var a=new List<string>{"-hide_banner","-nostdin","-n","-i",string.IsNullOrEmpty(choice.FilePath)?source:choice.FilePath,"-map",string.IsNullOrEmpty(choice.FilePath)?"0:s:"+choice.Stream:"0:s:0","-c:s","ass",name};
        Run(ff,a.ToArray(),work,token,log);return name;
    }
    public static string[] WithSubtitles(string[] original,SubtitleChoice choice,string prepared) {
        if(choice==null||!choice.Enabled)return original;
        var a=original.ToList();int vf=a.IndexOf("-vf");string filter=a[vf+1];
        // Burn captions before the final squeeze so the TV stretch restores their proportions too.
        int last=filter.LastIndexOf(",scale=",StringComparison.Ordinal);
        if(!choice.Bitmap) {
            if(string.IsNullOrEmpty(prepared)||prepared.Any(c=>!(char.IsLetterOrDigit(c)||c=='-'||c=='.')))throw new Exception("Invalid prepared subtitle filename.");
            filter=filter.Insert(last,",subtitles=filename="+prepared);
            a[vf+1]=filter;
        } else {
            a.RemoveRange(vf,2);
            int map=a.IndexOf("0:v:0");a[map]="[captioned]";
            a.InsertRange(a.Count-1,new[]{"-filter_complex","[0:v:0][0:s:"+choice.Stream+"]overlay=eof_action=pass:repeatlast=0,"+filter+"[captioned]"});
        }
        return a.ToArray();
    }
    public static string[] LosslessArgs(string[] original,bool pal,EncodingOptions options=null) {
        var a=original.ToList();string output=a.Last();int target=a.IndexOf("-target");
        // Preserve a bitmap subtitle graph which may follow the target options.
        int complex=a.IndexOf("-filter_complex");string graph=complex>=0?a[complex+1]:null;
        a.RemoveRange(target,a.Count-target);
        a.AddRange(new[]{"-c:v","ffv1","-g","1","-pix_fmt","yuv420p","-r",pal?"25":"30000/1001","-aspect","4:3","-ac","2","-ar","44100","-c:a","pcm_s16le"});
        if(options!=null)a.AddRange(new[]{"-r",options.FrameRate,"-aspect",options.Aspect,"-ac",options.Channels.ToString(),"-ar",options.SampleRate.ToString()});
        if(graph!=null)a.AddRange(new[]{"-filter_complex",graph});a.Add(output);return a.ToArray();
    }
    public static List<DiscCut> ManualCuts(IEnumerable<double> requested,double duration,bool pal,double customFps=0) {
        double fps=customFps>0?customFps:pal?25:30000.0/1001;
        var values=requested.OrderBy(x=>x).ToArray();var cuts=new List<DiscCut>();double last=0;
        foreach(double value in values) {
            if(double.IsNaN(value)||double.IsInfinity(value))throw new Exception("Invalid manual break time.");
            double cut=Math.Round(value*fps,MidpointRounding.AwayFromZero)/fps;
            if(cut-last<1 || duration-cut<1)throw new Exception("Manual breaks must leave at least one second on every disc and be at least one second apart.");
            cuts.Add(new DiscCut(cut,false){Manual=true,Requested=value});last=cut;
        }
        if(cuts.Count==0)throw new Exception("Add at least one manual break, or turn manual breaks off.");
        return cuts;
    }
    public static int MaximumRate(double duration,int minutes,string format,EncodingOptions options=null) {return FitBitrate(duration,minutes,options==null?2500:options.MaximumRate,options==null?224:options.AudioRate);}
    public static string[] EncodeVolumeArgs(string list,string output,double start,double duration,bool pal,string format,int rate,EncodingOptions options=null) {
        var a=new List<string>{"-hide_banner","-nostdin","-nostats","-progress","pipe:1","-n","-f","concat","-safe","1","-i",list,
            "-ss",Number(start),"-t",Number(duration),"-map","0:v:0","-map","0:a:0","-target",(pal?"pal-":"ntsc-")+(format=="SVCD"?"svcd":"vcd"),"-aspect","4:3","-ac","2","-b:a","224k"};
        if(format!="VCD")a.AddRange(new[]{"-b:v",rate+"k","-minrate:v",rate+"k","-maxrate:v",rate+"k"});
        if(format=="SVCD")a.AddRange(new[]{"-bf","0","-flags","+cgop","-sc_threshold","1000000000"});
        if(format=="XVCD")a.AddRange(new[]{"-muxrate",MuxRate(format,rate)});
        a.Add(output);return ApplyEncodingOptions(a.ToArray(),options,format,rate);
    }
    public static string[] WithBreakFades(string[] original,double start,double length,double seconds,bool fadeIn,bool fadeOut,double blackBefore=0,double blackAfter=0) {
        if((seconds<=0||(!fadeIn&&!fadeOut))&&blackBefore<=0&&blackAfter<=0)return original;
        double fade=Math.Min(seconds,length/2);
        var a=original.ToList();int seek=a.IndexOf("-ss");if(seek>=0)a.RemoveRange(seek,2);
        string vf="trim=start="+Number(start)+":duration="+Number(length)+",setpts=PTS-STARTPTS";
        string af="atrim=start="+Number(start)+":duration="+Number(length)+",asetpts=PTS-STARTPTS";
        if(fadeIn&&seconds>0){vf+=",fade=t=in:st=0:d="+Number(fade);af+=",afade=t=in:st=0:d="+Number(fade);}
        if(fadeOut&&seconds>0){vf+=",fade=t=out:st="+Number(length-fade)+":d="+Number(fade);af+=",afade=t=out:st="+Number(length-fade)+":d="+Number(fade);}
        if(blackBefore>0||blackAfter>0){
            vf+=",tpad=start_duration="+Number(blackBefore)+":stop_duration="+Number(blackAfter)+":start_mode=add:stop_mode=add:color=black";
            if(blackBefore>0)af+=",adelay="+Number(blackBefore*1000)+":all=1";
            af+=",apad=whole_dur="+Number(length+blackBefore+blackAfter);
            int duration=a.IndexOf("-t");a[duration+1]=Number(length+blackBefore+blackAfter);
        }
        a.InsertRange(a.Count-1,new[]{"-vf",vf,"-af",af});return a.ToArray();
    }
    public static void EncodeVolumes(string ff,string probe,string work,List<DiscCut> cuts,double duration,bool pal,string format,int selectedRate,bool maximize,int minutes,CancellationToken token,Action<string> log,Action<int,string> progress,EncodingOptions options=null,double fadeSeconds=0,double blackBefore=0,double blackAfter=0) {
        var report=new List<string>{"Disc,start_seconds,end_seconds,video_kbps,selection,black_before_seconds,black_after_seconds,output_seconds"};double start=0;
        for(int i=0;i<=cuts.Count;i++) {
            double end=i<cuts.Count?cuts[i].Time:duration,length=end-start;
            double outputLength=length+blackBefore+blackAfter;
            int rate=maximize?MaximumRate(outputLength,minutes,format,options):selectedRate;
            if(!maximize && outputLength>VolumeSeconds(format,rate,minutes,options==null?224:options.AudioRate)+0.05)throw new Exception("Disc "+(i+1)+" exceeds capacity at this bitrate. Enable Maximum bitrate per disc, lower the video bitrate, or add a break.");
            string raw=(format=="XVCD"?"padded-":"disc-")+i.ToString("D2")+".mpg";
            var tracker=new ConversionProgress(outputLength);log("Disc "+(i+1)+": "+rate+" kb/s; "+Number(outputLength)+" seconds including black screens.");
            var args=WithBreakFades(EncodeVolumeArgs("clips.txt",raw,start,length,pal,format,rate,options),start,length,fadeSeconds,i>0,i<cuts.Count,blackBefore,blackAfter);
            Run(ff,args,work,token,line=>{log(line);tracker.Observe(line,(p,d)=>progress(p,"Encoding disc "+(i+1)+" / "+(cuts.Count+1)+" • "+d));});
            string final="disc-"+i.ToString("D2")+".mpg";
            if(format=="XVCD")RemoveEmptyPadding(Path.Combine(work,raw),Path.Combine(work,final),token);
            double actual=Duration(probe,final,work,token,log);
            if(Math.Abs(actual-outputLength)>0.15)throw new Exception("Encoded disc duration differs from its plan. Inspect the source timestamps and job.log before burning.");
            if(new FileInfo(Path.Combine(work,final)).Length/2324.0*2352+1200L*2352>DiscCapacity(minutes))throw new Exception("Disc "+(i+1)+" exceeds media capacity. Reduce the bitrate or add a break.");
            report.Add((i+1)+","+Number(start)+","+Number(end)+","+rate+","+(i==cuts.Count?"end":cuts[i].Manual?"manual":cuts[i].Quiet?"quiet":cuts[i].Black?"black":"fallback")+","+Number(blackBefore)+","+Number(blackAfter)+","+Number(outputLength));start=end;
        }
        File.WriteAllLines(Path.Combine(work,"DISC-BITRATES.csv"),report);
    }
}
