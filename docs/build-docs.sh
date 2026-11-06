#!/usr/bin/env bash
# Converts docs/BA.md and docs/SA.md to .docx. Requires pandoc, mmdc, python3.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"
export PATH="$HOME/.local/bin:$PATH"

for tool in pandoc mmdc python3; do
  command -v "$tool" >/dev/null || { echo "Missing tool: $tool" >&2; exit 2; }
done

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Chromium's sandbox is unavailable in WSL; this only renders our own diagrams.
printf '{"args":["--no-sandbox"]}' > "$work/puppeteer.json"
export PUPPETEER_CONFIG="$work/puppeteer.json"
export IMAGE_DIR="images"
mkdir -p "$IMAGE_DIR"

pandoc -o "$work/default.docx" --print-default-data-file reference.docx
python3 tools/make_reference.py "$work/default.docx" "$work/reference.docx"

for doc in BA SA; do
  pandoc "$doc.md" -o "$doc.docx" --from markdown-yaml_metadata_block+yaml_metadata_block \
    --reference-doc="$work/reference.docx" --lua-filter=tools/mermaid.lua --toc --toc-depth=2
  echo "Built $doc.docx"
done
