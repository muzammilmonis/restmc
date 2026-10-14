using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestMCLauncher;

internal static class AppInfo
{
    public const string Name = "RestMC";
    public const string Version = "1.6.7";
    public const string MinecraftVersion = "1.21.11";
    public const string Api = "https://mc.tiers.rest";
    public const string ClientJar = Api + "/downloads/RestMC-1.6.7.jar?v=launcher3";
    public const string ClientSha = Api + "/downloads/RestMC-1.6.7.sha256?v=launcher3";
    // Upstream public-client id used by the original launcher. Replace with a RestMC Entra app id before broad public distribution.
    public const string MicrosoftClientId = "dacf11f1-b22d-4b0d-a837-5504fd8af781";
    public static readonly string RestRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".restmc");
    public static readonly string SettingsFile = Path.Combine(RestRoot, "launcher-settings.json");
    public static readonly string RestAccountFile = Path.Combine(RestRoot, "restmc-account.dat");
    public static readonly string MsAccountFile = Path.Combine(RestRoot, "microsoft-accounts.dat");
    public static readonly string GameSessionFile = Path.Combine(RestRoot, "game-session.txt");
}

internal sealed class LauncherSettings
{
    public string GameDirectory { get; set; } = Path.Combine(AppInfo.RestRoot, "game");
    public int RamMb { get; set; } = 4096;
    public string JavaPath { get; set; } = "";
    public string FabricLoader { get; set; } = "";
    public static LauncherSettings Load()
    {
        try { return JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(AppInfo.SettingsFile)) ?? new(); }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(AppInfo.RestRoot);
        File.WriteAllText(AppInfo.SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class RestAccount
{
    public string Token { get; set; } = "";
    public string Name { get; set; } = "";
    public string Plan { get; set; } = "free";
}

internal sealed class MicrosoftAccount
{
    public string RefreshToken { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string MinecraftToken { get; set; } = "";
    public string Name { get; set; } = "";
    public string Uuid { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
}

internal static class SecureStore
{
    private static byte[] Entropy => SHA256.HashData(Encoding.UTF8.GetBytes("RestMC/RestStudio/account-store/v1"));
    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
        byte[] cipher = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllText(path, "dpapi:" + Convert.ToHexString(cipher).ToLowerInvariant());
    }
    public static T? Load<T>(string path)
    {
        try
        {
            string raw = File.ReadAllText(path).Trim();
            if (!raw.StartsWith("dpapi:", StringComparison.OrdinalIgnoreCase)) return default;
            byte[] cipher = Convert.FromHexString(raw[6..]);
            byte[] plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<T>(plain);
        }
        catch { return default; }
    }
    public static string SealText(string text)
    {
        byte[] cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(text), Entropy, DataProtectionScope.CurrentUser);
        return "dpapi:" + Convert.ToHexString(cipher).ToLowerInvariant();
    }
}

internal static class Net
{
    public static readonly HttpClient Http = Build();
    private static HttpClient Build()
    {
        var h = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });
        h.DefaultRequestHeaders.UserAgent.ParseAdd("RestMC-Launcher/1.6.7");
        h.Timeout = TimeSpan.FromMinutes(15);
        return h;
    }
    public static async Task<JsonNode> JsonAsync(HttpRequestMessage req, CancellationToken ct = default)
    {
        using var res = await Http.SendAsync(req, ct);
        string body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)res.StatusCode}: {body}");
        return JsonNode.Parse(body) ?? throw new InvalidOperationException("Empty JSON response");
    }
    public static async Task DownloadAsync(string url, string path, IProgress<(long done,long total)>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".part";
        using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        long total = res.Content.Headers.ContentLength ?? -1;
        await using var input = await res.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 128, true);
        byte[] buf = new byte[1024 * 128]; long done = 0; int n;
        while ((n = await input.ReadAsync(buf, ct)) > 0) { await output.WriteAsync(buf.AsMemory(0, n), ct); done += n; progress?.Report((done,total)); }
        output.Close();
        File.Move(tmp, path, true);
    }
}
