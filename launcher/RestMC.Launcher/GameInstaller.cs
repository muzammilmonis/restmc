using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RestMCLauncher;

internal sealed class GameInstaller
{
    private readonly LauncherSettings settings;
    public GameInstaller(LauncherSettings s) => settings = s;
    public async Task<string> EnsureJavaAsync(Action<string> status, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(settings.JavaPath) && File.Exists(settings.JavaPath)) return settings.JavaPath;
        string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME"); if (!string.IsNullOrWhiteSpace(javaHome)) { string p=Path.Combine(javaHome,"bin","javaw.exe"); if(File.Exists(p)){settings.JavaPath=p;settings.Save();return p;} }
        string runtimeRoot=Path.Combine(AppInfo.RestRoot,"runtime"); string existing=Directory.Exists(runtimeRoot)?Directory.EnumerateFiles(runtimeRoot,"javaw.exe",SearchOption.AllDirectories).FirstOrDefault()??"":""; if(File.Exists(existing)){settings.JavaPath=existing;settings.Save();return existing;}
        status("Downloading Java 21 runtime…"); string zip=Path.Combine(AppInfo.RestRoot,"runtime.zip"); await Net.DownloadAsync("https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jre/hotspot/normal/eclipse?project=jdk",zip,null,ct); if(Directory.Exists(runtimeRoot))Directory.Delete(runtimeRoot,true);Directory.CreateDirectory(runtimeRoot);ZipFile.ExtractToDirectory(zip,runtimeRoot,true);File.Delete(zip); string found=Directory.EnumerateFiles(runtimeRoot,"javaw.exe",SearchOption.AllDirectories).FirstOrDefault()??throw new InvalidOperationException("Java runtime did not contain javaw.exe");settings.JavaPath=found;settings.Save();return found;
    }
    public async Task EnsureRestMcModAsync(Action<string> status, CancellationToken ct)
    {
        string mods=Path.Combine(settings.GameDirectory,"mods");Directory.CreateDirectory(mods);status("Updating RestMC client…");string shaText=(await Net.Http.GetStringAsync(AppInfo.ClientSha,ct)).Trim();string expected=shaText.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();string target=Path.Combine(mods,$"RestMC-{AppInfo.Version}.jar");bool ok=File.Exists(target)&&Hash(target)==expected;if(!ok){await Net.DownloadAsync(AppInfo.ClientJar,target,null,ct);if(Hash(target)!=expected){File.Delete(target);throw new InvalidOperationException("RestMC client SHA-256 verification failed.");}}
        foreach(string f in Directory.EnumerateFiles(mods,"*.jar")){string n=Path.GetFileName(f);if(!string.Equals(f,target,StringComparison.OrdinalIgnoreCase)&&(n.StartsWith("RestMC-",StringComparison.OrdinalIgnoreCase)||n.StartsWith("arcanicvisuals",StringComparison.OrdinalIgnoreCase)))try{File.Delete(f);}catch{}}
        await EnsureModrinthAsync("fabric-api","fabric-api",status,ct); await EnsureModrinthAsync("sodium","sodium",status,ct);
    }
    private async Task EnsureModrinthAsync(string project,string prefix,Action<string> status,CancellationToken ct)
    {
        string mods=Path.Combine(settings.GameDirectory,"mods"); if(Directory.EnumerateFiles(mods,"*.jar").Any(x=>Path.GetFileName(x).StartsWith(prefix,StringComparison.OrdinalIgnoreCase)))return;status($"Installing {project}…");string q=$"https://api.modrinth.com/v2/project/{project}/version?loaders=%5B%22fabric%22%5D&game_versions=%5B%22{AppInfo.MinecraftVersion}%22%5D";var arr=JsonNode.Parse(await Net.Http.GetStringAsync(q,ct))!.AsArray();var ver=arr.FirstOrDefault()??throw new InvalidOperationException($"No {project} build for {AppInfo.MinecraftVersion}");var file=ver!["files"]!.AsArray().FirstOrDefault(x=>x?["primary"]?.GetValue<bool>()==true)??ver["files"]![0]!;string url=file["url"]!.GetValue<string>();string name=file["filename"]!.GetValue<string>();await Net.DownloadAsync(url,Path.Combine(mods,name),null,ct);
    }
    public async Task<GameLaunch> PrepareGameAsync(MicrosoftAccount account, Action<string> status, CancellationToken ct)
    {
        Directory.CreateDirectory(settings.GameDirectory);string java=await EnsureJavaAsync(status,ct);await EnsureRestMcModAsync(status,ct);status("Reading Minecraft manifest…");var manifest=JsonNode.Parse(await Net.Http.GetStringAsync("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",ct))!;var v=manifest["versions"]!.AsArray().FirstOrDefault(x=>x?["id"]?.GetValue<string>()==AppInfo.MinecraftVersion)??throw new InvalidOperationException("Minecraft version not found.");var vanilla=JsonNode.Parse(await Net.Http.GetStringAsync(v!["url"]!.GetValue<string>(),ct))!;
        string versions=Path.Combine(settings.GameDirectory,"versions",AppInfo.MinecraftVersion);Directory.CreateDirectory(versions);string client=Path.Combine(versions,AppInfo.MinecraftVersion+".jar");if(!File.Exists(client)){status("Downloading Minecraft client…");await Net.DownloadAsync(vanilla["downloads"]!["client"]!["url"]!.GetValue<string>(),client,null,ct);}string assetsDir=Path.Combine(settings.GameDirectory,"assets");string libsDir=Path.Combine(settings.GameDirectory,"libraries");string nativesDir=Path.Combine(versions,"natives");Directory.CreateDirectory(nativesDir);
        var classpath=new List<string>();foreach(var lib in vanilla["libraries"]!.AsArray()){if(lib is null||!RulesAllow(lib["rules"]))continue;var artifact=lib["downloads"]?["artifact"];if(artifact is not null){string p=Path.Combine(libsDir,artifact["path"]!.GetValue<string>().Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(p))await Net.DownloadAsync(artifact["url"]!.GetValue<string>(),p,null,ct);classpath.Add(p);}string? nativeKey=lib["natives"]?["windows"]?.GetValue<string>();if(!string.IsNullOrEmpty(nativeKey)){nativeKey=nativeKey.Replace("${arch}",Environment.Is64BitOperatingSystem?"64":"32");var c=lib["downloads"]?["classifiers"]?[nativeKey];if(c is not null){string p=Path.Combine(libsDir,c["path"]!.GetValue<string>().Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(p))await Net.DownloadAsync(c["url"]!.GetValue<string>(),p,null,ct);ExtractNatives(p,nativesDir);}}}
        status("Downloading assets…");var ai=vanilla["assetIndex"]!;string assetIndexId=ai["id"]!.GetValue<string>();string idxPath=Path.Combine(assetsDir,"indexes",assetIndexId+".json");if(!File.Exists(idxPath))await Net.DownloadAsync(ai["url"]!.GetValue<string>(),idxPath,null,ct);var idx=JsonNode.Parse(File.ReadAllText(idxPath))!;foreach(var kv in idx["objects"]!.AsObject()){string hash=kv.Value!["hash"]!.GetValue<string>();string p=Path.Combine(assetsDir,"objects",hash[..2],hash);if(!File.Exists(p))await Net.DownloadAsync($"https://resources.download.minecraft.net/{hash[..2]}/{hash}",p,null,ct);}
        status("Installing Fabric loader…");string loader=settings.FabricLoader;if(string.IsNullOrWhiteSpace(loader)){var la=JsonNode.Parse(await Net.Http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{AppInfo.MinecraftVersion}",ct))!.AsArray();loader=la.FirstOrDefault(x=>x?["loader"]?["stable"]?.GetValue<bool>()==true)?["loader"]?["version"]?.GetValue<string>()??la[0]!["loader"]!["version"]!.GetValue<string>();settings.FabricLoader=loader;settings.Save();}var fabric=JsonNode.Parse(await Net.Http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{AppInfo.MinecraftVersion}/{loader}/profile/json",ct))!;foreach(var lib in fabric["libraries"]!.AsArray()){if(lib is null)continue;string name=lib["name"]!.GetValue<string>();string baseUrl=(lib["url"]?.GetValue<string>()??"https://maven.fabricmc.net/").TrimEnd('/')+"/";string rel=MavenPath(name);string p=Path.Combine(libsDir,rel.Replace('/',Path.DirectorySeparatorChar));if(!File.Exists(p))await Net.DownloadAsync(baseUrl+rel,p,null,ct);classpath.Add(p);}classpath.Add(client);string mainClass=fabric["mainClass"]!.GetValue<string>();
        var args=new List<string>{"--username",account.Name,"--version",$"fabric-loader-{loader}-{AppInfo.MinecraftVersion}","--gameDir",settings.GameDirectory,"--assetsDir",assetsDir,"--assetIndex",assetIndexId,"--uuid",account.Uuid,"--accessToken",account.MinecraftToken,"--clientId",AppInfo.MicrosoftClientId,"--userType","msa","--versionType","release"};
        return new GameLaunch(java,mainClass,string.Join(Path.PathSeparator,classpath.Distinct(StringComparer.OrdinalIgnoreCase)),nativesDir,args);
    }
    public Process Launch(GameLaunch g)
    {
        var psi=new ProcessStartInfo(g.Java){WorkingDirectory=settings.GameDirectory,UseShellExecute=false};psi.ArgumentList.Add($"-Xms512M");psi.ArgumentList.Add($"-Xmx{Math.Max(2048,settings.RamMb)}M");psi.ArgumentList.Add("-Djava.library.path="+g.Natives);psi.ArgumentList.Add("-Drestmc.api="+AppInfo.Api);psi.ArgumentList.Add("-Drestmc.launcher="+Environment.ProcessPath);psi.ArgumentList.Add("-cp");psi.ArgumentList.Add(g.Classpath);psi.ArgumentList.Add(g.MainClass);foreach(string a in g.GameArgs)psi.ArgumentList.Add(a);return Process.Start(psi)??throw new InvalidOperationException("Minecraft failed to start.");
    }
    private static bool RulesAllow(JsonNode? rules){if(rules is null)return true;bool allow=false;foreach(var r in rules.AsArray()){if(r is null)continue;bool match=true;string? os=r["os"]?["name"]?.GetValue<string>();if(!string.IsNullOrEmpty(os)&&os!="windows")match=false;if(match)allow=r["action"]?.GetValue<string>()=="allow";}return allow;}
    private static void ExtractNatives(string jar,string dir){using var z=ZipFile.OpenRead(jar);foreach(var e in z.Entries){if(string.IsNullOrEmpty(e.Name)||e.FullName.StartsWith("META-INF/",StringComparison.OrdinalIgnoreCase))continue;string p=Path.Combine(dir,e.Name);Directory.CreateDirectory(Path.GetDirectoryName(p)!);e.ExtractToFile(p,true);}}
    private static string MavenPath(string name){var p=name.Split(':');string group=p[0].Replace('.','/'),art=p[1],ver=p[2];return $"{group}/{art}/{ver}/{art}-{ver}.jar";}
    private static string Hash(string f){using var s=File.OpenRead(f);return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();}
}
internal sealed record GameLaunch(string Java,string MainClass,string Classpath,string Natives,List<string> GameArgs);
