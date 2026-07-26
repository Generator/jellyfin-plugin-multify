# Frequently Asked Questions

## MarkdownV2 Template Errors

### Why do I get "can't parse entities" when testing a Telegram notification?

Telegram's `MarkdownV2` parse mode treats certain characters as formatting markers. When these characters appear as literal text in your template, they must be **escaped** with a preceding `\` (backslash).

### Which characters need escaping?

The following characters are reserved in MarkdownV2 and must be escaped when used as literal text:

```
_ * [ ] ( ) ~ ` > # + - = | { } . !
```

### How do I fix my template?

**Wrong** (parentheses around year are literal, not formatting):

```
*Title:* {{ItemName}} ({{Year}})
```

**Right** (escape the parentheses):

```
*Title:* {{ItemName}} \({{Year}}\)
```

### Do I need to escape characters inside `{{variable}}` values?

**No.** Variable values (the content of `{{ItemName}}`, `{{Overview}}`, etc.) are automatically escaped before substitution. Only the **template body** — the literal text you write around the variables — needs manual escaping.

### Examples of common fixes

| Original (broken) | Fixed |
|-------------------|-------|
| `{{ItemName}} ({{Year}})` | `{{ItemName}} \({{Year}}\)` |
| `{{Client}} ({{DeviceName}})` | `{{Client}} \({{DeviceName}}\)` |
| `8.8/10 (Community)` | `8.8/10 \(Community\)` |
| `Rating: 8.8/10 \| 74/100` | `Rating: 8.8/10 \| 74/100` |
| `A > B > C` | `A \> B \> C` |
| `Don't forget!` | `Don't forget\!` |
| `42% complete (almost done)` | `42\% complete \(almost done\)` |

### What if my variable contains characters that break formatting?

This is handled automatically. For example, if `{{ItemName}}` is `"Inception (2010)"`, the parentheses inside the variable value are escaped by the plugin before being inserted into the template. You only need to escape the **template body itself**.

### My template works with `HTML` but not `MarkdownV2` — why?

`HTML` parse mode uses tags (`<b>`, `<i>`, `<a>`) and doesn't require escaping of punctuation. `MarkdownV2` uses punctuation characters as formatting markers. Switching to `HTML` is an option if you prefer not to deal with escaping.

---

## Telemetry / Debugging

### How do I see the actual Telegram error?

Check the Jellyfin server log (`docker/jellyfin/library/log/log_*.log`). The Telegram API error response body is logged with the full `description` field from Telegram. For example:

```
[ERR] Telegram sendMessage failed (400): "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: can't parse entities: Character '(' is reserved...\"}"
```

### Can I test my template without sending a real notification?

Yes. Use the **Test notification** button in the plugin configuration page. The test now uses a real library item from your server (or fallback data if the library is empty), so you can verify your template with realistic content.
