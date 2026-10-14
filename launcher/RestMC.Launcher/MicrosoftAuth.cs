using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestMCLauncher;

internal sealed class MicrosoftAuth
{
    public MicrosoftAccount? Current { get; private set; } = SecureStore.Load<MicrosoftAccount>(AppInfo.MsAccountFile);
    public async Task LoginAsync()
    {
        int port = GetPort();
        string redirect = $"http://localhost:{port}/";
        string state = B64(RandomNumberGenerator.GetBytes(24));
        string verifier = B64(RandomNumberGenerator.GetBytes(48));
        string challenge = B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        using var listener = new HttpListener(); listener.Prefixes.Add(redirect); listener.Start();
        string auth = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?" + Q(new Dictionary<string,string>{{"client_id",AppInfo.MicrosoftClientId},{"response_type","code"},{"redirect_uri",redirect},{"scope","XboxLive.signin offline_access"},{"state",state},{"code_challenge",challenge},{"code_challenge_method","S256"},{"prompt","select_account"}});
        Process.Start(new ProcessStartInfo(auth) { UseShellExecute = true });
        var ctxTask = listener.GetContextAsync(); var completed = await Task.WhenAny(ctxTask, Task.Delay(TimeSpan.FromMinutes(10))); if (completed != ctxTask) throw new TimeoutException("Microsoft login timed out.");
        var ctx = await ctxTask; string code = ctx.Request.QueryString["code"] ?? ""; string gotState = ctx.Request.QueryString["state"] ?? "";
        byte[] ok = Encoding.UTF8.GetBytes("<html><body style='font-family:Segoe UI;background:#0a0d13;color:#f5c65b'><h2>Microsoft login complete.</h2><p>You can return to RestMC.</p></body></html>"); ctx.Response.ContentType="text/html"; ctx.Response.ContentLength64=ok.Length; await ctx.Response.OutputStream.WriteAsync(ok); ctx.Response.Close(); listener.Stop();
        if (gotState != state || string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Microsoft callback validation failed.");
        var token = await MsTokenAsync(new Dictionary<string,string>{{"client_id",AppInfo.MicrosoftClientId},{"grant_type","authorization_code"},{"code",code},{"redirect_uri",redirect},{"code_verifier",verifier},{"scope","XboxLive.signin offline_access"}});
        Current = new MicrosoftAccount { RefreshToken = token.refreshToken, AccessToken = token.accessToken, ExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.expiresIn - 120) };
        await MinecraftChainAsync(Current);
        SecureStore.Save(AppInfo.MsAccountFile, Current);
    }
    public async Task EnsureFreshAsync()
    {
        if (Current is null) throw new InvalidOperationException("Sign in with Microsoft first.");
        if (Current.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(2) && !string.IsNullOrEmpty(Current.MinecraftToken)) return;
        var token = await MsTokenAsync(new Dictionary<string,string>{{"client_id",AppInfo.MicrosoftClientId},{"grant_type","refresh_token"},{"refresh_token",Current.RefreshToken},{"scope","XboxLive.signin offline_access"}});
        Current.AccessToken = token.accessToken; if (!string.IsNullOrEmpty(token.refreshToken)) Current.RefreshToken = token.refreshToken; Current.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(token.expiresIn - 120);
        await MinecraftChainAsync(Current); SecureStore.Save(AppInfo.MsAccountFile, Current);
    }
    public void Logout() { Current = null; if (File.Exists(AppInfo.MsAccountFile)) File.Delete(AppInfo.MsAccountFile); }
    private static async Task<(string accessToken,string refreshToken,int expiresIn)> MsTokenAsync(Dictionary<string,string> fields)
    {
        using var res = await Net.Http.PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", new FormUrlEncodedContent(fields));
        string body = await res.Content.ReadAsStringAsync(); if (!res.IsSuccessStatusCode) throw new InvalidOperationException("Microsoft token error: " + body);
        var j = JsonNode.Parse(body)!; return (j["access_token"]!.GetValue<string>(), j["refresh_token"]?.GetValue<string>() ?? "", j["expires_in"]!.GetValue<int>());
    }
    private static async Task MinecraftChainAsync(MicrosoftAccount a)
    {
        var xblReq = new HttpRequestMessage(HttpMethod.Post, "https://user.auth.xboxlive.com/user/authenticate") { Content = new StringContent(JsonSerializer.Serialize(new { Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = "d=" + a.AccessToken }, RelyingParty = "http://auth.xboxlive.com", TokenType = "JWT" }), Encoding.UTF8, "application/json") };
        var xbl = await Net.JsonAsync(xblReq); string xblToken = xbl["Token"]!.GetValue<string>(); string uhs = xbl["DisplayClaims"]!["xui"]![0]!["uhs"]!.GetValue<string>();
        var xstsReq = new HttpRequestMessage(HttpMethod.Post, "https://xsts.auth.xboxlive.com/xsts/authorize") { Content = new StringContent(JsonSerializer.Serialize(new { Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xblToken } }, RelyingParty = "rp://api.minecraftservices.com/", TokenType = "JWT" }), Encoding.UTF8, "application/json") };
        var xsts = await Net.JsonAsync(xstsReq); string xstsToken = xsts["Token"]!.GetValue<string>();
        var mcReq = new HttpRequestMessage(HttpMethod.Post, "https://api.minecraftservices.com/authentication/login_with_xbox") { Content = new StringContent(JsonSerializer.Serialize(new { identityToken = $"XBL3.0 x={uhs};{xstsToken}" }), Encoding.UTF8, "application/json") };
        var mc = await Net.JsonAsync(mcReq); a.MinecraftToken = mc["access_token"]!.GetValue<string>();
        var profReq = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile"); profReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", a.MinecraftToken); var prof = await Net.JsonAsync(profReq); a.Name = prof["name"]!.GetValue<string>(); a.Uuid = prof["id"]!.GetValue<string>();
    }
    private static int GetPort() { var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); l.Start(); int p=((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p; }
    private static string B64(byte[] b)=>Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');
    private static string Q(Dictionary<string,string> d)=>string.Join("&",d.Select(kv=>$"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
}
