#!/usr/bin/env python3
"""Adapt pandoc's reference.docx: Thai-capable fonts and bordered tables."""
import re
import shutil
import sys
import zipfile

FONT = "Tahoma"
BORDERS = (
    "<w:tblBorders>"
    + "".join(
        f'<w:{side} w:val="single" w:sz="4" w:space="0" w:color="999999"/>'
        for side in ("top", "left", "bottom", "right", "insideH", "insideV")
    )
    + "</w:tblBorders>"
)


def patch_styles(xml: str) -> str:
    fonts = f'<w:rFonts w:ascii="{FONT}" w:hAnsi="{FONT}" w:eastAsia="{FONT}" w:cs="{FONT}"/>'
    xml = re.sub(r"<w:rFonts\b[^>]*/>", fonts, xml)
    # Table style: pandoc's default has no grid lines.
    pattern = r'(<w:style\b[^>]*w:styleId="Table"[^>]*>.*?<w:tblPr>)(.*?)(</w:tblPr>)'
    xml, replaced = re.subn(
        pattern, lambda m: m.group(1) + m.group(2) + BORDERS + m.group(3), xml, count=1, flags=re.S
    )
    if not replaced:
        raise SystemExit("Table style not found in reference.docx")
    return xml


def main(source: str, target: str) -> None:
    with zipfile.ZipFile(source) as zin, zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            data = zin.read(item.filename)
            if item.filename == "word/styles.xml":
                data = patch_styles(data.decode("utf-8")).encode("utf-8")
            zout.writestr(item, data)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
