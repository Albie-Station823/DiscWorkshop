using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
class PreviewRegressionTest {
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static void Pump(int ms){var end=DateTime.UtcNow.AddMilliseconds(ms);while(DateTime.UtcNow<end){Application.DoEvents();Thread.Sleep(10);}}
 [DllImport("user32.dll")]static extern IntPtr GetFocus();
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr key,IntPtr flags);
 [DllImport("user32.dll")]static extern bool EnableWindow(IntPtr window,bool enabled);
 [DllImport("user32.dll")]static extern IntPtr SetFocus(IntPtr window);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")]static extern bool AttachThreadInput(uint first,uint second,bool attach);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
 static void NativeKey(Keys key){keybd_event((byte)key,0,0,UIntPtr.Zero);keybd_event((byte)key,0,2,UIntPtr.Zero);Pump(200);}
 static void Key(BreakEditor e,Keys key){if(!e.ContainsFocus){SetForegroundWindow(e.Handle);((Control)Field(e,"scrub")).Focus();Pump(80);}IntPtr target=GetFocus();Check(target!=IntPtr.Zero,"No focused control for keyboard test");PostMessage(target,0x100,new IntPtr((int)key),new IntPtr(1));PostMessage(target,0x101,new IntPtr((int)key),new IntPtr(unchecked((int)0xC0000001)));Pump(40);}
 static void Check(bool b,string s){if(!b)throw new Exception(s);}
 static void Seek(BreakEditor e,double time){((MovieTimeInput)Field(e,"position")).Value=time;typeof(BreakEditor).GetMethod("Seek",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(e,null);Pump(1200);}
 [STAThread]static int Main(){try{
  Application.EnableVisualStyles();string ff=@"C:\Program Files (x86)\Common Files\AutoPod\ffmpeg\bin\ffmpeg.exe";
  string work=Path.GetFullPath("test-results/preview-regression");Directory.CreateDirectory(work);
  Engine.Run(ff,new[]{"-v","error","-y","-f","lavfi","-i","nullsrc=s=160x90:r=25,geq=lum=20+6*N/25:cb=128:cr=128","-f","lavfi","-i","sine=frequency=440","-t","30","-c:v","libx264","-g","250","-c:a","aac","long.mp4"},work,CancellationToken.None,s=>{});
  Engine.Run(ff,new[]{"-v","error","-y","-i","long.mp4","-c","copy","-output_ts_offset","3600","offset.ts"},work,CancellationToken.None,s=>{});
  foreach(string name in new[]{"long.mp4","offset.ts"})using(var e=new BreakEditor(ff,new[]{Path.GetFullPath("test-results/preview-regression/"+name)},new[]{30.0},new List<double>(),false)){
   e.Show();e.Activate();((Control)Field(e,"scrub")).Focus();Pump(1200);var pos=(MovieTimeInput)Field(e,"position");
   Key(e,Keys.Right);Pump(1200);Seek(e,20);double displayed=pos.Value;Key(e,Keys.Left);Pump(1200);
   Console.WriteLine(name+" late Left: "+pos.Value+" generation="+Field(e,"generation")+" state="+((Label)Field(e,"state")).Text);Check(pos.Value<displayed&&displayed-pos.Value<.045,"Wrong late Left time");double previous=pos.Value;
   var image=(Bitmap)((PictureBox)Field(e,"picture")).Image;Console.WriteLine("Pixel: "+image.GetPixel(image.Width/2,image.Height/2).R);image.Save("test-results/preview-regression/actual-"+name+".png");Check(image.GetPixel(image.Width/2,image.Height/2).R>130,"Preview image returned to beginning");
   Key(e,Keys.Right);Pump(1000);Check(Math.Abs(pos.Value-previous-.04)<.002,"Wrong late Right time");
   Seek(e,15);Key(e,Keys.Space);Pump(1800);Key(e,Keys.Space);Pump(500);double paused=pos.Value;Key(e,Keys.Left);Pump(1000);
   Console.WriteLine(name+" paused "+paused+" -> "+pos.Value);Check(pos.Value>15&&Math.Abs(pos.Value-paused)<.1,"Playback frame stepping returned to start");image=(Bitmap)((PictureBox)Field(e,"picture")).Image;Check(image.GetPixel(image.Width/2,image.Height/2).R>105,"Paused playback picture returned to opening frame");Key(e,Keys.Right);Pump(1000);Check(pos.Value>15,"Right after pause returned to opening");
   var playback=(PreviewPlayback)Field(e,"playback");Key(e,Keys.Space);Pump(800);
   IntPtr native=(IntPtr)Field(playback,"window");Check(native!=IntPtr.Zero,"Player window not attached");uint playerProcess;uint playerThread=GetWindowThreadProcessId(native,out playerProcess),testThread=GetCurrentThreadId();
   Check(AttachThreadInput(testThread,playerThread,true),"Could not focus native player for keyboard test");
   try{
    SetForegroundWindow(e.Handle);EnableWindow(native,true);SetFocus(native);Pump(60);Check(GetFocus()==native,"Native player did not receive test focus");
    Console.WriteLine("Foreground="+GetForegroundWindow()+" owner="+e.Handle+" nativeRoot="+GetAncestor(native,2)+" hook="+Field(Field(e,"keyFilter"),"hook"));
    Check(GetForegroundWindow()==e.Handle||GetAncestor(GetForegroundWindow(),2)==e.Handle,"Test editor lost foreground; refusing to inject keys into another app");
    int beforeVolume=playback.VolumeSteps;NativeKey(Keys.Down);Check(playback.VolumeSteps==beforeVolume-1,"Native player bypassed volume shortcut");NativeKey(Keys.Up);Check(playback.VolumeSteps==beforeVolume,"Native player bypassed volume increase");
    NativeKey(Keys.Space);Pump(300);Check(playback.Paused,"Native player bypassed pause shortcut");double nativePaused=pos.Value;NativeKey(Keys.Left);Pump(1000);Check(pos.Value>15&&Math.Abs(pos.Value-nativePaused)<.1,"Native player bypassed frame shortcut");
   }finally{EnableWindow(native,false);AttachThreadInput(testThread,playerThread,false);}
   ((Control)Field(e,"play")).Focus();Key(e,Keys.Space);Pump(800);
   foreach(Control focus in new Control[]{(Control)Field(e,"scrub"),pos.Controls.OfType<NumericUpDown>().First(),(Control)Field(e,"play")}){
    focus.Focus();Pump(60);double before=pos.Value;int volume=playback.VolumeSteps;Key(e,Keys.Down);Key(e,Keys.Up);Check(playback.VolumeSteps==volume,"Volume keys did not route through focused "+focus.GetType().Name);Key(e,Keys.Down);Check(playback.VolumeSteps==volume-1,"Down was consumed by focused "+focus.GetType().Name);Check(pos.Value>=before-.1,"Volume key changed movie position");
   }
   Key(e,Keys.Space);Pump(300);pos.Controls.OfType<NumericUpDown>().First().Focus();double numericTime=pos.Value;Key(e,Keys.Left);Pump(1000);Check(pos.Value>15&&Math.Abs(pos.Value-numericTime)<.1,"Numeric input consumed frame-step key");e.Close();
  }
  File.WriteAllText(Path.Combine(work,"PASS.txt"),"PASS: rendered picture and source-frame Left/Right after late seeks and live play/pause; long-GOP MP4 and nonzero-timestamp MPEG transport stream.");Console.WriteLine("PASS");return 0;
 }catch(Exception x){Console.WriteLine(x);return 1;}}
}
