//go:build windows

package main

import (
    "embed"
    "fmt"
    "os"
    "os/exec"
    "path/filepath"
    "syscall"
    "unsafe"
)

//go:embed payload/RestMC.exe payload/Uninstall.exe
var payload embed.FS

var user32 = syscall.NewLazyDLL("user32.dll")
var shell32 = syscall.NewLazyDLL("shell32.dll")
var pMsg = user32.NewProc("MessageBoxW")
var pShell = shell32.NewProc("ShellExecuteW")

func w(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }
func message(body string, flags uintptr) uintptr {
    r, _, _ := pMsg.Call(0, uintptr(unsafe.Pointer(w(body))), uintptr(unsafe.Pointer(w("RestMC Setup"))), flags)
    return r
}
func psQuote(s string) string { return "'" + stringReplace(s, "'", "''") + "'" }
func stringReplace(s, a, b string) string {
    out := ""
    for {
        idx := -1
        for i := 0; i+len(a) <= len(s); i++ { if s[i:i+len(a)] == a { idx = i; break } }
        if idx < 0 { return out + s }
        out += s[:idx] + b
        s = s[idx+len(a):]
    }
}

func main() {
    if message("Install RestMC 1.6.7?\n\nThis installs the RestMC desktop launcher for the current Windows user. The launcher handles RestMC login, client install/update, Fabric checks and Minecraft launch.", 0x00000001|0x00000040) != 1 { return }
    local, app := os.Getenv("LOCALAPPDATA"), os.Getenv("APPDATA")
    home, _ := os.UserHomeDir()
    if local == "" || app == "" { message("Windows user folders could not be found.", 0x10); return }
    dir := filepath.Join(local, "Programs", "RestMC")
    if err := os.MkdirAll(dir, 0755); err != nil { message("Could not create RestMC install folder.\n\n"+err.Error(), 0x10); return }
    for _, name := range []string{"RestMC.exe", "Uninstall.exe"} {
        b, err := payload.ReadFile("payload/" + name)
        if err != nil { message("Installer payload is incomplete.", 0x10); return }
        if err = os.WriteFile(filepath.Join(dir, name), b, 0755); err != nil { message("Could not install "+name+".\n\n"+err.Error(), 0x10); return }
    }
    notice := "RestMC 1.6.7\r\nGPL-3.0-or-later\r\nSource: https://github.com/muzammilmonis/restmc\r\nBased on Arcanic Visuals; upstream notices are preserved in the source/client.\r\n"
    _ = os.WriteFile(filepath.Join(dir, "LICENSE-NOTICE.txt"), []byte(notice), 0644)
    exe, uninst := filepath.Join(dir, "RestMC.exe"), filepath.Join(dir, "Uninstall.exe")
    start := filepath.Join(app, "Microsoft", "Windows", "Start Menu", "Programs", "RestMC.lnk")
    desk := filepath.Join(home, "Desktop", "RestMC.lnk")
    script := fmt.Sprintf(`$ws=New-Object -ComObject WScript.Shell; $s=$ws.CreateShortcut(%s); $s.TargetPath=%s; $s.WorkingDirectory=%s; $s.Description='RestMC Launcher'; $s.Save(); $d=$ws.CreateShortcut(%s); $d.TargetPath=%s; $d.WorkingDirectory=%s; $d.Description='RestMC Launcher'; $d.Save()`, psQuote(start), psQuote(exe), psQuote(dir), psQuote(desk), psQuote(exe), psQuote(dir))
    _ = exec.Command("powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script).Run()
    key := `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\RestMC`
    reg := func(name, val string) { _ = exec.Command("reg", "add", key, "/v", name, "/t", "REG_SZ", "/d", val, "/f").Run() }
    _ = exec.Command("reg", "add", key, "/ve", "/t", "REG_SZ", "/d", "RestMC", "/f").Run()
    reg("DisplayName", "RestMC"); reg("DisplayVersion", "1.6.7"); reg("Publisher", "Rest Studio"); reg("InstallLocation", dir); reg("DisplayIcon", exe); reg("UninstallString", "\""+uninst+"\""); reg("URLInfoAbout", "https://mc.tiers.rest"); reg("URLUpdateInfo", "https://mc.tiers.rest")
    message("RestMC was installed successfully.\n\nThe launcher will open now.", 0x40)
    verb, target := w("open"), w(exe)
    pShell.Call(0, uintptr(unsafe.Pointer(verb)), uintptr(unsafe.Pointer(target)), 0, uintptr(unsafe.Pointer(w(dir))), 1)
}
