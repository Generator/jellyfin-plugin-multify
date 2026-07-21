# Multify API Documentation

## Test Notification Endpoint

Send test notifications to configured destinations.

### Endpoint

```
POST /Multify/TestNotification
```

### Authentication

Requires admin privileges (`RequiresElevation` policy). Include the Jellyfin access token in the `X-Emby-Token` header.

### Request Body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `destinationType` | string | Yes | Destination type: `telegram`, `gotify`, `ntfy`, or `generic` |
| `config` | object | Yes | Destination configuration (see schemas below) |

### Response

| Field | Type | Description |
|-------|------|-------------|
| `success` | boolean | Whether the test was successful |
| `errorMessage` | string? | Error message if failed |

---

## Destination Configurations

### Telegram

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `BotToken` | string | Yes | Bot token from @BotFather |
| `ChatId` | string | Yes | Chat ID for the target group/channel |
| `ParseMode` | string | No | `HTML`, `Markdown`, or `MarkdownV2` (default: `HTML`) |
| `MessageType` | integer | No | `0`=Text, `1`=Photo, `2`=Rich Message (default: `0`) |
| `MessageThreadId` | integer? | No | Forum topic ID (optional) |

**Example Request:**

```json
{
  "destinationType": "telegram",
  "config": {
    "BotToken": "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11",
    "ChatId": "-1001234567890",
    "ParseMode": "HTML",
    "MessageType": 0,
    "WebhookName": "My Telegram Bot",
    "EnableWebhook": true,
    "SendAllProperties": true,
    "Template": ""
  }
}
```

**Success Response:**

```json
{
  "success": true
}
```

**Error Response:**

```json
{
  "success": false,
  "errorMessage": "BotToken and ChatId are required for Telegram"
}
```

---

### Gotify

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `WebhookUri` | string | Yes | Gotify server URL (e.g., `https://gotify.example.com`) |
| `Token` | string | Yes | Application token |
| `Priority` | integer | No | Message priority (default: `0`) |

**Example Request:**

```json
{
  "destinationType": "gotify",
  "config": {
    "WebhookUri": "https://gotify.example.com",
    "Token": "your-app-token-here",
    "Priority": 5,
    "WebhookName": "My Gotify",
    "EnableWebhook": true,
    "SendAllProperties": true,
    "Template": ""
  }
}
```

---

### ntfy

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `WebhookUri` | string | Yes | ntfy server URL (e.g., `https://ntfy.sh`) |
| `Topic` | string | Yes | Topic name |
| `Priority` | integer | No | 1=Min, 2=Low, 3=Default, 4=High, 5=Max (default: `3`) |
| `EnableMarkdown` | boolean | No | Enable markdown formatting (default: `true`) |
| `AccessToken` | string? | No | Access token for private topics |

**Example Request:**

```json
{
  "destinationType": "ntfy",
  "config": {
    "WebhookUri": "https://ntfy.sh",
    "Topic": "my-jellyfin-notifications",
    "Priority": 3,
    "EnableMarkdown": true,
    "AccessToken": "",
    "WebhookName": "My ntfy",
    "EnableWebhook": true,
    "SendAllProperties": true,
    "Template": ""
  }
}
```

---

### Generic Webhook

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `WebhookUri` | string | Yes | Webhook URL (any HTTP endpoint) |
| `Headers` | array | No | Custom headers `[{Key, Value}]` |
| `Fields` | array | No | Custom fields merged into notification data `[{Key, Value}]` |

**Example Request:**

```json
{
  "destinationType": "generic",
  "config": {
    "WebhookUri": "https://hooks.example.com/notify",
    "Headers": [
      { "Key": "Authorization", "Value": "Bearer token123" }
    ],
    "Fields": [
      { "Key": "CustomField", "Value": "CustomValue" }
    ],
    "WebhookName": "My Webhook",
    "EnableWebhook": true,
    "SendAllProperties": true,
    "Template": ""
  }
}
```

---

## Common Error Messages

| Error | Cause |
|-------|-------|
| `Destination type is required.` | Missing or empty `destinationType` field |
| `Unsupported destination type: {type}` | Invalid `destinationType` value |
| `BotToken and ChatId are required for Telegram` | Missing Telegram credentials |
| `WebhookUri and Token are required for Gotify` | Missing Gotify server URL or token |
| `WebhookUri and Topic are required for ntfy` | Missing ntfy server URL or topic |
| `WebhookUri is required for generic webhook` | Missing webhook URL |
| `The HTTP request failed with status code: XXX` | Network or server error from destination |

---

## Template Variables

Test notifications use a simplified set of variables for testing purposes. For complete template variable reference, see [Template Variables](template-variables.md).

### Available Variables in Test Notifications

| Variable | Description | Example Value |
|----------|-------------|---------------|
| `{{Title}}` | Notification title | `Test Notification` |
| `{{Body}}` | Notification body | `This is a test message...` |
| `{{ServerName}}` | Server name | `Jellyfin Server` |
| `{{ItemType}}` | Item type | `Movie` |
| `{{ItemName}}` | Item name | `Test Movie (2024)` |
| `{{UserId}}` | User ID | `00000000-0000-0000-0000-000000000000` |
| `{{Timestamp}}` | Current UTC timestamp | `2024-01-15 10:30:00 UTC` |

### Example Template

```json
{
  "Template": "{{Title}}: {{ItemName}} ({{ItemType}})"
}
```

### Note

Test notifications do not include all production variables (such as `{{TrailerUrl}}`, `{{PrimaryImageUrl}}`, ratings, etc.). They use a minimal dataset to verify destination connectivity and configuration.

---

## cURL Examples

### Telegram

```bash
curl -X POST http://localhost:8098/Multify/TestNotification \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: YOUR_API_KEY" \
  -d '{
    "destinationType": "telegram",
    "config": {
      "BotToken": "YOUR_BOT_TOKEN",
      "ChatId": "YOUR_CHAT_ID",
      "ParseMode": "HTML",
      "MessageType": 0,
      "WebhookName": "Test",
      "EnableWebhook": true,
      "SendAllProperties": true
    }
  }'
```

### Gotify

```bash
curl -X POST http://localhost:8098/Multify/TestNotification \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: YOUR_API_KEY" \
  -d '{
    "destinationType": "gotify",
    "config": {
      "WebhookUri": "https://gotify.example.com",
      "Token": "YOUR_APP_TOKEN",
      "Priority": 5,
      "WebhookName": "Test",
      "EnableWebhook": true,
      "SendAllProperties": true
    }
  }'
```

### ntfy

```bash
curl -X POST http://localhost:8098/Multify/TestNotification \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: YOUR_API_KEY" \
  -d '{
    "destinationType": "ntfy",
    "config": {
      "WebhookUri": "https://ntfy.sh",
      "Topic": "my-topic",
      "Priority": 3,
      "WebhookName": "Test",
      "EnableWebhook": true,
      "SendAllProperties": true
    }
  }'
```

### Generic Webhook

```bash
curl -X POST http://localhost:8098/Multify/TestNotification \
  -H "Content-Type: application/json" \
  -H "X-Emby-Token: YOUR_API_KEY" \
  -d '{
    "destinationType": "generic",
    "config": {
      "WebhookUri": "https://hooks.example.com/notify",
      "WebhookName": "Test",
      "EnableWebhook": true,
      "SendAllProperties": true
    }
  }'
```
