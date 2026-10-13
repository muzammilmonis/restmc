//go:build windows

package main

import (
    "os"
    "os/exec"
    "path/filepath"
    "syscall"
    "time"
    "unsafe"
)

var user32 = syscall.NewLazyDLL("user32.dll")
var msg = user32.NewProc("MessageBoxW")
func w(s string) *uint16 { p, _ := syscall.UTF16PtrFromString(s); return p }

func main() {
    r, _, _ := msg.Call(0, uintptr(unsafe.Pointer(w("Remove RestMC from this PC?\n\nYour Minecraft worlds and Microsoft/Minecraft account data will not be touched."))), uintptr(unsafe.Pointer(w("RestMC Uninstall"))), 0x00000004|0x00000020)
    if r != 6 { return }
    local, app := os.Getenv("LOCALAPPDATA"), os.Getenv("APPDATA")
    home, _ := os.UserHomeDir()
    install := filepath.Join(local, "Programs", "RestMC")
    if app != "" {
        _ = os.Remove(filepath.Join(app, "Microsoft", "Windows", "Start Menu", "Programs", "RestMC.lnk"))
        _ = os.Remove(filepath.Join(app, ".minecraft", "config", "restmc-account.txt"))
    }
    if home != "" { _ = os.Remove(filepath.Join(home, "Desktop", "RestMC.lnk")) }
    _ = exec.Command("reg", "delete", `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\RestMC`, "/f").Run()
    self, _ := os.Executable()
    entries, _ := os.ReadDir(install)
    for _, e := range entries { p := filepath.Join(install, e.Name()); if p != self { _ = os.RemoveAll(p) } }
    script := filepath.Join(os.TempDir(), "restmc-remove.cmd")
    _ = os.WriteFile(script, []byte("@echo off\r\nping 127.0.0.1 -n 3 >nul\r\ndel /f /q \""+self+"\" >nul 2>&1\r\nrmdir /s /q \""+install+"\" >nul 2>&1\r\ndel /f /q \"%~f0\"\r\n"), 0600)
    _ = exec.Command("cmd", "/c", "start", "", "/min", script).Start()
    time.Sleep(100 * time.Millisecond)
    msg.Call(0, uintptr(unsafe.Pointer(w("RestMC was removed."))), uintptr(unsafe.Pointer(w("RestMC"))), 0x40)
}
