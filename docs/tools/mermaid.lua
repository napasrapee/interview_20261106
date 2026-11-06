-- Renders ```mermaid code blocks to PNG with mmdc and embeds the image.
-- Fails the whole build if a diagram cannot be rendered.
local stem = (PANDOC_STATE.output_file or "doc"):match("([^/]+)%.docx$") or "doc"
local image_dir = os.getenv("IMAGE_DIR") or "images"
local puppeteer = os.getenv("PUPPETEER_CONFIG")
local count = 0

function CodeBlock(block)
  if not block.classes:includes("mermaid") then
    return nil
  end
  count = count + 1
  local base = string.format("%s/%s-%02d", image_dir, stem, count)
  local source = base .. ".mmd"
  local image = base .. ".png"
  local file = assert(io.open(source, "w"))
  file:write(block.text)
  file:close()
  local args = { "-q", "-i", source, "-o", image, "-s", "2", "-b", "white" }
  if puppeteer then
    table.insert(args, "-p")
    table.insert(args, puppeteer)
  end
  pandoc.pipe("mmdc", args, "")
  os.remove(source)
  return pandoc.Para({ pandoc.Image({ pandoc.Str("Diagram " .. count) }, image) })
end
