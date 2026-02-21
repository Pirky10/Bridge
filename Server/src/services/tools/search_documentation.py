"""
Unity documentation search using web scraping.
Searches Unity docs and Scripting API via DuckDuckGo site-restricted search.
"""
import re
import urllib.parse
from typing import Annotated, Any

import httpx
from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool


@mcp_for_unity_tool(
    description=(
        "Searches Unity documentation and Scripting API. Uses free web scraping "
        "(no API key). Can search Unity Manual, Scripting API, Unity Forums, "
        "and Unity Learn. Returns relevant doc pages with summaries."
    ),
    annotations=ToolAnnotations(
        title="Search Documentation",
        readOnlyHint=True,
    ),
)
async def search_documentation(
    ctx: Context,
    query: Annotated[str, "Search query (e.g. 'Rigidbody.AddForce', 'how to use NavMesh')"],
    source: Annotated[str, "Source: manual, scripting_api, forums, learn, all"] | None = None,
    unity_version: Annotated[str, "Unity version (e.g., '2022.3', '6')"] | None = None,
    fetch_content: Annotated[bool, "Fetch full page content of the top result"] | None = None,
) -> dict[str, Any]:
    search_source = (source or "all").lower()
    should_fetch = fetch_content if fetch_content is not None else False

    # Build site-restricted query
    site_map = {
        "manual": "docs.unity3d.com/Manual",
        "scripting_api": "docs.unity3d.com/ScriptReference",
        "forums": "discussions.unity.com",
        "learn": "learn.unity.com",
    }

    if search_source in site_map:
        full_query = f"site:{site_map[search_source]} {query}"
    elif search_source == "all":
        full_query = f"site:docs.unity3d.com OR site:discussions.unity.com {query}"
    else:
        full_query = f"Unity {query}"

    try:
        results = await _search_ddg(full_query, max_results=8)

        # Optionally fetch the top result's content
        fetched_content = None
        if should_fetch and results:
            try:
                fetched_content = await _fetch_page_content(results[0]["url"])
            except Exception:
                pass

        return {
            "success": True,
            "message": f"Found {len(results)} documentation results for '{query}'",
            "data": {
                "query": query,
                "source": search_source,
                "results": results,
                "top_result_content": fetched_content,
            },
        }
    except Exception as e:
        return {"success": False, "message": f"Search failed: {e}"}


async def _search_ddg(query: str, max_results: int) -> list[dict]:
    """Scrape DuckDuckGo HTML search results."""
    encoded = urllib.parse.quote_plus(query)
    url = f"https://html.duckduckgo.com/html/?q={encoded}"

    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        ),
        "Accept": "text/html",
        "Accept-Language": "en-US,en;q=0.5",
    }

    async with httpx.AsyncClient(follow_redirects=True, timeout=15.0) as client:
        resp = await client.get(url, headers=headers)
        resp.raise_for_status()
        html = resp.text

    results = []

    links = re.findall(
        r'<a[^>]*class="result__a"[^>]*href="([^"]*)"[^>]*>(.*?)</a>',
        html, re.DOTALL
    )
    snippets = re.findall(
        r'<a[^>]*class="result__snippet"[^>]*>(.*?)</a>',
        html, re.DOTALL
    )

    for i, (link, title) in enumerate(links[:max_results]):
        clean_title = re.sub(r'<[^>]+>', '', title).strip()
        clean_snippet = ""
        if i < len(snippets):
            clean_snippet = re.sub(r'<[^>]+>', '', snippets[i]).strip()

        actual_url = _extract_url(link)

        # Categorize the result
        category = "other"
        if "docs.unity3d.com/ScriptReference" in actual_url:
            category = "scripting_api"
        elif "docs.unity3d.com/Manual" in actual_url:
            category = "manual"
        elif "discussions.unity.com" in actual_url or "forum.unity.com" in actual_url:
            category = "forums"
        elif "learn.unity.com" in actual_url:
            category = "learn"

        results.append({
            "title": clean_title,
            "url": actual_url,
            "snippet": clean_snippet,
            "category": category,
        })

    return results


async def _fetch_page_content(url: str) -> str | None:
    """Fetch and extract text content from a documentation page."""
    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        ),
    }

    async with httpx.AsyncClient(follow_redirects=True, timeout=15.0) as client:
        resp = await client.get(url, headers=headers)
        resp.raise_for_status()
        html = resp.text

    # Extract the main content area
    # Unity docs use various content wrappers
    content_patterns = [
        r'<div[^>]*class="[^"]*content[^"]*"[^>]*>(.*?)</div>\s*(?:</div>|\Z)',
        r'<article[^>]*>(.*?)</article>',
        r'<div[^>]*id="content"[^>]*>(.*?)</div>',
        r'<main[^>]*>(.*?)</main>',
    ]

    text = ""
    for pattern in content_patterns:
        match = re.search(pattern, html, re.DOTALL)
        if match:
            text = match.group(1)
            break

    if not text:
        # Fallback: get body text
        body_match = re.search(r'<body[^>]*>(.*?)</body>', html, re.DOTALL)
        if body_match:
            text = body_match.group(1)

    # Strip HTML tags and clean up
    text = re.sub(r'<script[^>]*>.*?</script>', '', text, flags=re.DOTALL)
    text = re.sub(r'<style[^>]*>.*?</style>', '', text, flags=re.DOTALL)
    text = re.sub(r'<[^>]+>', ' ', text)
    text = re.sub(r'\s+', ' ', text).strip()

    # Truncate to reasonable length
    if len(text) > 10000:
        text = text[:10000] + "... (truncated)"

    return text if text else None


def _extract_url(ddg_url: str) -> str:
    """Extract actual URL from DuckDuckGo redirect URL."""
    if "uddg=" in ddg_url:
        match = re.search(r'uddg=([^&]+)', ddg_url)
        if match:
            return urllib.parse.unquote(match.group(1))
    if ddg_url.startswith("//"):
        return "https:" + ddg_url
    return ddg_url
