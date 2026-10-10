using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Windows.Forms;

public sealed class DonationArrow : Control {
    readonly System.Windows.Forms.Timer animation;
    double phase;
    public DonationArrow() {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;TabStop=false;AccessibleName="Animated arrow pointing to Please donate";AccessibleRole=AccessibleRole.Graphic;
        animation=new System.Windows.Forms.Timer {Interval=33};
        animation.Tick+=(s,e)=>{phase=(phase+0.13)%(Math.PI*2);Invalidate();};
        animation.Start();
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float offset=(float)(Math.Sin(phase)+1)*Width*0.08f;
        float start=Width*0.12f+offset,end=Width*0.76f+offset,y=Height*0.5f,head=Height*0.2f;
        using(var pen=new Pen(Color.FromArgb(22,128,61),Math.Max(2,Height*0.1f))) {
            pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
            e.Graphics.DrawLine(pen,start,y,end,y);
            e.Graphics.DrawLines(pen,new [] {new PointF(end-head,y-head),new PointF(end,y),new PointF(end-head,y+head)});
        }
    }
    protected override void OnVisibleChanged(EventArgs e) {base.OnVisibleChanged(e);if(animation!=null)animation.Enabled=Visible;}
    protected override void Dispose(bool disposing) {if(disposing && animation!=null)animation.Dispose();base.Dispose(disposing);}
}

public static class Branding {
    public const string DonationUrl="https://paypal.me/albie";
    public const string VenmoUrl="https://account.venmo.com/u/Albie_Burdge";
    public static Bitmap Draw(int size) {
        var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(bitmap)) {
            g.SmoothingMode=SmoothingMode.AntiAlias; g.ScaleTransform(size/256f,size/256f);
            using(var bg=new LinearGradientBrush(new Rectangle(0,0,256,256),Color.FromArgb(20,31,67),Color.FromArgb(27,87,155),45))g.FillEllipse(bg,4,4,248,248);
            using(var disc=new LinearGradientBrush(new Rectangle(40,40,176,176),Color.FromArgb(238,250,255),Color.FromArgb(91,191,219),45))g.FillEllipse(disc,40,40,176,176);
            using(var arc=new Pen(Color.FromArgb(180,255,255,255),7))g.DrawArc(arc,55,55,146,146,195,85);
            using(var inner=new SolidBrush(Color.FromArgb(22,46,85)))g.FillEllipse(inner,96,96,64,64);
            using(var shadow=new SolidBrush(Color.FromArgb(20,31,67)))g.FillEllipse(shadow,135,135,100,100);
            using(var play=new SolidBrush(Color.FromArgb(255,181,63)))g.FillPolygon(play,new [] {new PointF(171,156),new PointF(171,212),new PointF(216,184)});
        }
        return bitmap;
    }
    public static void WriteAssets(string folder) {
        Directory.CreateDirectory(folder);
        using(var logo=Draw(256))logo.Save(Path.Combine(folder,"DiscWorkshop.png"),ImageFormat.Png);
        int[] sizes={16,24,32,48,64,128,256}; var payloads=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++)using(var bitmap=Draw(sizes[i]))using(var data=new MemoryStream()){bitmap.Save(data,ImageFormat.Png);payloads[i]=data.ToArray();}
        using(var file=File.Create(Path.Combine(folder,"DiscWorkshop.ico")))using(var writer=new BinaryWriter(file)) {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(payloads[i].Length);writer.Write(offset);offset+=payloads[i].Length;}
            foreach(var bytes in payloads)writer.Write(bytes);
        }
    }
    public static ContextMenuStrip CreateDonationMenu(Control owner,Action<string> openDonation=null) {
        if(openDonation==null)openDonation=OpenDonation;
        var menu=new ContextMenuStrip();
        menu.Items.Add("Donate with PayPal",null,(s,e)=>openDonation(DonationUrl));
        menu.Items.Add("Donate with Venmo",null,(s,e)=>openDonation(VenmoUrl));
        // Closing a popup is not the end of its lifetime: WinForms still uses it.
        owner.Disposed+=(s,e)=>menu.Dispose();
        return menu;
    }
    static void OpenDonation(string url) {
        try {Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
        catch(Exception e){MessageBox.Show("Open "+url+" in your browser.\n"+e.Message,"Donate to Albert Mark Burdge");}
    }
    public static void SaveShortcut(string path) {
        Type t=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(t),link=null;
        try {
            link=t.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,shell,new object[]{path});
            Type lt=link.GetType();
            foreach(var pair in new [] {new [] {"TargetPath",Application.ExecutablePath},new [] {"WorkingDirectory",AppDomain.CurrentDomain.BaseDirectory},new [] {"IconLocation",Application.ExecutablePath+",0"},new [] {"Description","Disc Workshop — Made by Albert Mark Burdge"}})
                lt.InvokeMember(pair[0],System.Reflection.BindingFlags.SetProperty,null,link,new object[]{pair[1]});
            lt.InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,link,null);
        } finally {if(link!=null)System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    public static void Shortcut(IWin32Window owner) {
        using(var dialog=new SaveFileDialog {Title="Create Disc Workshop shortcut",Filter="Windows shortcut|*.lnk",FileName="Disc Workshop.lnk",InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),OverwritePrompt=true})
            if(dialog.ShowDialog(owner)==DialogResult.OK){SaveShortcut(dialog.FileName);MessageBox.Show("Shortcut created with the Disc Workshop icon.","Disc Workshop");}
    }
}
