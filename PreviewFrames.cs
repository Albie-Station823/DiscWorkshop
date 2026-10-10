using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;

public static partial class Engine {
    public static List<double> PreviewFrames(string probe,string input,double time,CancellationToken token) {
        double origin=0;
        Run(probe,new[]{"-v","error","-show_entries","format=start_time","-of","default=noprint_wrappers=1:nokey=1",input},Path.GetDirectoryName(input),token,line=> {double value;if(double.TryParse(line,NumberStyles.Float,CultureInfo.InvariantCulture,out value))origin=value;});
        var frames=new List<double>();
        Run(probe,new[]{"-v","error","-read_intervals",Number(Math.Max(origin,time+origin-3))+"%"+Number(time+origin+3),"-select_streams","v:0","-show_frames","-show_entries","frame=best_effort_timestamp_time","-of","csv=p=0",input},Path.GetDirectoryName(input),token,line=> {
            double value;if(double.TryParse(line.Split(',')[0],NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&value-origin>=-0.0001)frames.Add(Math.Max(0,value-origin));
        });return frames.Distinct().OrderBy(x=>x).ToList();
    }
}
