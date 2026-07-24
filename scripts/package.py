#!/usr/bin/env python3
"""Build and package Multify releases for Jellyfin 10.12+."""
from __future__ import annotations

import argparse
import datetime
import hashlib
import json
import pathlib
import shutil
import subprocess
import sys
import zipfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
ARTIFACTS = ROOT / "artifacts"
CSPROJ = ROOT / "Jellyfin.Plugin.Multify.csproj"
# Note: This script is in scripts/, but CSPROJ is in the repo root

PLUGIN_GUID = "8e7a42b2-6a49-40e5-a05d-780ba1942cd1"
PLUGIN_NAME = "Multify"
PLUGIN_DESC = "A unified notification plugin for Jellyfin that sends notifications to multiple systems."
PLUGIN_OVERVIEW = "Sends notifications to Telegram, Gotify, ntfy, and generic webhooks with configurable event filtering, user filtering, library filtering, and customizable templates."
PLUGIN_CATEGORY = "Notifications"
PLUGIN_OWNER = "Generator"
PLUGIN_IMAGE_URL = "https://raw.githubusercontent.com/Generator/jellyfin-plugin-multify/main/assets/logo.png"

# Jellyfin target → (targetAbi, SDK)
JELLYFIN_TARGETS = {
    "10.12": ("10.12.0.0", "net9.0"),
}


def md5(path: pathlib.Path) -> str:
    h = hashlib.md5()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def run(cmd: list[str]) -> None:
    print(f"$ {' '.join(cmd)}", flush=True)
    subprocess.run(cmd, check=True)


def publish_jellyfin(jf_version: str, manifest_version: str, out_dir: pathlib.Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    run([
        "dotnet", "publish", str(CSPROJ),
        "-c", "Release",
        f"-p:Version={manifest_version}",
        "-o", str(out_dir),
        "--nologo",
    ])


def build_jellyfin_zip(
    jf_version: str,
    manifest_version: str,
    changelog: str,
    timestamp: str,
    target_abi: str,
    publish_dir: pathlib.Path,
) -> pathlib.Path:
    meta = {
        "category": PLUGIN_CATEGORY,
        "guid": PLUGIN_GUID,
        "name": PLUGIN_NAME,
        "description": PLUGIN_DESC,
        "overview": PLUGIN_OVERVIEW,
        "owner": PLUGIN_OWNER,
        "targetAbi": target_abi,
        "version": manifest_version,
        "changelog": changelog,
        "timestamp": timestamp,
    }
    zip_path = ARTIFACTS / f"Jellyfin.Plugin.Multify.zip"
    dll = publish_dir / "Jellyfin.Plugin.Multify.dll"
    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("meta.json", json.dumps(meta, indent=2))
        z.write(dll, dll.name)
    return zip_path


def update_manifest(entries: list[dict]) -> None:
    manifest_path = ROOT / "manifest.json"
    if manifest_path.exists():
        try:
            manifest = json.loads(manifest_path.read_text())
        except json.JSONDecodeError:
            manifest = []
    else:
        manifest = []

    if not manifest:
        manifest = [{
            "guid": PLUGIN_GUID,
            "name": PLUGIN_NAME,
            "overview": PLUGIN_OVERVIEW,
            "description": PLUGIN_DESC,
            "owner": PLUGIN_OWNER,
            "category": PLUGIN_CATEGORY,
            "imageUrl": PLUGIN_IMAGE_URL,
            "versions": [],
        }]

    plugin = manifest[0]
    existing = {v.get("version"): v for v in plugin.get("versions", [])}
    for entry in entries:
        existing[entry["version"]] = entry
    plugin["versions"] = sorted(existing.values(), key=lambda v: v.get("version", ""), reverse=True)
    manifest[0] = plugin

    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--version", required=True, help="Base plugin version (e.g. 0.0.3)")
    p.add_argument("--repo", default="", help="owner/repo for GitHub release URL")
    p.add_argument("--changelog", default="", help="Release notes blurb")
    p.add_argument(
        "--jellyfin",
        default="10.12",
        help="Comma-separated Jellyfin targets to build (default: 10.12).",
    )
    args = p.parse_args()

    base_version = args.version.lstrip("v")
    timestamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    if ARTIFACTS.exists():
        shutil.rmtree(ARTIFACTS)
    ARTIFACTS.mkdir()

    jf_requested = [v.strip() for v in args.jellyfin.split(",") if v.strip()]
    manifest_entries: list[dict] = []

    for jf_version in jf_requested:
        if jf_version not in JELLYFIN_TARGETS:
            print(f"!! unknown Jellyfin target: {jf_version} — skipping", file=sys.stderr)
            continue
        target_abi, _tfm = JELLYFIN_TARGETS[jf_version]
        manifest_version = base_version  # 3-part for single target

        publish_dir = ARTIFACTS / f"publish-jf-{jf_version}"
        publish_jellyfin(jf_version, manifest_version, publish_dir)

        zip_path = build_jellyfin_zip(
            jf_version, manifest_version, args.changelog, timestamp, target_abi, publish_dir,
        )
        checksum = md5(zip_path)
        print(f"  → {zip_path.name}  md5={checksum}  abi={target_abi}")

        if args.repo:
            manifest_entries.append({
                "version": manifest_version,
                "changelog": args.changelog,
                "targetAbi": target_abi,
                "sourceUrl": f"https://github.com/{args.repo}/releases/download/v{base_version}/{zip_path.name}",
                "checksum": checksum,
                "timestamp": timestamp,
            })

    if manifest_entries:
        update_manifest(manifest_entries)
        print(f"manifest.json: {len(manifest_entries)} entry(ies) added")
    else:
        print("manifest.json: skipped (no --repo or no Jellyfin targets)")

    return 0


if __name__ == "__main__":
    sys.exit(main())