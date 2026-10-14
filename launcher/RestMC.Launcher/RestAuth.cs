using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestMCLauncher;

internal sealed class RestAuth
{
    public RestAccount? Current { get; private set; } = SecureStore.Load<RestAccount>(AppInfo.RestAccountFile);
    public async Task RefreshAsync()
    {
        if (Current is null || string.IsNullOrWhiteSpace(Current.Token)) return;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, AppInfo.Api + "/api/v1/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Current.Token);
            var j = await Net.JsonAsync(req);
            Current.Name = j["name"]?.GetValue<string>() ?? Current.Name;
            Current.Plan = j["plan"]?.GetValue<string>() ?? Current.Plan;
            SecureStore.Save(AppInfo.RestAccountFile, Current);
            WriteBridge();
        }
        catch { }
    }
    public async Task LoginAsync(IWin32Window owner)
    {
        using var listener = new HttpListener();
        int port = GetPort();
        string prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix); listener.Start();
        string state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        Process.Start(new ProcessStartInfo($"{AppInfo.Api}/login/?port={port}&state={state}") { UseShellExecute = true });
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        var ctxTask = listener.GetContextAsync();
        var completed = await Task.WhenAny(ctxTask, Task.Delay(Timeout.Infinite, cts.Token));
        if (completed != ctxTask) throw new TimeoutException("Login timed out.");
        var ctx = await ctxTask;
        string code = ctx.Request.QueryString["code"] ?? "";
        string gotState = ctx.Request.QueryString["state"] ?? "";
        byte[] ok = Encoding.UTF8.GetBytes("<html><body style='font-family:Segoe UI;background:#0a0d13;color:#f5c65b'><h2>RestMC login complete.</h2><p>You can return to the launcher.</p></body></html>");
        ctx.Response.ContentType = "text/html"; ctx.Response.ContentLength64 = ok.Length; await ctx.Response.OutputStream.WriteAsync(ok); ctx.Response.Close(); listener.Stop();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(gotState))) throw new InvalidOperationException("Login state mismatch.");
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("No login code received.");
        var req = new HttpRequestMessage(HttpMethod.Post, AppInfo.Api + "/api/v1/exchange") { Content = new StringContent(JsonSerializer.Serialize(new { code }), Encoding.UTF8, "application/json") };
        var j = await Net.JsonAsync(req);
        string token = j["token"]?.GetValue<string>() ?? j["session"]?.GetValue<string>() ?? throw new InvalidOperationException("Token missing from exchange response.");
        Current = new RestAccount { Token = token };
        await RefreshAsync();
        SecureStore.Save(AppInfo.RestAccountFile, Current);
        WriteBridge();
    }
    public async Task LogoutAsync()
    {
        if (Current is not null && !string.IsNullOrWhiteSpace(Current.Token))
        {
            try { var req = new HttpRequestMessage(HttpMethod.Post, AppInfo.Api + "/api/v1/logout"); req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Current.Token); await Net.JsonAsync(req); } catch { }
        }
        if (File.Exists(AppInfo.RestAccountFile)) File.Delete(AppInfo.RestAccountFile);
        if (Current is not null) File.WriteAllText(AppInfo.GameSessionFile, "out\n" + SecureStore.SealText(Current.Token) + "\n");
        Current = null;
    }
    public void WriteBridge()
    {
        if (Current is null) return;
        Directory.CreateDirectory(AppInfo.RestRoot);
        File.WriteAllText(AppInfo.GameSessionFile, "in\n" + SecureStore.SealText(Current.Token) + "\n" + Current.Name + "\n");
    }
    private static int GetPort() { var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); l.Start(); int p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
}
