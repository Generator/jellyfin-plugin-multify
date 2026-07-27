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

## Telegram Rich Messages (SendRichMessage)

### How do I include an image in a rich message?

Use the `<img src="url"/>` HTML tag **inline** in your template. Telegram's `sendRichMessage` supports mixing Markdown and HTML in the same message, so you can use `**bold**` alongside `<img>` tags.

**Wrong** (Markdown inline image syntax — not supported by Telegram):
```markdown
**Nova Temporada Adicionada**
![{{SeriesName}}]({{TmdbPosterUrl}})
```

**Right** (HTML `<img>` tag — renders as a photo block):
```markdown
**Nova Temporada Adicionada**

<img src="{{TmdbPosterUrl}}"/>

**Série TV:** {{SeriesName}}
```

### Can I mix Markdown and HTML in the same rich message?

**Yes.** Telegram's API explicitly supports this. The `<img>` tag uses HTML, while everything else (bold, italic, links) can use Markdown:

```markdown
**Bold text** and *italic text*

<img src="{{TmdbPosterUrl}}"/>

[Open in Jellyfin]({{ItemUrl}})
```

### Why doesn't `![alt](url)` work for images?

Telegram's rich message parser does **not** support Markdown image syntax (`![alt](url)`). Using it will cause a `can't parse entities: Invalid tg://emoji` error. Always use `<img src="url"/>` instead.

### What HTML tags are available for rich messages?

| Tag | Purpose | Example |
|-----|---------|---------|
| `<img src="url"/>` | Inline photo | `<img src="{{TmdbPosterUrl}}"/>` |
| `<b>` or `<strong>` | Bold | `<b>text</b>` |
| `<i>` or `<em>` | Italic | `<i>text</i>` |
| `<u>` or `<ins>` | Underline | `<u>text</u>` |
| `<s>` or `<strike>` | Strikethrough | `<s>text</s>` |
| `<code>` | Monospace | `<code>text</code>` |
| `<a href="url">` | Link | `<a href="{{ItemUrl}}">Open</a>` |
| `<tg-emoji emoji-id="..."/>` | Custom emoji | `<tg-emoji emoji-id="5368324170671202286"/>` |

### What's the difference between SendPhoto and SendRichMessage for images?

- **SendPhoto**: Sends a **standalone photo** with a caption below it. The image is the primary content; the caption is secondary text.
- **SendRichMessage**: Embeds the image **inside formatted text** as a block. The image appears between paragraphs, alongside bold/italic/links.

For a "Season Added" notification where the image is part of a richly formatted message, **SendRichMessage** with `<img src="..."/>` gives the best result.

---

## Telemetry / Debugging

### How do I see the actual Telegram error?

Check the Jellyfin server log (`docker/jellyfin/library/log/log_*.log`). The Telegram API error response body is logged with the full `description` field from Telegram. For example:

```
[ERR] Telegram sendMessage failed (400): "{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request: can't parse entities: Character '(' is reserved...\"}"
```

### Can I test my template without sending a real notification?

Yes. Use the **Test notification** button in the plugin configuration page. The test now uses a real library item from your server (or fallback data if the library is empty), so you can verify your template with realistic content.
