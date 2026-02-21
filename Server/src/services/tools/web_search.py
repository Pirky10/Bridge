"""
Web search tool using DuckDuckGo (free, no API key required).
Uses HTTP scraping of DuckDuckGo HTML search results.
"""
import re
import urllib.parse
from typing import Annotated, Any

import httpx
from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool


@mcp_for_unity_tool(
    unity_target=None,
    description=(
        "Searches the web using DuckDuckGo (free, no API key). "
        "Returns search results with titles, URLs, and snippets. "
        "Useful for finding Unity tutorials, asset store packages, "
        "Stack Overflow solutions, and general development resources."
    ),
    annotations=ToolAnnotations(
        title="Web Search",
        readOnlyHint=True,
    ),
)
async def web_search(
    ctx: Context,
    query: Annotated[str, "Search query"],
    num_results: Annotated[int, "Max results to return (default: 8)"] | None = None,
    site: Annotated[str, "Limit to specific site (e.g., stackoverflow.com)"] | None = None,
) -> dict[str, Any]:
    max_results = num_results or 8

    # Build the query
    search_query = query
    if site:
        search_query = f"site:{site} {query}"

    try:
        results = await _search_duckduckgo(search_query, max_results)
        return {
            "success": True,
            "message": f"Found {len(results)} results for '{query}'",
            "data": {
                "query": query,
                "results": results,
            },
        }
    except Exception as e:
        return {"success": False, "message": f"Search failed: {e}"}


async def _search_duckduckgo(query: str, max_results: int) -> list[dict]:
    """Scrape DuckDuckGo HTML search results."""
    encoded_query = urllib.parse.quote_plus(query)
    url = f"https://html.duckduckgo.com/html/?q={encoded_query}"

    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        ),
        "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
        "Accept-Language": "en-US,en;q=0.5",
    }

    async with httpx.AsyncClient(follow_redirects=True, timeout=15.0) as client:
        response = await client.get(url, headers=headers)
        response.raise_for_status()
        html = response.text

    results = []

    # Parse DuckDuckGo HTML results
    # Each result is in a div with class "result"
    result_blocks = re.findall(
        r'<div[^>]*class="[^"]*result[^"]*"[^>]*>(.*?)</div>\s*</div>',
        html, re.DOTALL
    )

    if not result_blocks:
        # Alternative pattern: look for result__a links
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

            # DuckDuckGo wraps URLs in a redirect — extract actual URL
            actual_url = _extract_url(link)

            results.append({
                "title": clean_title,
                "url": actual_url,
                "snippet": clean_snippet,
            })
    else:
        for block in result_blocks[:max_results]:
            # Extract title and URL
            title_match = re.search(
                r'<a[^>]*class="result__a"[^>]*href="([^"]*)"[^>]*>(.*?)</a>',
                block, re.DOTALL
            )
            snippet_match = re.search(
                r'<a[^>]*class="result__snippet"[^>]*>(.*?)</a>',
                block, re.DOTALL
            )

            if title_match:
                url = _extract_url(title_match.group(1))
                title = re.sub(r'<[^>]+>', '', title_match.group(2)).strip()
                snippet = ""
                if snippet_match:
                    snippet = re.sub(r'<[^>]+>', '', snippet_match.group(1)).strip()

                results.append({
                    "title": title,
                    "url": url,
                    "snippet": snippet,
                })

    return results


def _extract_url(ddg_url: str) -> str:
    """Extract actual URL from DuckDuckGo redirect URL."""
    if "uddg=" in ddg_url:
        match = re.search(r'uddg=([^&]+)', ddg_url)
        if match:
            return urllib.parse.unquote(match.group(1))
    if ddg_url.startswith("//"):
        return "https:" + ddg_url
    return ddg_url
