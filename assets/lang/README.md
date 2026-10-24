# Launcher translations

Each `<code>.txt` file in this folder is one language. `build.bat` builds every one of them into the exe, and they show up in **Options → Language**. When no language has been picked, the launcher uses the Windows display language if there is a file for it, and English if there isn't.

Included: Čeština (cs), Dansk (da), Deutsch (de), Ελληνικά (el), Español (es), Suomi (fi), Français (fr), हिन्दी (hi), Magyar (hu), Bahasa Indonesia (id), Italiano (it), 日本語 (ja), 한국어 (ko), Norsk bokmål (nb), Nederlands (nl), Polski (pl), Português do Brasil (pt-BR), Română (ro), Русский (ru), Svenska (sv), ไทย (th), Türkçe (tr), Українська (uk), Tiếng Việt (vi), 简体中文 (zh-CN), 繁體中文 (zh-TW).

The translations were machine-made. If you speak one of these languages, corrections are very welcome.

Right-to-left languages (Arabic, Hebrew, Persian) are not included. They would need the whole layout mirrored, not just new text.

## File format

```
@name Deutsch                        the language's own name, shown in Options
@font Palatino Linotype, Georgia     optional: fonts for this language (the first one installed wins)
# a comment
Press PLAY to enter Azeroth.         the English text, exactly as the launcher shows it ...
= Drücke SPIELEN, um Azeroth zu betreten.     ... and its translation on the next line
```

- `{0}`, `{1}` ... are filled in by the launcher (names, numbers, paths). Keep every one of them, but you can move them around in the sentence.
- `\n` is a line break.
- Any text that has no translation is shown in English, so a partial file still works.
- Patch notes, the About page and addon names are never translated.
- When a text mentions a button (PLAY, INSTALL, UPDATE, USE CLOUD ...), use the same word the button uses in that language.

## Adding a language

1. Copy `de.txt` to `<code>.txt`, using the language code Windows uses (for example `pt-BR`, `zh-TW`, or `ca` for Catalan).
2. Change `@name`, and also `@font` if the language needs a font with its own script.
3. Replace each `= ...` line with your translation.
4. Run `build.bat`. The new language appears in Options.
