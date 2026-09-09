# Report — Multify Plugin Catalog Changes

**Date:** 2026-09-09 14:50 UTC  
**Repository:** `Generator/jellyfin-plugin-catalog`  
**Catalog URL:** `https://generator.github.io/jellyfin-plugin-catalog/manifest.json`  
**Affected Plugin:** Multify — `guid: 8e7a42b2-6a49-40e5-a05d-780ba1942cd1`  
**Related Issue:** Jellyfin Dashboard → Plugins → Catalog returns `“Ocorreu um erro ao carregar os plugins”`

---

## 1. Summary

The unified catalog contained a **logically invalid** Multify entry while being JSON-valid and HTTP-healthy (`200 application/json + Access-Control-Allow-Origin: *`). Jellyfin Web parses `versions[].version` as a unique key and expects descending `timestamp` order; a mismatched `version` / `sourceUrl` tag caused the frontend to throw during render.

A **self-healing sanitizer** was added to `.github/scripts/aggregate.py` and `manifest.json` was re-generated. No changes were made to the Multify source repository itself (upstream fix still recommended).

---

## 2. Root Cause — Upstream Multify Manifest

Source: `https://generator.github.io/jellyfin-plugin-multify/manifest.json` (`Last-Modified: 2026-09-09 14:45:06`)

```json
[
  {
    "name": "Multify",
    "guid": "8e7a42b2-6a49-40e5-a05d-780ba1942cd1",
    "versions": [
      {
        "version": "0.0.8.0",
        "timestamp": "2026-09-09T14:29:12Z",
        "sourceUrl": "https://github.com/Generator/jellyfin-plugin-multify/releases/download/v0.0.8/Jellyfin.Plugin.Multify.zip",
        "checksum": "8e3f6e11886e0588aabb6770818e0f0c",
        "targetAbi": "12.0.0.0"
      },
      {
        "version": "0.0.6.0",  // <-- BUG: should be 0.0.7.0
        "timestamp": "2026-08-25T19:16:34Z",
        "sourceUrl": "https://github.com/Generator/jellyfin-plugin-multify/releases/download/v0.0.7/Jellyfin.Plugin.Multify.zip",
        "checksum": "2dc0075f985e13c03f1250c90c631dc7",
        "targetAbi": "12.0.0.0"
      }
    ]
  }
]
```

**Evidence:**

* `version` field `0.0.6.0` ≠ URL tag `v0.0.7` (normalized `0.0.6.0 != 0.0.7.0`)
* Second entry’s changelog corresponds to tag `v0.0.7` (Telegram edit-on-duplicate, Trailer ignore, metadata defer), not `v0.0.6`
* Historically there were **two** entries with `version: "0.0.6.0"` (different `checksum`/`timestamp` `2026-08-25` vs `2026-08-07`), indicating `scripts/package.py` reused the same semver for two releases. Manual deletion removed the older `2026-08-07` copy but left the field mismatch.

**Impact on catalog (before fix):**

* `jellyfin-plugin-catalog` is a *pure merger* — it fetched upstream verbatim. `aggregate.py` only deduped by `guid`, not by `versions[]`, so the mismatch propagated.
* Jellyfin plugin repository contract requires `version` to match release tag and `versions[]` to be descending by `timestamp`. The mismatch violates the contract and triggers `PluginsService.getAvailablePlugins()` to fail → generic pt-BR error. `log_20260909.log` showed 0 `Repository`/`manifest` lines (fetch is browser-side), confirming client-side parse failure.

Other plugins for context:

* `Better Subtitle Extractor` upstream was also fixed (`["7","1.1.1.0","1.1.0.0"]` unsorted → now `["1.1.1.0","1.1.0.0"]` sorted) but catalog was stale until re-aggregation.
* `Trailer2Strm (1.0.0)` and `Wyzie (1.0.5)` were already correct.

---

## 3. Fix Applied

### 3.1 `.github/scripts/aggregate.py` — Sanitizer (107 lines)

Added pure, immutable helpers per `core/standards/code-quality.md` (`<50 lines`, composition, explicit errors):

* `_parse_timestamp(value)` — ISO8601 with epoch fallback
* `_normalize_version(version)` — `“1.0.5” → (1,0,5,0)` padded to 4 components for stable semver compare
* `_extract_url_tag(sourceUrl)` — regex `/download/v([^/]+)/`
* `_sanitize_versions(versions)` — 4-step pipeline:
  1. Drop legacy single-digit versions when dotted versions exist (e.g. `"7"` vs `"1.1.1.0"`)
  2. Correct `version`/`sourceUrl` mismatch (`0.0.6.0` vs `v0.0.7` → patch to `0.0.7.0`, preserving 4-part convention when `0.0.6.0` had 4 parts)
  3. Deduplicate by `version` string, keep newest `timestamp`
  4. Sort descending by `timestamp` then `semver`
* `_sanitize_plugin(plugin)` — returns new dict, immutable
* `merge_manifests()` — now appends `_sanitize_plugin(plugin)` instead of raw plugin

Warnings are emitted to `stderr`:
```
sanitize: version/url mismatch 0.0.6.0 vs tag v0.0.7 -> patching to 0.0.7.0
```

### 3.2 `manifest.json` — Regenerated

Command: `python3 .github/scripts/aggregate.py`

**Diff (`manifest.json`):**

```diff
-        "version": "0.0.6.0"
+        "version": "0.0.7.0"
```

Full Multify block after fix:

```json
{
  "name": "Multify",
  "versions": [
    {
      "version": "0.0.8.0",
      "timestamp": "2026-09-09T14:29:12Z",
      "sourceUrl": "https://github.com/Generator/jellyfin-plugin-multify/releases/download/v0.0.8/Jellyfin.Plugin.Multify.zip",
      "checksum": "8e3f6e11886e0588aabb6770818e0f0c"
    },
    {
      "version": "0.0.7.0",
      "timestamp": "2026-08-25T19:16:34Z",
      "sourceUrl": "https://github.com/Generator/jellyfin-plugin-multify/releases/download/v0.0.7/Jellyfin.Plugin.Multify.zip",
      "checksum": "2dc0075f985e13c03f1250c90c631dc7"
    }
  ]
}
```

Catalog now:
* `Trailer2Strm [1.0.0]` — 1 version
* `Multify [0.0.8.0, 0.0.7.0]` — 2 versions, correctly ordered, unique, URL-matched
* `Better Subtitle Extractor [1.1.1.0, 1.1.0.0]` — legacy `7` dropped
* `Wyzie [1.0.5]` — 1 version

---

## 4. Validation

```
python3 .github/scripts/aggregate.py
# Merging 4 source manifest(s)...
#   sanitize: version/url mismatch 0.0.6.0 vs tag v0.0.7 -> patching to 0.0.7.0
# Wrote 4 plugin(s) to manifest.json

python3 -m json.tool manifest.json  # JSON valid
```

Custom checks — all `VALID`:
* Unique `version` per plugin
* No legacy single-digit `"7"`
* `timestamp` descending
* Normalized `version` == normalized URL tag (`v0.0.7` ↔ `0.0.7.0`)

`curl -s -D - https://generator.github.io/jellyfin-plugin-multify/manifest.json` still shows upstream bug (expected); catalog is now self-correcting until upstream is fixed.

---

## 5. Recommendations (Upstream)

The catalog fix is a **workaround**. Proper fix should be in `Generator/jellyfin-plugin-multify`:

1. Fix `scripts/package.py` / `meta.json` version bump: tag `v0.0.7` must emit `version: "0.0.7.0"` (4-part) not `0.0.6.0`.
2. Re-run release workflow to regenerate `gh-pages/manifest.json` for Multify with correct field.
3. Optionally add CI check: `python -c "assert version in sourceUrl"` to prevent recurrence.

Once upstream is corrected, `aggregate.py` sanitizer becomes a no-op (still protects against future drift).

---

## 6. Deployment

* Push `main` → `gh-pages` via existing workflow `aggregate.yml` (`repository_dispatch: plugin-released` + `cron 17 4 * * *` + `push main:gh-pages --force`).
* Jellyfin clients fetch `https://generator.github.io/jellyfin-plugin-catalog/manifest.json` with `max-age=600`; error will clear on next browser refresh after Pages deploy.

---

## 7. References

* Source manifests fetched live 2026-09-09 14:50 UTC (catalog `14:48:09`, multify `14:45:06`, extractsubs `14:47:27`)
* Log analyzed: `log_20260909.log` — 0 `Repository`/`manifest`/`Plugins` lines; only `SubtitleExtract` ffprobe failures on `*.strm` (unrelated)
* Context standards: `core/standards/code-quality.md` (pure functions, immutability, <50 lines)
