using System;
using System.Windows.Forms;

namespace TimeTrack.DesktopHost;

public class MainForm : Form
{
    public MainForm()
    {
        Text = "TimeTrack Desktop";
        Width = 800;
        Height = 600;

        // TODO: Integrar WebView2
        var label = new Label
        {
            Text = "TimeTrack Desktop Host - WebView2 placeholder",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };
        Controls.Add(label);
    }
}
