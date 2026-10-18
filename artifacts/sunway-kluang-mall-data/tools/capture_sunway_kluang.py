#!/usr/bin/env python3
"""Capture public store, event, and deal data from Sunway Kluang Mall."""

from __future__ import annotations

import csv
import hashlib
import html as html_lib
import json
import mimetypes
import re
import shutil
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen

from lxml import html


BASE_URL = "https://www.sunwaykluangmall.com"
API_BASE = BASE_URL + "/api/directory/live/v1/public/"
CALLER = "Alcassoft Fractals"
USER_AGENT = "MallIQ public-data capture/1.0"
OUTPUT_NAME = "sunway-kluang-mall-data"


def fetch(url: str, *, binary: bool = False, retries: int = 3):
    headers = {"User-Agent": USER_AGENT, "X-caller": CALLER}
    last_error = None
    for attempt in range(retries):
        try:
            with urlopen(Request(url, headers=headers), timeout=45) as response:
                data = response.read()
                content_type = response.headers.get_content_type()
                if binary:
                    return data, content_type
                charset = response.headers.get_content_charset() or "utf-8"
                return data.decode(charset, errors="replace")
        except (HTTPError, URLError, TimeoutError, ConnectionError) as exc:
            last_error = exc
            if attempt + 1 < retries:
                time.sleep(0.8 * (attempt + 1))
    raise RuntimeError(f"Failed to fetch {url}: {last_error}")


def fetch_json(url: str):
    return json.loads(fetch(url))


def write_text(path: Path, value: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8", newline="\n")


def write_json(path: Path, value):
    write_text(path, json.dumps(value, ensure_ascii=False, indent=2) + "\n")


def clean_text(value) -> str:
    if value is None:
        return ""
    if isinstance(value, str) and "<" in value:
        try:
            value = html.fromstring(f"<div>{value}</div>").text_content()
        except Exception:
            pass
    return re.sub(r"\s+", " ", html_lib.unescape(str(value))).strip()


def slugify(value: str) -> str:
    value = re.sub(r"[^a-zA-Z0-9._-]+", "-", value or "item").strip("-._")
    return value[:120] or "item"


def image_url(image_obj: dict | None, *, crop: bool = False) -> str:
    if not image_obj or not image_obj.get("_meta"):
        return ""
    meta = image_obj["_meta"]
    suffix = "w768-crop" if crop else "w768"
    return f"{meta.get('thumbsUrl', '')}{suffix}{meta.get('ext', '')}"


def extension_for(url: str, content_type: str) -> str:
    path_suffix = Path(urlparse(url).path).suffix
    if path_suffix and len(path_suffix) <= 6:
        return path_suffix.lower()
    return mimetypes.guess_extension(content_type) or ".bin"


class Capture:
    def __init__(self, out: Path):
        self.out = out
        self.downloads = []
        self.failures = []

    def download(self, url: str, target_stem: Path) -> str:
        if not url:
            return ""
        try:
            payload, content_type = fetch(url, binary=True)
            path = target_stem.with_suffix(extension_for(url, content_type))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
            rel = path.relative_to(self.out).as_posix()
            self.downloads.append({
                "url": url,
                "path": rel,
                "bytes": len(payload),
                "sha256": hashlib.sha256(payload).hexdigest(),
            })
            return rel
        except Exception as exc:
            self.failures.append({"url": url, "error": str(exc)})
            return ""


def get_all_stores(raw_dir: Path):
    docs = []
    offset = 0
    expected = None
    while expected is None or offset < expected:
        url = API_BASE + f"shops/list-paged?offset={offset}&limit=100"
        page = fetch_json(url)
        write_json(raw_dir / f"stores-offset-{offset}.json", page)
        docs.extend(page.get("docs", []))
        expected = int(page.get("total", len(docs)))
        received = len(page.get("docs", []))
        if received == 0:
            break
        offset += received
    unique = {item.get("id") or item.get("_id") or item.get("slug"): item for item in docs}
    return sorted(unique.values(), key=lambda x: (x.get("title") or "").casefold()), expected


def get_all_deals(raw_dir: Path):
    url = API_BASE + "promotions/lookup-category-paged/?offset=0&limit=100"
    page = fetch_json(url)
    write_json(raw_dir / "deals.json", page)
    return sorted(page.get("docs", []), key=lambda x: (x.get("dateStart") or "", x.get("title") or "")), int(page.get("total", 0))


def parse_listing_cards(page_html: str, section: str):
    tree = html.fromstring(page_html)
    cards = []
    seen = set()
    prefixes = (f"/{section}/",)
    for card in tree.xpath("//*[contains(concat(' ', normalize-space(@class), ' '), ' card ')]"):
        hrefs = card.xpath(".//a/@href")
        href = next((h for h in hrefs if h.startswith(prefixes)), "")
        if section == "events" and not href:
            href = next((h for h in hrefs if h == "/christmas-colourama"), "")
        if not href or href in seen:
            continue
        seen.add(href)
        title = clean_text(" ".join(card.xpath(".//h5//text() | .//h6//text()")))
        all_text = clean_text(card.text_content())
        date_match = re.search(r"\b\d{1,2}\s+[A-Z][a-z]{2}(?:\s+\d{4})?\s*-\s*\d{1,2}\s+[A-Z][a-z]{2}\s+\d{4}\b", all_text)
        if not date_match:
            date_match = re.search(r"\b[A-Z][a-z]{2}\s+\d{1,2}\s*-\s*[A-Z][a-z]{2}\s+\d{1,2}\b", all_text)
        image = next(iter(card.xpath(".//img/@data-src | .//img/@src")), "")
        cards.append({
            "title": title,
            "date_display": date_match.group(0) if date_match else "",
            "source_url": urljoin(BASE_URL, href),
            "slug": href.rstrip("/").split("/")[-1],
            "listing_image_url": image,
        })
    return cards


def parse_detail_page(page_html: str, source_url: str):
    tree = html.fromstring(page_html)
    title = clean_text(" ".join(tree.xpath("//section//h6[contains(@class,'font-weight-bold')][1]//text()")))
    if not title:
        title = clean_text(" ".join(tree.xpath("//title/text()"))).split(" | ")[0]
    detail_nodes = tree.xpath("//section//*[contains(concat(' ', normalize-space(@class), ' '), ' font-normal ')]")
    detail_text = clean_text(detail_nodes[0].text_content()) if detail_nodes else ""
    terms_nodes = tree.xpath("//*[@id='tncModal']//*[contains(concat(' ', normalize-space(@class), ' '), ' modal-body ')]")
    terms_text = clean_text(terms_nodes[0].text_content()) if terms_nodes else ""
    if terms_text and terms_text in detail_text:
        detail_text = detail_text.replace(terms_text, "").strip()
    images = []
    for value in tree.xpath("//section//img/@data-src | //section//img/@src"):
        if value and value.startswith("http") and value not in images:
            images.append(value)
    meta_description = "".join(tree.xpath("//meta[@name='description']/@content"))
    return {
        "title": title,
        "source_url": source_url,
        "description": clean_text(meta_description),
        "detail_text": detail_text,
        "terms_and_conditions": terms_text,
        "image_urls": images,
    }


def store_row(item: dict):
    categories = [x.get("title", "") for x in item.get("categoryTree", []) if x.get("title")]
    contact = item.get("contact") or {}
    return {
        "id": item.get("id") or item.get("_id") or "",
        "title": item.get("title", ""),
        "slug": item.get("slug", ""),
        "category": "; ".join(categories) or (item.get("category") or {}).get("title", ""),
        "level": item.get("level", ""),
        "venue": item.get("venue", ""),
        "description": clean_text(item.get("text", "")),
        "website": item.get("website", ""),
        "email": contact.get("email", ""),
        "phone": contact.get("phone", ""),
        "facebook": item.get("facebook", ""),
        "instagram": item.get("instagram", ""),
        "tiktok": item.get("tiktok", ""),
        "xiaohongshu": item.get("xiaohongshu", ""),
        "source_url": urljoin(BASE_URL, "/directory/" + item.get("slug", "")),
        "main_image_url": image_url(item.get("mainImage")),
        "logo_url": image_url(item.get("logo"), crop=True),
    }


def deal_row(item: dict):
    shop = item.get("shopMeta") or {}
    return {
        "id": item.get("id") or item.get("_id") or "",
        "title": item.get("title", ""),
        "slug": item.get("slug", ""),
        "date_start": item.get("dateStart", ""),
        "date_end": item.get("dateEnd", ""),
        "display_start": item.get("displayStart", ""),
        "shop": shop.get("title", ""),
        "shop_venue": shop.get("venue", ""),
        "description": clean_text(item.get("description") or item.get("text", "")),
        "terms": clean_text(item.get("term", "")),
        "source_url": urljoin(BASE_URL, "/promotions/" + item.get("slug", "")),
        "main_image_url": image_url(item.get("mainImage")),
    }


def write_csv(path: Path, rows: list[dict]):
    path.parent.mkdir(parents=True, exist_ok=True)
    fields = list(rows[0].keys()) if rows else ["source_url"]
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(rows)


def markdown_record(title: str, row: dict, body: str = ""):
    lines = [f"# {title}", ""]
    for key, value in row.items():
        if value in (None, "", []):
            continue
        label = key.replace("_", " ").title()
        if key.endswith("_url") or key == "source_url":
            lines.append(f"- {label}: [{value}]({value})")
        else:
            lines.append(f"- {label}: {value}")
    if body:
        lines.extend(["", "## Captured text", "", body])
    return "\n".join(lines).rstrip() + "\n"


def build_capture(root: Path):
    out = root / OUTPUT_NAME
    staging = root / (OUTPUT_NAME + ".staging")
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    capture = Capture(staging)
    captured_at = datetime.now(timezone.utc).replace(microsecond=0).isoformat()

    raw_dir = staging / "raw-api"
    sources_dir = staging / "source-pages"
    stores, store_total = get_all_stores(raw_dir)
    deals, deal_total = get_all_deals(raw_dir)

    event_html = fetch(BASE_URL + "/events")
    deal_html = fetch(BASE_URL + "/promotions")
    write_text(sources_dir / "events.html", event_html)
    write_text(sources_dir / "promotions.html", deal_html)
    event_cards = parse_listing_cards(event_html, "events")

    store_rows = []
    for item in stores:
        row = store_row(item)
        store_rows.append(row)
        item_dir = staging / "stores" / "by-store" / slugify(row["slug"] or row["title"])
        write_json(item_dir / "data.json", item)
        write_text(item_dir / "README.md", markdown_record(row["title"], row, row["description"]))
        row["main_image_file"] = capture.download(row["main_image_url"], item_dir / "images" / "main")
        row["logo_file"] = capture.download(row["logo_url"], item_dir / "images" / "logo")
        for index, alt in enumerate(item.get("alternativeImages", []), start=1):
            capture.download(image_url(alt), item_dir / "images" / f"alternative-{index:02d}")

    deal_rows = []
    for item in deals:
        row = deal_row(item)
        item_dir = staging / "deals" / "by-deal" / slugify(row["slug"] or row["title"])
        detail_html = ""
        try:
            detail_html = fetch(row["source_url"])
            write_text(item_dir / "source.html", detail_html)
        except Exception as exc:
            capture.failures.append({"url": row["source_url"], "error": str(exc)})
        write_json(item_dir / "data.json", item)
        write_text(item_dir / "README.md", markdown_record(row["title"], row, row["description"]))
        row["main_image_file"] = capture.download(row["main_image_url"], item_dir / "images" / "main")
        for index, alt in enumerate(item.get("alternativeImages", []), start=1):
            capture.download(image_url(alt), item_dir / "images" / f"alternative-{index:02d}")
        deal_rows.append(row)

    event_rows = []
    event_objects = []
    for card in event_cards:
        item_dir = staging / "events" / "by-event" / slugify(card["slug"] or card["title"])
        try:
            detail_html = fetch(card["source_url"])
            write_text(item_dir / "source.html", detail_html)
            detail = parse_detail_page(detail_html, card["source_url"])
        except Exception as exc:
            capture.failures.append({"url": card["source_url"], "error": str(exc)})
            detail = {"title": card["title"], "source_url": card["source_url"], "description": "", "detail_text": "", "terms_and_conditions": "", "image_urls": []}
        detail.update({"slug": card["slug"], "date_display": card["date_display"], "listing_image_url": card["listing_image_url"]})
        image_urls = detail.get("image_urls") or ([card["listing_image_url"]] if card["listing_image_url"] else [])
        image_files = []
        for index, image in enumerate(image_urls, start=1):
            image_files.append(capture.download(image, item_dir / "images" / f"image-{index:02d}"))
        detail["image_files"] = [x for x in image_files if x]
        write_json(item_dir / "data.json", detail)
        body = detail.get("detail_text", "")
        if detail.get("terms_and_conditions"):
            body += "\n\n## Terms and conditions\n\n" + detail["terms_and_conditions"]
        write_text(item_dir / "README.md", markdown_record(detail.get("title") or card["title"], {
            "slug": card["slug"], "date_display": card["date_display"], "source_url": card["source_url"], "description": detail.get("description", "")
        }, body))
        event_objects.append(detail)
        event_rows.append({
            "title": detail.get("title") or card["title"],
            "slug": card["slug"],
            "date_display": card["date_display"],
            "description": detail.get("description", ""),
            "detail_text": detail.get("detail_text", ""),
            "terms_and_conditions": detail.get("terms_and_conditions", ""),
            "source_url": card["source_url"],
            "image_urls": "; ".join(image_urls),
            "image_files": "; ".join(detail["image_files"]),
        })

    write_json(staging / "stores" / "stores.json", stores)
    write_csv(staging / "stores" / "stores.csv", store_rows)
    write_json(staging / "deals" / "deals.json", deals)
    write_csv(staging / "deals" / "deals.csv", deal_rows)
    write_json(staging / "events" / "events.json", event_objects)
    write_csv(staging / "events" / "events.csv", event_rows)

    manifest = {
        "source": BASE_URL + "/",
        "captured_at_utc": captured_at,
        "scope": "Public store directory, event listings, and deal/promotion listings",
        "counts": {
            "stores_api_total": store_total,
            "stores_captured": len(stores),
            "events_captured": len(event_rows),
            "deals_api_total": deal_total,
            "deals_captured": len(deals),
            "files_downloaded": len(capture.downloads),
            "download_failures": len(capture.failures),
        },
        "downloads": capture.downloads,
        "failures": capture.failures,
    }
    write_json(staging / "manifest.json", manifest)
    write_csv(staging / "download-manifest.csv", capture.downloads)
    write_json(staging / "download-failures.json", capture.failures)
    write_text(staging / "README.md", f"""# Sunway Kluang Mall public data capture

Captured from [{BASE_URL}/]({BASE_URL}/) at `{captured_at}`.

## Contents

- `stores/`: {len(stores)} public store records, CSV/JSON indexes, per-store details, and available images.
- `events/`: {len(event_rows)} events linked from the public events page, CSV/JSON indexes, page captures, extracted text, and images.
- `deals/`: {len(deals)} public deals/promotions, CSV/JSON indexes, per-deal page captures, details, and images.
- `raw-api/`: original public API responses used for the structured capture.
- `source-pages/`: source event and promotion listing HTML.
- `manifest.json`: counts, checksums, source URLs, and any failed downloads.

Dates and availability reflect the website at capture time. Copyright remains with the source owners; use the package in line with the source site's terms.
""")

    if out.exists():
        shutil.rmtree(out)
    staging.replace(out)
    archive = shutil.make_archive(str(root / OUTPUT_NAME), "zip", root_dir=out)
    return out, Path(archive), manifest


if __name__ == "__main__":
    target = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    output, archive, manifest = build_capture(target)
    print(json.dumps({"output": str(output), "archive": str(archive), "counts": manifest["counts"]}, ensure_ascii=False, indent=2))
