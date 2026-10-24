AZEROTH SURVIVORS LAUNCHER - C# VERSION
=======================================

This is the same launcher as wow_launcher.py (same look, same buttons, same cloud sync, addons, changelog,
setup, tray icon and UPDATE button), rewritten in C#. It builds into ONE small exe (well under 1 MB plus the
patch notes) that runs on the .NET Framework 4.8 that every Windows 10 and 11 PC already has.

Why: the Python exe was flagged because PyInstaller wraps every program in the same self-unpacking starter
that a lot of real malware also uses. This exe has no wrapper, no packer and unpacks nothing at start.

BUILD
  Double-click build.bat. Nothing needs to be installed: it uses the C# compiler that is part of Windows.
  The exe appears in the "dist" folder. If the build shows errors, copy them and send them over.

WHAT TO EDIT (then run build.bat again)
  assets\changelog.txt   the patch notes (posts separated by a line with only === on it)
  assets\addons.txt      the Download More list (one addon per line, explained at the top of the file)
  assets\about.txt       the About Launcher page
  src\Update.cs          ManifestUrl = the https address of update.json, once you have picked a host
  src\Launcher.cs        RequireLauncherInstall (the temporary "install with this launcher first" rule),
                         ClientUrl (where INSTALL sends players), DiscordUrl

GAME UPDATES
  make_update.bat builds update.json for a new game version (see make_update.py for details).

SIGNING (removes the remaining warnings for good)
  Make a sign_config.bat as explained at the top of sign_build.bat; build.bat then signs the exe by itself.

SETTINGS
  Players keep everything: this version reads and writes the same files as the Python launcher
  (%APPDATA%\WoWLauncher\config.json and %LOCALAPPDATA%\WoWLauncher\state.json), and the cloud sync
  folder format is unchanged (AzerothSync compatible).
