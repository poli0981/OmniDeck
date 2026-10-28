#!/usr/bin/env python3
"""Post OmniDeck release announcements and CI failure alerts to Discord webhooks.

Standard library only, so the workflows need no setup step. Every input comes from environment
variables set by the calling workflow, never from `${{ }}` interpolated into a shell:

  release   GITHUB_REPOSITORY, GH_TOKEN, TAG, DISCORD_WEBHOOK
  failure   GITHUB_REPOSITORY, WORKFLOW_NAME, RUN_URL, BRANCH, COMMIT_SHA, COMMIT_MESSAGE, ACTOR,
            DISCORD_WEBHOOK

A missing webhook (forks, Dependabot) is a notice, not a failure. DRY_RUN=1 prints the payload
instead of posting it.
"""

from __future__ import annotations

import json
import os
import sys
import urllib.error
import urllib.parse
import urllib.request

COLOR_RELEASE = 0x57F287  # green
COLOR_PRERELEASE = 0xFEE75C  # yellow
COLOR_FAILURE = 0xED4245  # red

# Discord's CDN rejects Python's default user agent.
USER_AGENT = "OmniDeck-notify (+https://github.com/poli0981/OmniDeck)"


def env(name: str) -> str:
    return os.environ.get(name, "").strip()


def truncate(text: str, limit: int) -> str:
    return text if len(text) <= limit else text[: limit - 1].rstrip() + "…"


def compact(embed: dict) -> dict:
    return {key: value for key, value in embed.items() if value}


def get_release(repository: str, tag: str, token: str) -> dict:
    url = f"https://api.github.com/repos/{repository}/releases/tags/{urllib.parse.quote(tag, safe='')}"
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": USER_AGENT,
        "X-GitHub-Api-Version": "2022-11-28",
    }
    if token:
        headers["Authorization"] = f"Bearer {token}"
    with urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=20) as response:
        return json.load(response)


def release_payload() -> dict:
    repository, tag = env("GITHUB_REPOSITORY"), env("TAG")
    if not tag:
        raise ValueError("TAG is empty")
    release = get_release(repository, tag, env("GH_TOKEN"))
    prerelease = bool(release.get("prerelease"))
    fields = [{"name": "Type", "value": "Pre-release" if prerelease else "Stable", "inline": True}]
    setup = next((a for a in release.get("assets") or [] if a.get("name", "").endswith("-Setup.exe")), None)
    if setup:
        fields.append({"name": "Installer", "value": f"[{setup['name']}]({setup['browser_download_url']})", "inline": True})
    embed = {
        "title": f"🚀 OmniDeck {tag}" + (" (pre-release)" if prerelease else ""),
        "url": release.get("html_url"),
        "description": truncate(release.get("body") or "", 1500),
        "color": COLOR_PRERELEASE if prerelease else COLOR_RELEASE,
        "fields": fields,
        "footer": {"text": repository},
        "timestamp": release.get("published_at"),
    }
    return {"username": "Release Bot", "embeds": [compact(embed)]}


def failure_payload() -> dict:
    repository, sha = env("GITHUB_REPOSITORY"), env("COMMIT_SHA")
    message_lines = env("COMMIT_MESSAGE").splitlines()
    commit = f"[{sha[:7]}](https://github.com/{repository}/commit/{sha})" if sha else "?"
    embed = {
        "title": f"❌ {env('WORKFLOW_NAME') or 'Workflow'} failed: {repository}",
        "url": env("RUN_URL"),
        "description": truncate(message_lines[0], 300) if message_lines else "",
        "color": COLOR_FAILURE,
        "fields": [
            {"name": "Ref", "value": env("BRANCH") or "?", "inline": True},
            {"name": "Commit", "value": commit, "inline": True},
            {"name": "Actor", "value": env("ACTOR") or "?", "inline": True},
        ],
        "footer": {"text": repository},
    }
    return {"username": "CI Bot", "embeds": [compact(embed)]}


def post(webhook: str, payload: dict) -> None:
    request = urllib.request.Request(
        webhook,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json", "User-Agent": USER_AGENT},
        method="POST",
    )
    with urllib.request.urlopen(request, timeout=20):
        pass  # urlopen raises HTTPError for any non-2xx answer


def main(argv: list[str]) -> int:
    if len(argv) != 2 or argv[1] not in ("release", "failure"):
        print("usage: discord_notify.py release|failure", file=sys.stderr)
        return 2
    kind = argv[1]
    webhook = env("DISCORD_WEBHOOK")
    dry_run = env("DRY_RUN") == "1"
    if not webhook and not dry_run:
        print("::notice::No Discord webhook is available to this run; nothing was posted.")
        return 0
    try:
        payload = release_payload() if kind == "release" else failure_payload()
        if dry_run:
            print(json.dumps(payload, indent=2, ensure_ascii=False))
            return 0
        post(webhook, payload)
    except (urllib.error.URLError, ValueError, KeyError) as error:
        # Never print the webhook URL: it is a secret.
        print(f"::error::Discord {kind} notification failed: {error}")
        return 1
    print(f"Posted the {kind} notification to Discord.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
