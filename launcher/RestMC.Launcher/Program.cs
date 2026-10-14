using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestMCLauncher;

internal static class Program
{
    [STAThread]
    static void Main(){Directory.CreateDirectory(AppInfo.RestRoot);ApplicationConfiguration.Initialize();Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);Application.Run(new MainForm());}
}
