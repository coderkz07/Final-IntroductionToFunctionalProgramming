"""Set the base URL and prepare entry pages for the published StudyFlow app."""

import argparse
from html import escape
import re
from pathlib import Path


def prepare(site: Path, base_path: str) -> None:
    base_path = "/" + base_path.strip("/")
    if base_path != "/":
        base_path += "/"

    index = site / "index.html"
    document = index.read_text(encoding="utf-8")
    document, changes = re.subn(r'<base href="[^"]*" />',
                                lambda _: f'<base href="{escape(base_path, quote=True)}" />', document)
    if changes != 1:
        raise ValueError("Expected one base tag in the published index.html.")
    index.write_text(document, encoding="utf-8")
    # The publish step compressed the index before its base URL was set.
    for suffix in (".br", ".gz"):
        (site / ("index.html" + suffix)).unlink(missing_ok=True)
    (site / "404.html").write_text(document, encoding="utf-8")
    (site / ".nojekyll").touch()

    for route in ("assignments", "schedule"):
        page = site / route
        page.mkdir(exist_ok=True)
        (page / "index.html").write_text(document, encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("site", type=Path, help="The published wwwroot directory")
    parser.add_argument("base_path", help="The Pages base path returned by configure-pages")
    arguments = parser.parse_args()
    prepare(arguments.site, arguments.base_path)
