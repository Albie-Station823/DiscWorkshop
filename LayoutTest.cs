using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class LayoutTest {
    [STAThread]static void Main(){Application.EnableVisualStyles();using(var form=new Workshop()){
        Func<string,object> field=n=>typeof(Workshop).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
        ((CheckBox)field("lowVision")).Checked=true;form.Show();Application.DoEvents();
        var scroll=(Panel)field("scroll");scroll.AutoScrollPosition=new Point(0,2000);Application.DoEvents();
        foreach(var name in new[]{"lowVision","accessibilityBar","scroll","movieRoot"}){var c=(Control)field(name);Console.WriteLine(name+" "+c.Bounds+" visible="+c.Visible+" screen="+c.PointToScreen(Point.Empty));}
        var root=(TableLayoutPanel)field("movieRoot");foreach(Control c in root.Controls)Console.WriteLine(c.GetType().Name+" "+c.Bounds+" autosize="+c.AutoSize+" pref="+c.PreferredSize);
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results","layout-current.png"));}form.Close();
    }}
}
