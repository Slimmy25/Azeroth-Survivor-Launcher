DoomWithin for Azeroth Survivors (1.12 client)

DoomWithin v1.2.1 by tna0y and vladvis (https://doomwiki.org/wiki/Doom_Within), GPLv2 - see COPYING.
Includes the Doom shareware data built into the memory image (doomgeneric.lua).

Changes for this client (by Slimjim, 2026):
- Lua 5.0: % -> DWMod (floored modulo), # -> table.getn, hex numbers above 32 bits written in decimal
- 1.12 API: new frame.lua (pixels placed by hand), SetScript handlers use this/arg1,
  DWCompat.lua replaces RunNextFrame, C_Timer.After, print and debugprofilestop
- Interpreter only: the AOT chunk and the unused JIT files are left out

Start it with /dw start, stop with /dw stop.
