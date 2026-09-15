using System;
using System.Drawing;
using WinForms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;
using WpfWindow = System.Windows.Window;
using WpfWindowState = System.Windows.WindowState;

namespace CodePacker.Services;

public class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _trayIcon;
    private readonly WpfWindow _mainWindow;

    public TrayService(WpfWindow mainWindow)
    {
        _mainWindow = mainWindow;

        _trayIcon = new WinForms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Code Packer",
            Visible = true
        };

        var menu = new WinForms.ContextMenuStrip();

        menu.Items.Add("Развернуть", null, (_, _) => ShowWindow());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) =>
            WpfApplication.Current.Shutdown());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        _mainWindow.Show();
        _mainWindow.WindowState = WpfWindowState.Normal;
        _mainWindow.Activate();
    }

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }
}