using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

class DonationMenuTest {
    [STAThread] static int Main() {
        Application.EnableVisualStyles();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Exception failure=null;
        var selected=new List<string>();
        var form=new Form {Text="Donation menu regression test",Size=new Size(360,180)};
        var menu=Branding.CreateDonationMenu(form,url=>selected.Add(url));
        var timer=new Timer {Interval=40};
        int step=0;
        Application.ThreadException+=(s,e)=>{failure=e.Exception;timer.Stop();form.Close();};
        timer.Tick+=(s,e)=>{
            try {
                int action=step%4;
                if(action==0)menu.Show(form,new Point(20,40));
                if(action==1) {
                    if(!menu.Visible)throw new Exception("Menu did not open");
                    menu.Close(ToolStripDropDownCloseReason.Keyboard);
                }
                if(action==2)menu.Show(form,new Point(20,40));
                if(action==3) {
                    menu.Items[(step/4)%2].PerformClick();
                    menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                    if(menu.IsDisposed)throw new Exception("Menu disposed during dismissal");
                }
                if(++step==80){timer.Stop();form.Close();}
            } catch(Exception ex){failure=ex;timer.Stop();form.Close();}
        };
        form.Shown+=(s,e)=>timer.Start();
        Application.Run(form);
        timer.Dispose();form.Dispose();
        if(!menu.IsDisposed)failure=new Exception("Menu was not disposed with owner");
        if(selected.Count!=20)failure=new Exception("Expected 20 donation selections");
        for(int i=0;i<selected.Count;i++)
            if(selected[i]!=(i%2==0?Branding.DonationUrl:Branding.VenmoUrl))failure=new Exception("Incorrect donation destination");
        if(failure!=null){Console.Error.WriteLine(failure);return 1;}
        Console.WriteLine("PASS: 40 openings, 20 dismissals, 20 alternating donation selections, owner disposal; no browser launched.");
        return 0;
    }
}
