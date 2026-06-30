@echo off
REM © Mayanktaker Computers & Web Development | https://mayanktaker.com
REM Publish the Linux GTK binary (Wayland build, .NET 8 LTS).
set BUILD_VER=8.0.25

RMDIR /S /Q BIN

MKDIR BIN
MKDIR BIN\chrome-extension

REM IMPORTANT (Wayland/Fedora44): publish FRAMEWORK-DEPENDENT. A self-contained / trimmed
REM publish resolves a DIFFERENT (incompatible) GtkSharp/GLibSharp assembly set that crashes
REM at runtime on glib2 >= 2.88 with GLib.GType NRE. The framework-dependent publish uses
REM the correct *Sharp.dll versions. Users need the .NET 8 runtime installed (RPM already
REM `Requires: /usr/lib/dotnet` via dotnet-runtime-8.0).
dotnet publish -c Release -f net8.0 --self-contained false -p:PublishTrimmed=false ..\XDM.Gtk.UI\XDM.Gtk.UI.csproj -o BIN
