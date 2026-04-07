
-- Magic Chalk Studio - version complète corrigée
-- Sauvegarde / chargement dans le dossier de l'application (même dossier que main.lua)
-- Undo / Redo
-- Sélection rectangulaire et déplacement
-- Boutons de commande lisibles

local utf8 = require("utf8")

-- =========================
-- JSON minimal
-- =========================
local json = {}

do
  local escape_char_map = {
    ["\\"] = "\\\\", ["\""] = "\\\"", ["\b"] = "\\b", ["\f"] = "\\f",
    ["\n"] = "\\n", ["\r"] = "\\r", ["\t"] = "\\t",
  }

  local function escape_char(c)
    return escape_char_map[c] or string.format("\\u%04x", c:byte())
  end

  function json.encode(v)
    local t = type(v)
    if t == "nil" then
      return "null"
    elseif t == "number" or t == "boolean" then
      return tostring(v)
    elseif t == "string" then
      return '"' .. v:gsub('[%z\1-\31\\"]', escape_char) .. '"'
    elseif t == "table" then
      local is_array = true
      local max = 0
      for k, _ in pairs(v) do
        if type(k) ~= "number" then
          is_array = false
          break
        end
        if k > max then max = k end
      end
      local out = {}
      if is_array then
        for i = 1, max do
          out[#out + 1] = json.encode(v[i])
        end
        return "[" .. table.concat(out, ",") .. "]"
      else
        for k, val in pairs(v) do
          out[#out + 1] = json.encode(tostring(k)) .. ":" .. json.encode(val)
        end
        return "{" .. table.concat(out, ",") .. "}"
      end
    else
      error("Type JSON non supporté: " .. t)
    end
  end

  local function decode_error(_, idx, msg)
    error("Erreur JSON à " .. tostring(idx) .. ": " .. msg)
  end

  local function skip_ws(str, idx)
    while true do
      local c = str:sub(idx, idx)
      if c == " " or c == "\n" or c == "\r" or c == "\t" then
        idx = idx + 1
      else
        return idx
      end
    end
  end

  local parse_value

  local function parse_string(str, idx)
    idx = idx + 1
    local out = {}
    while idx <= #str do
      local c = str:sub(idx, idx)
      if c == '"' then
        return table.concat(out), idx + 1
      elseif c == "\\" then
        local n = str:sub(idx + 1, idx + 1)
        if n == '"' or n == "\\" or n == "/" then out[#out + 1] = n
        elseif n == "b" then out[#out + 1] = "\b"
        elseif n == "f" then out[#out + 1] = "\f"
        elseif n == "n" then out[#out + 1] = "\n"
        elseif n == "r" then out[#out + 1] = "\r"
        elseif n == "t" then out[#out + 1] = "\t"
        else decode_error(str, idx, "escape invalide") end
        idx = idx + 2
      else
        out[#out + 1] = c
        idx = idx + 1
      end
    end
    decode_error(str, idx, "string non fermée")
  end

  local function parse_number(str, idx)
    local s, e = str:find("^-?%d+%.?%d*[eE]?[+-]?%d*", idx)
    if not s then decode_error(str, idx, "nombre invalide") end
    local num = tonumber(str:sub(s, e))
    return num, e + 1
  end

  local function parse_array(str, idx)
    idx = idx + 1
    local res = {}
    idx = skip_ws(str, idx)
    if str:sub(idx, idx) == "]" then return res, idx + 1 end
    while true do
      local val
      val, idx = parse_value(str, idx)
      res[#res + 1] = val
      idx = skip_ws(str, idx)
      local c = str:sub(idx, idx)
      if c == "]" then return res, idx + 1 end
      if c ~= "," then decode_error(str, idx, "virgule attendue") end
      idx = skip_ws(str, idx + 1)
    end
  end

  local function parse_object(str, idx)
    idx = idx + 1
    local res = {}
    idx = skip_ws(str, idx)
    if str:sub(idx, idx) == "}" then return res, idx + 1 end
    while true do
      if str:sub(idx, idx) ~= '"' then decode_error(str, idx, "clé string attendue") end
      local key
      key, idx = parse_string(str, idx)
      idx = skip_ws(str, idx)
      if str:sub(idx, idx) ~= ":" then decode_error(str, idx, "':' attendu") end
      idx = skip_ws(str, idx + 1)
      local val
      val, idx = parse_value(str, idx)
      res[key] = val
      idx = skip_ws(str, idx)
      local c = str:sub(idx, idx)
      if c == "}" then return res, idx + 1 end
      if c ~= "," then decode_error(str, idx, "virgule attendue") end
      idx = skip_ws(str, idx + 1)
    end
  end

  function parse_value(str, idx)
    idx = skip_ws(str, idx)
    local c = str:sub(idx, idx)
    if c == '"' then return parse_string(str, idx)
    elseif c == "{" then return parse_object(str, idx)
    elseif c == "[" then return parse_array(str, idx)
    elseif c == "-" or c:match("%d") then return parse_number(str, idx)
    elseif str:sub(idx, idx + 3) == "true" then return true, idx + 4
    elseif str:sub(idx, idx + 4) == "false" then return false, idx + 5
    elseif str:sub(idx, idx + 3) == "null" then return nil, idx + 4
    else decode_error(str, idx, "token invalide") end
  end

  function json.decode(str)
    local val, idx = parse_value(str, 1)
    idx = skip_ws(str, idx)
    if idx <= #str then decode_error(str, idx, "contenu supplémentaire") end
    return val
  end
end

-- =========================
-- Helpers
-- =========================
local function cloneTable(t)
  local out = {}
  for k, v in pairs(t or {}) do out[k] = v end
  return out
end

local function sanitizeFileName(s)
  s = tostring(s or "structure")
  s = s:gsub('[<>:"/\\|%?%*]', "_")
  s = s:gsub("%s+$", "")
  s = s:gsub("^%s+", "")
  if s == "" then s = "structure" end
  return s
end

local function escapeJsonString(s)
  s = tostring(s or "")
  s = s:gsub("\\", "\\\\")
  s = s:gsub("\"", "\\\"")
  s = s:gsub("\n", "\\n")
  s = s:gsub("\r", "\\r")
  s = s:gsub("\t", "\\t")
  return s
end

local function defaultBlocks()
  return {
    "minecraft:stone","minecraft:cobblestone","minecraft:stone_bricks","minecraft:mossy_stone_bricks",
    "minecraft:deepslate","minecraft:deepslate_bricks","minecraft:deepslate_tiles","minecraft:dirt",
    "minecraft:grass_block","minecraft:sand","minecraft:red_sand","minecraft:gravel",
    "minecraft:oak_log","minecraft:spruce_log","minecraft:birch_log","minecraft:jungle_log","minecraft:acacia_log",
    "minecraft:dark_oak_log","minecraft:cherry_log","minecraft:mangrove_log","minecraft:oak_planks",
    "minecraft:spruce_planks","minecraft:birch_planks","minecraft:jungle_planks","minecraft:acacia_planks",
    "minecraft:dark_oak_planks","minecraft:cherry_planks","minecraft:mangrove_planks","minecraft:bamboo_planks",
    "minecraft:oak_stairs","minecraft:spruce_stairs","minecraft:birch_stairs","minecraft:jungle_stairs",
    "minecraft:acacia_stairs","minecraft:dark_oak_stairs","minecraft:cherry_stairs","minecraft:mangrove_stairs",
    "minecraft:stone_stairs","minecraft:brick_stairs","minecraft:deepslate_brick_stairs","minecraft:quartz_stairs",
    "minecraft:purpur_stairs","minecraft:glass","minecraft:white_stained_glass","minecraft:light_blue_stained_glass",
    "minecraft:gray_stained_glass","minecraft:bricks","minecraft:bookshelf","minecraft:obsidian","minecraft:glowstone",
    "minecraft:sea_lantern","minecraft:iron_block","minecraft:gold_block","minecraft:diamond_block",
    "minecraft:emerald_block","minecraft:redstone_block","minecraft:lapis_block","minecraft:amethyst_block",
    "minecraft:copper_block","minecraft:torch","minecraft:lantern","minecraft:soul_lantern",
    "minecraft:crafting_table","minecraft:furnace","minecraft:chest","minecraft:barrel","minecraft:piston",
    "minecraft:sticky_piston","minecraft:redstone_wire","minecraft:oak_door","minecraft:spruce_door",
    "minecraft:birch_door","minecraft:iron_door","minecraft:blast_furnace","minecraft:smoker",
    "minecraft:trapped_chest","minecraft:ender_chest","minecraft:oak_leaves","minecraft:spruce_leaves",
    "minecraft:birch_leaves","minecraft:jungle_leaves","minecraft:acacia_leaves","minecraft:dark_oak_leaves",
    "minecraft:cherry_leaves","minecraft:azalea_leaves","minecraft:white_wool","minecraft:black_wool",
    "minecraft:red_wool","minecraft:blue_wool","minecraft:green_wool","minecraft:yellow_wool",
    "minecraft:water","minecraft:lava","minecraft:air"
  }
end

local statePresets = {
  stairs = {
    order = {"facing", "half", "shape", "waterlogged"},
    values = {
      facing = {"north", "east", "south", "west"},
      half = {"bottom", "top"},
      shape = {"straight", "inner_left", "inner_right", "outer_left", "outer_right"},
      waterlogged = {"false", "true"},
    },
    defaults = {facing = "north", half = "bottom", shape = "straight", waterlogged = "false"}
  },
  log = {
    order = {"axis"},
    values = {axis = {"x", "y", "z"}},
    defaults = {axis = "y"}
  },
  piston = {
    order = {"facing", "extended"},
    values = {facing = {"north", "east", "south", "west", "up", "down"}, extended = {"false", "true"}},
    defaults = {facing = "north", extended = "false"}
  },
  door = {
    order = {"facing", "half", "hinge", "open", "powered"},
    values = {
      facing = {"north", "east", "south", "west"},
      half = {"lower", "upper"},
      hinge = {"left", "right"},
      open = {"false", "true"},
      powered = {"false", "true"},
    },
    defaults = {facing = "north", half = "lower", hinge = "left", open = "false", powered = "false"}
  },
  redstone_wire = {
    order = {"east", "north", "south", "west", "power"},
    values = {
      east = {"none", "side", "up"},
      north = {"none", "side", "up"},
      south = {"none", "side", "up"},
      west = {"none", "side", "up"},
      power = {"0","1","2","3","4","5","6","7","8","9","10","11","12","13","14","15"},
    },
    defaults = {east = "none", north = "none", south = "none", west = "none", power = "0"}
  },
  furnace = {
    order = {"facing", "lit"},
    values = {facing = {"north", "east", "south", "west"}, lit = {"false", "true"}},
    defaults = {facing = "north", lit = "false"}
  },
  chest = {
    order = {"facing", "type", "waterlogged"},
    values = {facing = {"north", "east", "south", "west"}, type = {"single", "left", "right"}, waterlogged = {"false", "true"}},
    defaults = {facing = "north", type = "single", waterlogged = "false"}
  },
}

local function getPresetType(blockId)
  if not blockId then return nil end
  if blockId:find("_stairs", 1, true) then return "stairs" end
  if blockId:find("_log", 1, true) or blockId:find("stem", 1, true) or blockId:find("hyphae", 1, true) then return "log" end
  if blockId == "minecraft:piston" or blockId == "minecraft:sticky_piston" then return "piston" end
  if blockId:find("_door", 1, true) then return "door" end
  if blockId == "minecraft:redstone_wire" then return "redstone_wire" end
  if blockId == "minecraft:furnace" or blockId == "minecraft:smoker" or blockId == "minecraft:blast_furnace" then return "furnace" end
  if blockId == "minecraft:chest" or blockId == "minecraft:trapped_chest" or blockId == "minecraft:ender_chest" or blockId == "minecraft:barrel" then return "chest" end
  return nil
end

local function parseBlockStateString(stateString)
  local props = {}
  if not stateString or stateString == "" then return props end
  local inside = stateString:match("%[(.*)%]")
  if not inside then return props end
  for pair in inside:gmatch("[^,]+") do
    local k, v = pair:match("([^=]+)=([^=]+)")
    if k and v then props[k] = v end
  end
  return props
end

local function buildBlockStateString(blockId, props)
  local presetType = getPresetType(blockId)
  if not presetType then
    return "Block{" .. tostring(blockId or "minecraft:air") .. "}"
  end

  local preset = statePresets[presetType]
  local parts = {}
  for _, key in ipairs(preset.order) do
    if props[key] ~= nil then parts[#parts + 1] = key .. "=" .. tostring(props[key]) end
  end
  if #parts == 0 then return "Block{" .. tostring(blockId or "minecraft:air") .. "}" end
  return "Block{" .. tostring(blockId or "minecraft:air") .. "}[" .. table.concat(parts, ",") .. "]"
end

local function makeDefaultStateString(blockId)
  local presetType = getPresetType(blockId)
  if not presetType then return "Block{" .. tostring(blockId or "minecraft:air") .. "}" end
  return buildBlockStateString(blockId, cloneTable(statePresets[presetType].defaults))
end

-- =========================
-- App state
-- =========================
local app = {
  name = "unnamed",
  sizeX = 50000000,
  sizeY = 255,
  sizeZ = 50000000,
  layer = 0,
  tool = "single",
  showGrid = true,
  currentBlockId = "minecraft:stone",
  currentState = "",
  rectStart = nil,
  blocks = {},
  palette = {},
  filteredPalette = {},
  message = "Glisse un JSON ou charge un fichier depuis le dossier de l'application.",
  saveName = "structure",
  editingName = false,
  searchText = "",
  editingSearch = false,
  cameraX = 0,
  cameraZ = 0,
  savePathInfo = "",
  selectedBlockKey = nil,
  loadPath = "",
  saveFiles = {},
  selection = nil,
  undoStack = {},
  redoStack = {},
  previewWrites = {},
}

local ui = {
  sidebarW = 560,
  cell = 24,
  offsetX = 590,
  offsetY = 60,
  paletteBox = {x = 14, y = 350, w = 530, h = 200},
  stateBox = {x = 14, y = 570, w = 530, h = 170},
  saveListBox = {x = 14, y = 760, w = 530, h = 110},
  cmdBox = {x = 14, y = 900, w = 530, h = 220},
  paletteScroll = 0,
  saveScroll = 0,
  cmdScroll = 0,
  lineH = 24,
  cmdButtonH = 34,
  cmdButtonGap = 8,
  cmdColumns = 2,
  stateButtons = {},
  actionButtons = {},
  fileButtons = {},
  loadButton = nil,
  editNameButton = nil,
  editSearchButton = nil,
  saveButton = nil,
}

local colors = {
  bg = {0.08, 0.08, 0.09},
  panel = {0.12, 0.12, 0.14},
  panel2 = {0.16, 0.16, 0.18},
  text = {0.95, 0.95, 0.97},
  sub = {0.72, 0.78, 0.85},
  accent = {0.18, 0.45, 0.75},
  warn = {0.75, 0.42, 0.18},
  grid = {0.24, 0.24, 0.28},
  empty = {0.08, 0.08, 0.09},
  hover = {1.0, 0.95, 0.45, 0.35},
  stair = {0.94, 0.67, 0.22},
  scrollbar = {0.35, 0.35, 0.42},
  selected = {0.25, 0.70, 1.0, 0.35},
  selectionOutline = {0.95, 0.85, 0.25, 0.95},
  buttonBlue = {0.17, 0.27, 0.45},
  buttonGreen = {0.16, 0.36, 0.22},
  buttonRed = {0.45, 0.20, 0.18},
  buttonBrown = {0.34, 0.28, 0.20},
  buttonPurple = {0.30, 0.20, 0.40},
  buttonGray = {0.17, 0.19, 0.24},
}

local commandButtons = {
  {label = "Pose simple", action = "tool_single", group = "tool"},
  {label = "Brush 3x3", action = "tool_brush", group = "tool"},
  {label = "Ligne", action = "tool_line", group = "tool"},
  {label = "Rect plein", action = "tool_fill", group = "tool"},
  {label = "Rect contour", action = "tool_wall", group = "tool"},
  {label = "Cercle plein", action = "tool_circle_fill", group = "tool"},
  {label = "Cercle contour", action = "tool_circle_wall", group = "tool"},
  {label = "Ellipse pleine", action = "tool_ellipse_fill", group = "tool"},
  {label = "Ellipse contour", action = "tool_ellipse_wall", group = "tool"},
  {label = "Sélection zone", action = "tool_select", group = "select"},
  {label = "Annuler forme", action = "cancel_rect", group = "select"},
  {label = "Déplacer <-", action = "move_left", group = "move"},
  {label = "Déplacer ->", action = "move_right", group = "move"},
  {label = "Déplacer haut", action = "move_up", group = "move"},
  {label = "Déplacer bas", action = "move_down", group = "move"},
  {label = "Undo", action = "undo", group = "edit"},
  {label = "Redo", action = "redo", group = "edit"},
  {label = "Copie", action = "copy_selection", group = "edit"},
  {label = "Sauver legacy", action = "save_legacy", group = "file"},
  {label = "Export NeoForge", action = "save_wrapper", group = "file"},
  {label = "Sauver compact", action = "save_compact", group = "file"},
  {label = "Rotation +90°", action = "rotate", group = "edit"},
  {label = "Vider layer", action = "clear_layer", group = "danger"},
  {label = "Vider tout", action = "clear_all", group = "danger"},
  {label = "Grille ON/OFF", action = "toggle_grid", group = "view"},
  {label = "Layer +", action = "layer_up", group = "view"},
  {label = "Layer -", action = "layer_down", group = "view"},
  {label = "Zoom +", action = "zoom_in", group = "view"},
  {label = "Zoom -", action = "zoom_out", group = "view"},
  {label = "Caméra haut", action = "cam_up", group = "view"},
  {label = "Caméra bas", action = "cam_down", group = "view"},
  {label = "Caméra gauche", action = "cam_left", group = "view"},
  {label = "Caméra droite", action = "cam_right", group = "view"},
  {label = "Bloc suivant", action = "next_block", group = "block"},
  {label = "Bloc précédent", action = "prev_block", group = "block"},
  {label = "Nom ON/OFF", action = "toggle_name", group = "field"},
  {label = "Recherche ON/OFF", action = "toggle_search", group = "field"},
}

-- forward declarations
local clampCamera
local loadStructure
local saveLegacy
local saveCompact
local saveWrapper
local rotate90
local refreshSaveFileList

-- =========================
-- Files in application folder
-- =========================
local localAppDir = nil

local function joinPath(a, b)
  local sep = package.config:sub(1, 1)
  return tostring(a) .. sep .. tostring(b)
end

local function projectDir()
  local candidates = {}

  local okBase, baseDir = pcall(function()
    return love.filesystem.getSourceBaseDirectory()
  end)
  if okBase and type(baseDir) == "string" and baseDir ~= "" then
    candidates[#candidates + 1] = baseDir
  end

  local okSrc, srcDir = pcall(function()
    return love.filesystem.getSource()
  end)
  if okSrc and type(srcDir) == "string" and srcDir ~= "" then
    candidates[#candidates + 1] = srcDir
  end

  if arg and type(arg[0]) == "string" and arg[0] ~= "" then
    candidates[#candidates + 1] = arg[0]:match("(.+)[/\\].-$") or arg[0]
  end

  local info = debug.getinfo(1, "S")
  local source = info and info.source or ""
  if source:sub(1, 1) == "@" then
    source = source:sub(2)
    candidates[#candidates + 1] = source:match("(.+)[/\\].-$") or source
  end

  for _, candidate in ipairs(candidates) do
    if type(candidate) == "string" then
      candidate = candidate:gsub("[/\\]+$", "")
      if candidate ~= "" and not candidate:match("^%.[/\\]?$") then
        local testPath = joinPath(candidate, "main.lua"or"MagicalChalkEditor.exe")
        local f = io.open(testPath, "rb")
        if f then
          f:close()
          return candidate
        end
      end
    end
  end

  return nil
end

local function setupSaveDirectory()
  local src = projectDir()
  if src and src ~= "" then
    localAppDir = joinPath(src, "FileSave")

    if package.config:sub(1, 1) == "\\" then
      os.execute('mkdir "' .. localAppDir .. '" >nul 2>nul')
    else
      os.execute('mkdir -p "' .. localAppDir .. '" >/dev/null 2>/dev/null')
    end

    app.savePathInfo = "Dossier FileSave: " .. localAppDir
    return
  end

  localAppDir = nil
  app.savePathInfo = "Dossier FileSave introuvable"
end

local function readWholeFile(path)
  local f = io.open(path, "rb")
  if not f then return nil end
  local d = f:read("*a")
  f:close()
  return d
end

local function writeWholeFile(path, content)
  local f = io.open(path, "wb")
  if not f then return false end
  f:write(content)
  f:close()
  return true
end

local function listJsonFilesInDirectory(path)
  local items = {}
  local seen = {}
  local sep = package.config:sub(1, 1)
  local cmd
  if sep == "\\" then
    cmd = 'dir /b "' .. path .. '"'
  else
    cmd = 'ls -1 "' .. path .. '"'
  end

  local pipe = io.popen(cmd)
  if pipe then
    for line in pipe:lines() do
      local name = tostring(line or "")
      if name:lower():match("%.json$") and not seen[name:lower()] then
        seen[name:lower()] = true
        items[#items + 1] = name
      end
    end
    pipe:close()
  end

  table.sort(items, function(a, b) return a:lower() < b:lower() end)
  return items
end

refreshSaveFileList = function()
  app.saveFiles = {}
  if localAppDir then
    app.saveFiles = listJsonFilesInDirectory(localAppDir)
  end

  local maxScroll = math.max(0, #app.saveFiles - math.max(1, math.floor((ui.saveListBox.h - 8) / ui.lineH)))
  if ui.saveScroll > maxScroll then ui.saveScroll = maxScroll end
end

local function saveVisibleLines()
  return math.max(1, math.floor((ui.saveListBox.h - 8) / ui.lineH))
end

local function paletteVisibleLines()
  return math.max(1, math.floor((ui.paletteBox.h - 8) / ui.lineH))
end

local function commandButtonRows()
  return math.ceil(#commandButtons / math.max(1, ui.cmdColumns))
end

local function buttonVisibleRows()
  local perRow = ui.cmdButtonH + ui.cmdButtonGap
  return math.max(1, math.floor((ui.cmdBox.h - 8) / perRow))
end

local function safeScissor(x, y, w, h)
  if x and y and w and h and w > 0 and h > 0 then
    love.graphics.setScissor(x, y, w, h)
  else
    love.graphics.setScissor()
  end
end

local function drawScrollBar(rect, contentLines, scrollLines, lineHeight)
  local rowH = lineHeight or ui.lineH
  local visibleLines = math.max(1, math.floor(rect.h / rowH))
  if contentLines <= visibleLines then return end

  local trackX = rect.x + rect.w - 10
  local trackY = rect.y + 4
  local trackH = rect.h - 8

  love.graphics.setColor(0.18, 0.18, 0.20)
  love.graphics.rectangle("fill", trackX, trackY, 6, trackH, 3, 3)

  local ratio = visibleLines / contentLines
  local handleH = math.max(20, trackH * ratio)
  local maxScroll = contentLines - visibleLines
  local handleY = trackY

  if maxScroll > 0 then
    handleY = trackY + (scrollLines / maxScroll) * (trackH - handleH)
  end

  love.graphics.setColor(colors.scrollbar)
  love.graphics.rectangle("fill", trackX, handleY, 6, handleH, 3, 3)
end

-- =========================
-- Blocks
-- =========================
local function loadBlocksFile()
  local blocks = {}
  local text = nil

  if localAppDir then
    text = readWholeFile(joinPath(localAppDir, "blocks.txt"))
  end

  if not text then
    text = table.concat(defaultBlocks(), "\n")
    if localAppDir then
      writeWholeFile(joinPath(localAppDir, "blocks.txt"), text)
    else
      love.filesystem.write("blocks.txt", text)
    end
  end

  local seen = {}
  for line in text:gmatch("[^\r\n]+") do
    line = line:gsub("^%s+", ""):gsub("%s+$", "")
    if line ~= "" and line:sub(1,1) ~= "#" and not seen[line] then
      seen[line] = true
      blocks[#blocks + 1] = line
    end
  end

  if #blocks == 0 then
    blocks = defaultBlocks()
  end

  table.sort(blocks)
  app.palette = blocks
  if not seen[app.currentBlockId] then
    app.currentBlockId = blocks[1]
  end
end

local function rebuildFilteredPalette()
  app.filteredPalette = {}
  local q = (app.searchText or ""):lower()

  for _, blockId in ipairs(app.palette) do
    if q == "" or blockId:lower():find(q, 1, true) then
      app.filteredPalette[#app.filteredPalette + 1] = blockId
    end
  end

  local maxScroll = math.max(0, #app.filteredPalette - paletteVisibleLines())
  if ui.paletteScroll > maxScroll then ui.paletteScroll = maxScroll end
end

local function blockKey(x, y, z)
  return x .. "," .. y .. "," .. z
end

local function setBlockDirect(x, y, z, blockId, stateString)
  if x < 0 or y < 0 or z < 0 or x >= app.sizeX or y >= app.sizeY or z >= app.sizeZ then
    return
  end

  local key = blockKey(x, y, z)
  if blockId == "minecraft:air" then
    app.blocks[key] = nil
    if app.selectedBlockKey == key then app.selectedBlockKey = nil end
  else
    local finalState = stateString
    if not finalState or finalState == "" then
      finalState = makeDefaultStateString(blockId)
    end
    app.blocks[key] = {
      x = x, y = y, z = z,
      blockId = blockId,
      blockStateString = finalState
    }
  end
end

local function getBlockSnapshotAt(x, y, z)
  local b = app.blocks[blockKey(x, y, z)]
  if b then
    return {x = b.x, y = b.y, z = b.z, blockId = b.blockId, blockStateString = b.blockStateString}
  end
  return {x = x, y = y, z = z, blockId = "minecraft:air", blockStateString = ""}
end

local function pushUndo(action)
  app.undoStack[#app.undoStack + 1] = action
  if #app.undoStack > 100 then
    table.remove(app.undoStack, 1)
  end
  app.redoStack = {}
end

local function applyChangeSet(changes, forward)
  for _, change in ipairs(changes) do
    local src = forward and change.after or change.before
    setBlockDirect(src.x, src.y, src.z, src.blockId, src.blockStateString)
  end
end

local function undo()
  local action = table.remove(app.undoStack)
  if not action then
    app.message = "Rien à annuler."
    return
  end
  applyChangeSet(action.changes, false)
  app.redoStack[#app.redoStack + 1] = action
  app.message = "Undo"
end

local function redo()
  local action = table.remove(app.redoStack)
  if not action then
    app.message = "Rien à refaire."
    return
  end
  applyChangeSet(action.changes, true)
  app.undoStack[#app.undoStack + 1] = action
  app.message = "Redo"
end

local function applyWrites(writes, label)
  local merged = {}
  local order = {}
  for _, w in ipairs(writes) do
    local key = blockKey(w.x, w.y, w.z)
    if not merged[key] then
      merged[key] = {x = w.x, y = w.y, z = w.z, blockId = w.blockId, blockStateString = w.blockStateString}
      order[#order + 1] = key
    else
      merged[key].blockId = w.blockId
      merged[key].blockStateString = w.blockStateString
    end
  end

  local changes = {}
  for _, key in ipairs(order) do
    local w = merged[key]
    changes[#changes + 1] = {
      before = getBlockSnapshotAt(w.x, w.y, w.z),
      after = {
        x = w.x, y = w.y, z = w.z,
        blockId = w.blockId or "minecraft:air",
        blockStateString = w.blockStateString or ""
      }
    }
  end

  if #changes == 0 then return end
  applyChangeSet(changes, true)
  pushUndo({label = label or "edit", changes = changes})
end

local function countBlocks()
  local n = 0
  for _ in pairs(app.blocks) do n = n + 1 end
  return n
end

local function uniquePaletteCount()
  local seen = {}
  local n = 0
  for _, b in pairs(app.blocks) do
    local k = (b.blockId or "minecraft:air") .. "|" .. (b.blockStateString or "")
    if not seen[k] then
      seen[k] = true
      n = n + 1
    end
  end
  return n
end

local function getSelectedBlock()
  if not app.selectedBlockKey then return nil end
  return app.blocks[app.selectedBlockKey]
end

local function getBlockGlyph(block)
  if not block then return "" end
  local props = parseBlockStateString(block.blockStateString)

  if getPresetType(block.blockId) == "stairs" then
    if props.facing == "north" then return "^" end
    if props.facing == "south" then return "v" end
    if props.facing == "east" then return ">" end
    if props.facing == "west" then return "<" end
    return "S"
  end

  if getPresetType(block.blockId) == "log" then
    if props.axis == "x" then return "-" end
    if props.axis == "y" then return "|" end
    if props.axis == "z" then return "/" end
    return "L"
  end

  if getPresetType(block.blockId) == "piston" or getPresetType(block.blockId) == "furnace" or getPresetType(block.blockId) == "chest" then
    if props.facing == "north" then return "^" end
    if props.facing == "south" then return "v" end
    if props.facing == "east" then return ">" end
    if props.facing == "west" then return "<" end
    if props.facing == "up" then return "U" end
    if props.facing == "down" then return "D" end
  end

  if getPresetType(block.blockId) == "door" then return "D" end
  if getPresetType(block.blockId) == "redstone_wire" then return "R" end

  return (block.blockId:gsub("minecraft:", "")):sub(1, 1):upper()
end

local function getBlockColor(blockId)
  local id = (blockId or ""):lower()
  if id:find("_stairs", 1, true) then
    return colors.stair[1], colors.stair[2], colors.stair[3]
  elseif id:find("stone") then
    return 0.55, 0.55, 0.55
  elseif id:find("oak") or id:find("spruce") or id:find("birch") or id:find("jungle")
      or id:find("acacia") or id:find("cherry") or id:find("mangrove") then
    return 0.62, 0.42, 0.19
  elseif id:find("glass") then
    return 0.66, 0.85, 1.0
  elseif id:find("grass") then
    return 0.35, 0.63, 0.31
  elseif id:find("leaves") then
    return 0.24, 0.55, 0.24
  elseif id:find("water") then
    return 0.31, 0.47, 0.65
  elseif id:find("lava") then
    return 0.88, 0.35, 0.25
  elseif id:find("sand") then
    return 0.90, 0.82, 0.55
  elseif id:find("obsidian") then
    return 0.24, 0.19, 0.35
  elseif id:find("diamond") then
    return 0.46, 0.84, 0.92
  elseif id:find("emerald") then
    return 0.27, 0.80, 0.46
  elseif id:find("gold") then
    return 0.95, 0.80, 0.30
  elseif id:find("iron") then
    return 0.80, 0.80, 0.80
  elseif id:find("amethyst") then
    return 0.56, 0.39, 0.78
  elseif id:find("redstone") then
    return 0.85, 0.15, 0.15
  elseif id:find("piston") then
    return 0.67, 0.60, 0.42
  elseif id:find("door") then
    return 0.55, 0.35, 0.18
  elseif id:find("furnace") then
    return 0.35, 0.35, 0.35
  elseif id:find("chest") or id:find("barrel") then
    return 0.60, 0.40, 0.18
  else
    return 0.80, 0.80, 0.80
  end
end

-- =========================
-- Save / Load structures
-- =========================
local function legacyBlocksSorted()
  local blocks = {}
  for _, b in pairs(app.blocks) do
    blocks[#blocks + 1] = {
      x = b.x, y = b.y, z = b.z,
      blockId = b.blockId,
      blockStateString = b.blockStateString or makeDefaultStateString(b.blockId)
    }
  end

  table.sort(blocks, function(a, b)
    if a.x ~= b.x then return a.x < b.x end
    if a.y ~= b.y then return a.y < b.y end
    return a.z < b.z
  end)

  return blocks
end

local function legacyDict()
  local structureName = sanitizeFileName(app.saveName)
  return {
    name = structureName,
    sizeX = app.sizeX,
    sizeY = app.sizeY,
    sizeZ = app.sizeZ,
    blocks = legacyBlocksSorted()
  }
end

local function encodeLegacyStrict()
  local data = legacyDict()
  local parts = {}
  parts[#parts + 1] = "{"
  parts[#parts + 1] = "\"name\":\"" .. escapeJsonString(data.name) .. "\","
  parts[#parts + 1] = "\"sizeX\":" .. tostring(data.sizeX) .. ","
  parts[#parts + 1] = "\"sizeY\":" .. tostring(data.sizeY) .. ","
  parts[#parts + 1] = "\"sizeZ\":" .. tostring(data.sizeZ) .. ","
  parts[#parts + 1] = "\"blocks\":["

  for i, b in ipairs(data.blocks) do
    parts[#parts + 1] =
      "{\"x\":" .. tostring(b.x) ..
      ",\"y\":" .. tostring(b.y) ..
      ",\"z\":" .. tostring(b.z) ..
      ",\"blockId\":\"" .. escapeJsonString(b.blockId) ..
      "\",\"blockStateString\":\"" .. escapeJsonString(b.blockStateString or makeDefaultStateString(b.blockId)) ..
      "\"}"
    if i < #data.blocks then parts[#parts + 1] = "," end
  end

  parts[#parts + 1] = "]}"
  return table.concat(parts)
end

local function compactDict()
  local structureName = sanitizeFileName(app.saveName)
  local paletteIndex = {}
  local palette = {}
  local positions = {}
  local states = {}

  for _, b in pairs(app.blocks) do
    local k = (b.blockId or "minecraft:air") .. "|" .. (b.blockStateString or makeDefaultStateString(b.blockId))
    local idx = paletteIndex[k]
    if not idx then
      palette[#palette + 1] = k
      idx = #palette - 1
      paletteIndex[k] = idx
    end
    positions[#positions + 1] = b.x
    positions[#positions + 1] = b.y
    positions[#positions + 1] = b.z
    states[#states + 1] = idx
  end

  return {
    name = structureName,
    sizeX = app.sizeX,
    sizeY = app.sizeY,
    sizeZ = app.sizeZ,
    format = 2,
    palette = palette,
    positions = positions,
    states = states,
  }
end

local function wrapperDict()
  local structureName = sanitizeFileName(app.saveName)
  local data = compactDict()
  data.name = structureName
  return { structures = { [structureName:lower()] = data } }
end

local function saveToFile(relativeName, content)
  if not localAppDir then
    return false, "dossier application introuvable"
  end
  local path = joinPath(localAppDir, relativeName)
  if writeWholeFile(path, content) then
    return true, path
  end
  return false, path
end

local function loadFromSaveFile(name)
  local fileName = sanitizeFileName(name or name+"")
  if fileName == "" then
    app.message = "Nom de fichier vide"
    return
  end
  if not fileName:lower():match("%.json$") then
    fileName = fileName .. ".json"
  end

  if not localAppDir then
    app.message = "Dossier application introuvable."
    return
  end

  local fullPath = joinPath(localAppDir, fileName)
  local contents = readWholeFile(fullPath)

  if not contents or contents == "" then
    app.message = "Impossible de lire: " .. fullPath
    return
  end

  local okJson, data = pcall(json.decode, contents)
  if not okJson then
    app.message = "JSON invalide: " .. tostring(data)
    return
  end

  local okLoad, errLoad = pcall(function()
    loadStructure(data)
  end)
  if not okLoad then
    app.message = "Erreur chargement structure: " .. tostring(errLoad)
    return
  end

  app.loadPath = fileName
  app.message = "Structure chargée: " .. fileName
end

loadStructure = function(data)
  if data.structures then
    for _, v in pairs(data.structures) do
      return loadStructure(v)
    end
  end

  app.blocks = {}
  app.selectedBlockKey = nil
  app.selection = nil
  app.name = data.name or "unnamed"
  app.saveName = app.name
  app.sizeX = math.max(1, math.min(50000000, tonumber(data.sizeX) or 50000000))
  app.sizeY = math.max(1, math.min(255, tonumber(data.sizeY) or 255))
  app.sizeZ = math.max(1, math.min(50000000, tonumber(data.sizeZ) or 50000000))
  app.layer = math.min(app.layer, app.sizeY - 1)
  app.rectStart = nil

  if (tonumber(data.format) or 0) >= 2 and type(data.palette) == "table" and type(data.positions) == "table" and type(data.states) == "table" then
    local count = math.min(#data.states, math.floor(#data.positions / 3))
    for i = 1, count do
      local pIndex = (data.states[i] or 0) + 1
      local enc = data.palette[pIndex]
      if enc then
        local sep = enc:find("|", 1, true)
        local blockId, stateString
        if sep then
          blockId = enc:sub(1, sep - 1)
          stateString = enc:sub(sep + 1)
        else
          blockId = enc
          stateString = ""
        end
        local base = (i - 1) * 3
        setBlockDirect(tonumber(data.positions[base + 1]) or 0, tonumber(data.positions[base + 2]) or 0, tonumber(data.positions[base + 3]) or 0, blockId, stateString)
      end
    end
  elseif type(data.blocks) == "table" then
    for _, b in ipairs(data.blocks) do
      if type(b) == "table" then
        setBlockDirect(
          tonumber(b.x) or 0,
          tonumber(b.y) or 0,
          tonumber(b.z) or 0,
          b.blockId or "minecraft:air",
          b.blockStateString or ""
        )
      end
    end
  end

  app.undoStack = {}
  app.redoStack = {}
  app.message = "Structure chargée: " .. app.name
end

saveLegacy = function()
  local structureName = sanitizeFileName(app.saveName)
  app.name = structureName
  local fileName = structureName .. ".json"
  local ok, path = saveToFile(fileName, encodeLegacyStrict())
  if ok then
    refreshSaveFileList()
    app.message = "Legacy sauvé: " .. path
  else
    app.message = "Échec sauvegarde: " .. tostring(path)
  end
end

saveCompact = function()
  local structureName = sanitizeFileName(app.saveName)
  app.name = structureName
  local fileName = structureName .. "_compact.json"
  local ok, path = saveToFile(fileName, json.encode(compactDict()))
  if ok then
    refreshSaveFileList()
    app.message = "Compact sauvé: " .. path
  else
    app.message = "Échec sauvegarde: " .. tostring(path)
  end
end

saveWrapper = function()
  local structureName = sanitizeFileName(app.saveName)
  app.name = structureName
  local fileName = structureName .. "_saveddata.json"
  local ok, path = saveToFile(fileName, json.encode(wrapperDict()))
  if ok then
    refreshSaveFileList()
    app.message = "Export NeoForge: " .. path
  else
    app.message = "Échec sauvegarde: " .. tostring(path)
  end
end

local function rotateFacingString(value)
  local map = { north = "east", east = "south", south = "west", west = "north" }
  return map[value] or value
end

local function rotateStateY90(blockId, stateString)
  local presetType = getPresetType(blockId)
  if not presetType then return stateString end
  local props = parseBlockStateString(stateString)
  if props.facing then props.facing = rotateFacingString(props.facing) end

  if presetType == "redstone_wire" then
    local east = props.east
    local south = props.south
    local west = props.west
    local north = props.north
    props.east = north
    props.south = east
    props.west = south
    props.north = west
  end

  return buildBlockStateString(blockId, props)
end

rotate90 = function()
  local changes = {}
  local oldX, oldZ = app.sizeX, app.sizeZ

  for _, b in pairs(app.blocks) do
    changes[#changes + 1] = { before = cloneTable(b), after = {x = b.x, y = b.y, z = b.z, blockId = "minecraft:air", blockStateString = ""} }
  end

  for _, b in pairs(app.blocks) do
    local nx = oldZ - 1 - b.z
    local nz = b.x
    changes[#changes + 1] = {
      before = getBlockSnapshotAt(nx, b.y, nz),
      after = {
        x = nx, y = b.y, z = nz,
        blockId = b.blockId,
        blockStateString = rotateStateY90(b.blockId, b.blockStateString)
      }
    }
  end

  app.sizeX, app.sizeZ = oldZ, oldX
  applyChangeSet(changes, true)
  pushUndo({label = "rotate", changes = changes})
  app.message = "Rotation +90° appliquée"
end

-- =========================
-- Geometry
-- =========================
local function fillRectWrites(a, b, wallOnly)
  local writes = {}
  local minX, maxX = math.min(a.x, b.x), math.max(a.x, b.x)
  local minZ, maxZ = math.min(a.z, b.z), math.max(a.z, b.z)
  for x = minX, maxX do
    for z = minZ, maxZ do
      if (not wallOnly) or x == minX or x == maxX or z == minZ or z == maxZ then
        writes[#writes + 1] = {x = x, y = app.layer, z = z, blockId = app.currentBlockId, blockStateString = app.currentState}
      end
    end
  end
  return writes
end

local function brush3x3Writes(c)
  local writes = {}
  for dx = -1, 1 do
    for dz = -1, 1 do
      writes[#writes + 1] = {x = c.x + dx, y = app.layer, z = c.z + dz, blockId = app.currentBlockId, blockStateString = app.currentState}
    end
  end
  return writes
end

local function drawLineWrites(a, b)
  local writes = {}
  local x0, z0 = a.x, a.z
  local x1, z1 = b.x, b.z
  local dx = math.abs(x1 - x0)
  local dz = math.abs(z1 - z0)
  local sx = x0 < x1 and 1 or -1
  local sz = z0 < z1 and 1 or -1
  local err = dx - dz

  while true do
    writes[#writes + 1] = {x = x0, y = app.layer, z = z0, blockId = app.currentBlockId, blockStateString = app.currentState}
    if x0 == x1 and z0 == z1 then break end
    local e2 = err * 2
    if e2 > -dz then
      err = err - dz
      x0 = x0 + sx
    end
    if e2 < dx then
      err = err + dx
      z0 = z0 + sz
    end
  end
  return writes
end

local function drawEllipseWrites(a, b, outline)
  local writes = {}
  local minX, maxX = math.min(a.x, b.x), math.max(a.x, b.x)
  local minZ, maxZ = math.min(a.z, b.z), math.max(a.z, b.z)

  local width = maxX - minX + 1
  local height = maxZ - minZ + 1
  local cx = minX + (width - 1) / 2
  local cz = minZ + (height - 1) / 2
  local rx = math.max(0.5, width / 2)
  local rz = math.max(0.5, height / 2)

  for x = minX, maxX do
    for z = minZ, maxZ do
      local nx = (x - cx) / rx
      local nz = (z - cz) / rz
      local d = nx * nx + nz * nz

      if outline then
        local innerRx = math.max(0.5, rx - 1)
        local innerRz = math.max(0.5, rz - 1)
        local inx = (x - cx) / innerRx
        local inz = (z - cz) / innerRz
        local innerD = inx * inx + inz * inz
        if d <= 1.0 and innerD >= 1.0 then
          writes[#writes + 1] = {x = x, y = app.layer, z = z, blockId = app.currentBlockId, blockStateString = app.currentState}
        end
      else
        if d <= 1.0 then
          writes[#writes + 1] = {x = x, y = app.layer, z = z, blockId = app.currentBlockId, blockStateString = app.currentState}
        end
      end
    end
  end

  return writes
end

local function drawCircleWrites(a, b, outline)
  local writes = {}
  local dx = b.x - a.x
  local dz = b.z - a.z
  local r = math.max(1, math.floor(math.sqrt(dx * dx + dz * dz) + 0.5))
  local r2 = r * r
  local r1 = math.max(0, r - 1)
  local r12 = r1 * r1

  for x = a.x - r, a.x + r do
    for z = a.z - r, a.z + r do
      local ddx = x - a.x
      local ddz = z - a.z
      local dist2 = ddx * ddx + ddz * ddz
      if outline then
        if dist2 <= r2 and dist2 >= r12 then
          writes[#writes + 1] = {x = x, y = app.layer, z = z, blockId = app.currentBlockId, blockStateString = app.currentState}
        end
      else
        if dist2 <= r2 then
          writes[#writes + 1] = {x = x, y = app.layer, z = z, blockId = app.currentBlockId, blockStateString = app.currentState}
        end
      end
    end
  end
  return writes
end

local function rebuildPreview(mouseWX, mouseWZ)
  app.previewWrites = {}
  if not app.rectStart or mouseWX == nil or mouseWZ == nil then
    return
  end

  local a = app.rectStart
  local b = {x = mouseWX, z = mouseWZ}

  if app.tool == "line" then
    app.previewWrites = drawLineWrites(a, b)
  elseif app.tool == "fillRect" then
    app.previewWrites = fillRectWrites(a, b, false)
  elseif app.tool == "wallRect" then
    app.previewWrites = fillRectWrites(a, b, true)
  elseif app.tool == "circleFill" then
    app.previewWrites = drawCircleWrites(a, b, false)
  elseif app.tool == "circleWall" then
    app.previewWrites = drawCircleWrites(a, b, true)
  elseif app.tool == "ellipseFill" then
    app.previewWrites = drawEllipseWrites(a, b, false)
  elseif app.tool == "ellipseWall" then
    app.previewWrites = drawEllipseWrites(a, b, true)
  end
end

local function visibleGridSize()
  local w, h = love.graphics.getDimensions()
  local cols = math.max(1, math.floor((w - ui.offsetX - 20) / ui.cell))
  local rows = math.max(1, math.floor((h - ui.offsetY - 20) / ui.cell))
  return cols, rows
 end
clampCamera = function()
  local cols, rows = visibleGridSize()
  app.cameraX = math.max(0, math.min(math.max(0, app.sizeX - cols), app.cameraX))
  app.cameraZ = math.max(0, math.min(math.max(0, app.sizeZ - rows), app.cameraZ))
end

local function mouseToCell(mx, my)
  local cols, rows = visibleGridSize()
  local gx = math.floor((mx - ui.offsetX) / ui.cell)
  local gz = math.floor((my - ui.offsetY) / ui.cell)
  if gx >= 0 and gz >= 0 and gx < cols and gz < rows then
    local wx = gx + app.cameraX
    local wz = gz + app.cameraZ
    if wx < app.sizeX and wz < app.sizeZ then return wx, wz, gx, gz end
  end
  return nil, nil, nil, nil
end

local function pointInRect(px, py, r)
  return px >= r.x and py >= r.y and px <= r.x + r.w and py <= r.y + r.h
end

local function cycleProperty(block, propName, direction)
  if not block then return end
  local presetType = getPresetType(block.blockId)
  if not presetType then return end
  local preset = statePresets[presetType]
  local values = preset.values[propName]
  if not values then return end

  local props = parseBlockStateString(block.blockStateString)
  if props[propName] == nil and preset.defaults[propName] ~= nil then props[propName] = preset.defaults[propName] end

  local currentIndex = 1
  for i, v in ipairs(values) do
    if tostring(v) == tostring(props[propName]) then currentIndex = i break end
  end

  currentIndex = currentIndex + direction
  if currentIndex > #values then currentIndex = 1 end
  if currentIndex < 1 then currentIndex = #values end

  props[propName] = values[currentIndex]
  block.blockStateString = buildBlockStateString(block.blockId, props)
  app.message = "State modifié: " .. propName .. "=" .. tostring(values[currentIndex])
end

local function setSelectionFromPoints(a, b)
  app.selection = {
    minX = math.min(a.x, b.x),
    maxX = math.max(a.x, b.x),
    minZ = math.min(a.z, b.z),
    maxZ = math.max(a.z, b.z),
    y = app.layer
  }
  app.message = "Sélection créée."
end

local function moveSelection(dx, dz)
  if not app.selection then
    app.message = "Aucune sélection."
    return
  end

  local sel = app.selection
  local captured = {}
  for _, b in pairs(app.blocks) do
    if b.y == sel.y and b.x >= sel.minX and b.x <= sel.maxX and b.z >= sel.minZ and b.z <= sel.maxZ then
      captured[#captured + 1] = cloneTable(b)
    end
  end

  if #captured == 0 then
    app.message = "Sélection vide."
    return
  end

  local changes = {}
  for _, b in ipairs(captured) do
    changes[#changes + 1] = {before = cloneTable(b), after = {x = b.x, y = b.y, z = b.z, blockId = "minecraft:air", blockStateString = ""}}
  end

  for _, b in ipairs(captured) do
    local nx, nz = b.x + dx, b.z + dz
    changes[#changes + 1] = {
      before = getBlockSnapshotAt(nx, b.y, nz),
      after = {x = nx, y = b.y, z = nz, blockId = b.blockId, blockStateString = b.blockStateString}
    }
  end

  applyChangeSet(changes, true)
  pushUndo({label = "moveSelection", changes = changes})
  app.selection.minX = app.selection.minX + dx
  app.selection.maxX = app.selection.maxX + dx
  app.selection.minZ = app.selection.minZ + dz
  app.selection.maxZ = app.selection.maxZ + dz
  app.message = "Sélection déplacée."
end

local function copySelection()
  if not app.selection then
    app.message = "Aucune sélection à copier."
    return
  end

  local sel = app.selection
  local changes = {}
  for _, b in pairs(app.blocks) do
    if b.y == sel.y and b.x >= sel.minX and b.x <= sel.maxX and b.z >= sel.minZ and b.z <= sel.maxZ then
      local nx = b.x + 1
      local nz = b.z + 1
      changes[#changes + 1] = {
        before = getBlockSnapshotAt(nx, b.y, nz),
        after = {x = nx, y = b.y, z = nz, blockId = b.blockId, blockStateString = b.blockStateString}
      }
    end
  end

  if #changes == 0 then
    app.message = "Sélection vide."
    return
  end

  applyChangeSet(changes, true)
  pushUndo({label = "copySelection", changes = changes})
  app.message = "Copie effectuée."
end

local function clearCurrentLayer()
  local changes = {}
  for _, b in pairs(app.blocks) do
    if b.y == app.layer then
      changes[#changes + 1] = {before = cloneTable(b), after = {x = b.x, y = b.y, z = b.z, blockId = "minecraft:air", blockStateString = ""}}
    end
  end
  if #changes == 0 then
    app.message = "Layer déjà vide."
    return
  end
  applyChangeSet(changes, true)
  pushUndo({label = "clearLayer", changes = changes})
  app.message = "Layer vidé."
end

local function clearAllBlocks()
  local changes = {}
  for _, b in pairs(app.blocks) do
    changes[#changes + 1] = {before = cloneTable(b), after = {x = b.x, y = b.y, z = b.z, blockId = "minecraft:air", blockStateString = ""}}
  end
  if #changes == 0 then
    app.message = "Déjà vide."
    return
  end
  applyChangeSet(changes, true)
  pushUndo({label = "clearAll", changes = changes})
  app.selection = nil
  app.selectedBlockKey = nil
  app.message = "Tout vidé."
end

local function commandColor(item)
  if item.group == "danger" then
    return colors.buttonRed
  elseif item.group == "file" then
    return colors.buttonGreen
  elseif item.group == "view" or item.group == "move" then
    return colors.buttonBrown
  elseif item.group == "tool" or item.group == "select" then
    return colors.buttonBlue
  elseif item.group == "edit" then
    return colors.buttonPurple
  else
    return colors.buttonGray
  end
end

local function runAction(action)
  if action == "tool_single" then app.tool = "single"; app.message = "Outil: pose simple"
  elseif action == "tool_brush" then app.tool = "brush"; app.message = "Outil: brush 3x3"
  elseif action == "tool_line" then app.tool = "line"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: ligne"
  elseif action == "tool_fill" then app.tool = "fillRect"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: rectangle plein"
  elseif action == "tool_wall" then app.tool = "wallRect"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: rectangle contour"
  elseif action == "tool_circle_fill" then app.tool = "circleFill"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: cercle plein"
  elseif action == "tool_circle_wall" then app.tool = "circleWall"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: cercle contour"
  elseif action == "tool_ellipse_fill" then app.tool = "ellipseFill"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: ellipse pleine"
  elseif action == "tool_ellipse_wall" then app.tool = "ellipseWall"; app.rectStart = nil; app.previewWrites = {}; app.message = "Outil: ellipse contour"
  elseif action == "tool_select" then
  if app.selection then
    app.selection = nil
    app.rectStart = nil
    app.previewWrites = {}
    app.message = "Sélection retirée"
  else
    app.tool = "selectRect"
    app.rectStart = nil
    app.previewWrites = {}
    app.message = "Outil: sélection rectangulaire"
  end
  elseif action == "cancel_rect" then app.rectStart = nil; app.previewWrites = {}; app.message = "Sélection annulée"
  elseif action == "move_left" then moveSelection(-1, 0)
  elseif action == "move_right" then moveSelection(1, 0)
  elseif action == "move_up" then moveSelection(0, -1)
  elseif action == "move_down" then moveSelection(0, 1)
  elseif action == "undo" then undo()
  elseif action == "redo" then redo()
  elseif action == "copy_selection" then copySelection()
  elseif action == "save_legacy" then saveLegacy()
  elseif action == "save_wrapper" then saveWrapper()
  elseif action == "save_compact" then saveCompact()
  elseif action == "rotate" then rotate90()
  elseif action == "clear_layer" then clearCurrentLayer()
  elseif action == "clear_all" then clearAllBlocks()
  elseif action == "toggle_grid" then app.showGrid = not app.showGrid; app.message = app.showGrid and "Grille activée" or "Grille désactivée"
  elseif action == "layer_up" then app.layer = math.min(app.sizeY - 1, app.layer + 1)
  elseif action == "layer_down" then app.layer = math.max(0, app.layer - 1)
  elseif action == "zoom_in" then ui.cell = math.min(56, ui.cell + 2)
  elseif action == "zoom_out" then ui.cell = math.max(8, ui.cell - 2)
  elseif action == "cam_up" then app.cameraZ = app.cameraZ - 4
  elseif action == "cam_down" then app.cameraZ = app.cameraZ + 4
  elseif action == "cam_left" then app.cameraX = app.cameraX - 4
  elseif action == "cam_right" then app.cameraX = app.cameraX + 4
  elseif action == "next_block" then
    local source = (#app.filteredPalette > 0) and app.filteredPalette or app.palette
    if #source > 0 then
      local idx = 1
      for i, v in ipairs(source) do if v == app.currentBlockId then idx = i break end end
      idx = idx % #source + 1
      app.currentBlockId = source[idx]
      app.currentState = makeDefaultStateString(app.currentBlockId)
      app.message = "Bloc courant: " .. app.currentBlockId
    end
  elseif action == "prev_block" then
    local source = (#app.filteredPalette > 0) and app.filteredPalette or app.palette
    if #source > 0 then
      local idx = 1
      for i, v in ipairs(source) do if v == app.currentBlockId then idx = i break end end
      idx = idx - 1
      if idx < 1 then idx = #source end
      app.currentBlockId = source[idx]
      app.currentState = makeDefaultStateString(app.currentBlockId)
      app.message = "Bloc courant: " .. app.currentBlockId
    end
  elseif action == "toggle_name" then
    app.editingName = not app.editingName
    if app.editingName then app.editingSearch = false end
  elseif action == "toggle_search" then
    app.editingSearch = not app.editingSearch
    if app.editingSearch then app.editingName = false end
  end
  clampCamera()
end

-- =========================
-- Love callbacks
-- =========================
function love.load()
  love.window.setTitle("Magic Chalk Studio")
  love.window.setMode(1700, 980, {resizable = true, minwidth = 1280, minheight = 780})
  love.graphics.setBackgroundColor(colors.bg)
  setupSaveDirectory()
  loadBlocksFile()
  app.currentState = makeDefaultStateString(app.currentBlockId)
  rebuildFilteredPalette()
  refreshSaveFileList()
  app.loadPath = (#app.saveFiles > 0 and app.saveFiles[1]) or ""
  app.message = "Dossier prêt. " .. app.savePathInfo
local icon = love.image.newImageData("magic_chalk.png")
    love.window.setIcon(icon)
  if localAppDir then
    local iconPath = joinPath(localAppDir, "magic_chalk.png")
    local imgData = pcall(function()
      return love.image.newImageData(iconPath)
    end)
    if imgData and type(imgData) == "userdata" then
      love.window.setIcon(imgData)
    end
  end
end

function love.filedropped(file)
  app.message = "Fichier détecté: " .. tostring(file:getFilename())

  local filename = file:getFilename()
  local lower = filename:lower()
  if not lower:match("%.json$") then
    app.message = "Le fichier doit être un .json"
    return
  end

  local contents = nil
  local ok1 = pcall(function()
    file:open("r")
    contents = file:read()
    file:close()
  end)

  if (not ok1) or contents == nil or contents == "" then
    local ok2, result = pcall(function()
      local f = io.open(filename, "rb")
      if not f then return nil end
      local data = f:read("*a")
      f:close()
      return data
    end)
    if ok2 then contents = result end
  end

  if not contents or contents == "" then
    app.message = "Impossible de lire le fichier JSON."
    return
  end

  local okJson, data = pcall(json.decode, contents)
  if not okJson then
    app.message = "JSON invalide: " .. tostring(data)
    return
  end

  local okLoad, errLoad = pcall(function()
    loadStructure(data)
  end)
  if not okLoad then
    app.message = "Erreur chargement structure: " .. tostring(errLoad)
    return
  end

  refreshSaveFileList()
  app.message = "Structure chargée: " .. tostring(app.name)
end

function love.directorydropped(path)
  app.message = "Dossier détecté, glisse un fichier JSON et non un dossier: " .. tostring(path)
end

function love.textinput(t)
  if app.editingName and t ~= "\r" and t ~= "\n" then
    app.saveName = app.saveName .. t
  elseif app.editingSearch and t ~= "\r" and t ~= "\n" then
    app.searchText = app.searchText .. t
    rebuildFilteredPalette()
  end
end

function love.keypressed(key)
  if app.editingName or app.editingSearch then
    if key == "backspace" then
      if app.editingName then
        local byteoffset = utf8.offset(app.saveName, -1)
        if byteoffset then app.saveName = app.saveName:sub(1, byteoffset - 1) end
      elseif app.editingSearch then
        local byteoffset = utf8.offset(app.searchText, -1)
        if byteoffset then
          app.searchText = app.searchText:sub(1, byteoffset - 1)
          rebuildFilteredPalette()
        end
      end
    elseif key == "return" or key == "kpenter" or key == "escape" then
      app.editingName = false
      app.editingSearch = false
      app.message = "Édition arrêtée"
    end
    return
  end

  if key == "return" or key == "kpenter" then
    app.editingName = true
    app.editingSearch = false
    app.message = "Édition du nom: ON"
  elseif key == "slash" then
    app.editingSearch = true
    app.editingName = false
    app.message = "Recherche blocs: ON"
  elseif key == "z" and (love.keyboard.isDown("lctrl") or love.keyboard.isDown("rctrl")) then
    undo()
  elseif key == "y" and (love.keyboard.isDown("lctrl") or love.keyboard.isDown("rctrl")) then
    redo()
  elseif key == "left" then
    app.cameraX = app.cameraX - 4
  elseif key == "right" then
    app.cameraX = app.cameraX + 4
  elseif key == "up" then
    app.cameraZ = app.cameraZ - 4
  elseif key == "down" then
    app.cameraZ = app.cameraZ + 4
  elseif key == "pageup" then
    app.layer = math.min(app.sizeY - 1, app.layer + 1)
  elseif key == "pagedown" then
    app.layer = math.max(0, app.layer - 1)
  elseif key == "escape" then
    app.rectStart = nil
    app.previewWrites = {}
    app.message = "Sélection annulée"
  end
  clampCamera()
end

function love.wheelmoved(x, y)
  local mx, my = love.mouse.getPosition()

  if pointInRect(mx, my, ui.paletteBox) then
    local maxScroll = math.max(0, #app.filteredPalette - paletteVisibleLines())
    ui.paletteScroll = math.max(0, math.min(maxScroll, ui.paletteScroll - y))
    return
  end

  if pointInRect(mx, my, ui.saveListBox) then
    local maxScroll = math.max(0, #app.saveFiles - saveVisibleLines())
    ui.saveScroll = math.max(0, math.min(maxScroll, ui.saveScroll - y))
    return
  end

  if pointInRect(mx, my, ui.cmdBox) then
    local maxScroll = math.max(0, commandButtonRows() - buttonVisibleRows())
    ui.cmdScroll = math.max(0, math.min(maxScroll, ui.cmdScroll - y))
    return
  end

  if love.keyboard.isDown("lctrl", "rctrl") then
    if y > 0 then ui.cell = math.min(56, ui.cell + 2)
    elseif y < 0 then ui.cell = math.max(8, ui.cell - 2) end
  else
    app.layer = math.max(0, math.min(app.sizeY - 1, app.layer + (y > 0 and 1 or -1)))
  end
  clampCamera()
end

function love.mousemoved(x, y, dx, dy)
  local wx, wz = mouseToCell(x, y)
  rebuildPreview(wx, wz)
end

function love.mousepressed(x, y, button)
  if ui.editNameButton and pointInRect(x, y, ui.editNameButton) then
    runAction("toggle_name")
    return
  end

  if ui.editSearchButton and pointInRect(x, y, ui.editSearchButton) then
    runAction("toggle_search")
    return
  end

  if ui.saveButton and pointInRect(x, y, ui.saveButton) then
    saveLegacy()
    return
  end

  if ui.loadButton and pointInRect(x, y, ui.loadButton) then
    loadFromSaveFile(app.loadPath)
    return
  end

  for _, btn in ipairs(ui.fileButtons) do
    if pointInRect(x, y, btn.rect) then
      app.loadPath = btn.name
      if button == 1 then
        loadFromSaveFile(btn.name)
      end
      return
    end
  end

  for _, btn in ipairs(ui.actionButtons) do
    if pointInRect(x, y, btn.rect) then
      runAction(btn.action)
      return
    end
  end

  if pointInRect(x, y, ui.paletteBox) then
    local idx = ui.paletteScroll + math.floor((y - ui.paletteBox.y - 4) / ui.lineH) + 1
    if app.filteredPalette[idx] then
      app.currentBlockId = app.filteredPalette[idx]
      app.currentState = makeDefaultStateString(app.currentBlockId)
      app.message = "Bloc choisi: " .. app.currentBlockId
    end
    return
  end

  for _, btn in ipairs(ui.stateButtons) do
    if pointInRect(x, y, btn.rect) then
      local block = getSelectedBlock()
      if block then cycleProperty(block, btn.prop, btn.dir) end
      return
    end
  end

  local wx, wz = mouseToCell(x, y)
  if not wx then return end
  local key = blockKey(wx, app.layer, wz)
  local existing = app.blocks[key]

  if love.keyboard.isDown("lshift", "rshift") and button == 1 then
    if existing then
      app.selectedBlockKey = key
      app.message = "Bloc sélectionné: " .. existing.blockId
    else
      app.selectedBlockKey = nil
      app.message = "Aucun bloc à sélectionner."
    end
    return
  end

  if button == 1 then
    if app.tool == "single" then
      applyWrites({{x = wx, y = app.layer, z = wz, blockId = app.currentBlockId, blockStateString = app.currentState}}, "single")
    elseif app.tool == "brush" then
      applyWrites(brush3x3Writes({x = wx, z = wz}), "brush")
    elseif app.tool == "fillRect" or app.tool == "wallRect" or app.tool == "line" or app.tool == "circleFill"
        or app.tool == "circleWall" or app.tool == "ellipseFill" or app.tool == "ellipseWall" or app.tool == "selectRect" then
      if not app.rectStart then
        app.rectStart = {x = wx, z = wz}
        rebuildPreview(wx, wz)
        app.message = "Point A défini: " .. wx .. ", " .. wz
      else
        if app.tool == "fillRect" then
          applyWrites(fillRectWrites(app.rectStart, {x = wx, z = wz}, false), "fillRect")
        elseif app.tool == "wallRect" then
          applyWrites(fillRectWrites(app.rectStart, {x = wx, z = wz}, true), "wallRect")
        elseif app.tool == "line" then
          applyWrites(drawLineWrites(app.rectStart, {x = wx, z = wz}), "line")
        elseif app.tool == "circleFill" then
          applyWrites(drawCircleWrites(app.rectStart, {x = wx, z = wz}, false), "circleFill")
        elseif app.tool == "circleWall" then
          applyWrites(drawCircleWrites(app.rectStart, {x = wx, z = wz}, true), "circleWall")
        elseif app.tool == "ellipseFill" then
          applyWrites(drawEllipseWrites(app.rectStart, {x = wx, z = wz}, false), "ellipseFill")
        elseif app.tool == "ellipseWall" then
          applyWrites(drawEllipseWrites(app.rectStart, {x = wx, z = wz}, true), "ellipseWall")
        elseif app.tool == "selectRect" then
          setSelectionFromPoints(app.rectStart, {x = wx, z = wz})
        end
        app.rectStart = nil
        app.previewWrites = {}
      end
    end
  elseif button == 2 then
    applyWrites({{x = wx, y = app.layer, z = wz, blockId = "minecraft:air", blockStateString = ""}}, "erase")
  end
end

function love.draw()
  local _, h = love.graphics.getDimensions()
  clampCamera()
  ui.stateButtons = {}

  love.graphics.setColor(colors.panel)
  love.graphics.rectangle("fill", 0, 0, ui.sidebarW, h)

  love.graphics.setColor(colors.text)
  love.graphics.print("Magic Chalk Studio - By MickDev", 14, 14)
  love.graphics.setColor(colors.sub)
  love.graphics.printf(app.savePathInfo, 14, 40, ui.sidebarW - 28)

  local y = 74
  love.graphics.setColor(colors.text)
  love.graphics.print("Nom structure: " .. app.name, 14, y); y = y + 22
  love.graphics.setColor(colors.sub)
  love.graphics.printf(("Taille: %d x %d x %d  |  Layer: %d"):format(app.sizeX, app.sizeY, app.sizeZ, app.layer), 14, y, ui.sidebarW - 28); y = y + 22
  love.graphics.printf(("Outil: %s  |  Caméra X/Z: %d / %d"):format(app.tool, app.cameraX, app.cameraZ), 14, y, ui.sidebarW - 28); y = y + 24
  love.graphics.printf(("Blocs: %d  |  Palette utilisée: %d  |  Undo: %d  Redo: %d"):format(countBlocks(), uniquePaletteCount(), #app.undoStack, #app.redoStack), 14, y, ui.sidebarW - 28); y = y + 26

  love.graphics.setColor(colors.text)
  love.graphics.print("Nom du JSON", 14, y); y = y + 22
  local boxW = ui.sidebarW - 148
  love.graphics.setColor(app.editingName and colors.accent or colors.panel2)
  love.graphics.rectangle("fill", 14, y, boxW, 32, 8, 8)
  love.graphics.setColor(colors.text)
  love.graphics.printf(app.saveName .. (app.editingName and " | EDIT" or ""), 20, y + 8, boxW - 10)
  ui.editNameButton = {x = 14 + boxW + 8, y = y, w = 56, h = 32}
  ui.saveButton = {x = ui.editNameButton.x + 64, y = y, w = 62, h = 32}
  love.graphics.setColor(colors.buttonBlue)
  love.graphics.rectangle("fill", ui.editNameButton.x, ui.editNameButton.y, ui.editNameButton.w, ui.editNameButton.h, 8, 8)
  love.graphics.setColor(colors.buttonGreen)
  love.graphics.rectangle("fill", ui.saveButton.x, ui.saveButton.y, ui.saveButton.w, ui.saveButton.h, 8, 8)
  love.graphics.setColor(colors.text)
  love.graphics.printf("Edit", ui.editNameButton.x, ui.editNameButton.y + 8, ui.editNameButton.w, "center")
  love.graphics.printf("Save", ui.saveButton.x, ui.saveButton.y + 8, ui.saveButton.w, "center")
  y = y + 44

  love.graphics.setColor(colors.text)
  love.graphics.print("Chargement JSON", 14, y + 2)
  local loadY = y + 24
  ui.loadPathBox = {x = 14, y = loadY, w = ui.sidebarW - 140, h = 32}
  ui.loadButton = {x = ui.sidebarW - 114, y = loadY, w = 100, h = 32}
  love.graphics.setColor(colors.panel2)
  love.graphics.rectangle("fill", ui.loadPathBox.x, ui.loadPathBox.y, ui.loadPathBox.w, ui.loadPathBox.h, 8, 8)
  love.graphics.setColor(colors.text)
  love.graphics.printf(app.loadPath, ui.loadPathBox.x + 6, ui.loadPathBox.y + 8, ui.loadPathBox.w - 12)
  love.graphics.setColor(colors.buttonGreen)
  love.graphics.rectangle("fill", ui.loadButton.x, ui.loadButton.y, ui.loadButton.w, ui.loadButton.h, 8, 8)
  love.graphics.setColor(colors.text)
  love.graphics.printf("Charger", ui.loadButton.x, ui.loadButton.y + 8, ui.loadButton.w, "center")
  y = y + 64

  love.graphics.setColor(colors.text)
  love.graphics.print("Recherche bloc", 14, y); y = y + 22
  love.graphics.setColor(app.editingSearch and colors.accent or colors.panel2)
  love.graphics.rectangle("fill", 14, y, ui.sidebarW - 96, 32, 8, 8)
  love.graphics.setColor(colors.text)
  love.graphics.printf(app.searchText .. (app.editingSearch and " | EDIT" or ""), 20, y + 8, ui.sidebarW - 110)
  ui.editSearchButton = {x = ui.sidebarW - 72, y = y, w = 58, h = 32}
  love.graphics.setColor(colors.buttonBlue)
  love.graphics.rectangle("fill", ui.editSearchButton.x, ui.editSearchButton.y, ui.editSearchButton.w, ui.editSearchButton.h, 8, 8)
  love.graphics.setColor(colors.text)
  love.graphics.printf("Edit", ui.editSearchButton.x, ui.editSearchButton.y + 8, ui.editSearchButton.w, "center")
  y = y + 50

  ui.paletteBox.x, ui.paletteBox.y, ui.paletteBox.w = 14, y + 8, ui.sidebarW - 28
  ui.paletteBox.h = 180
  love.graphics.setColor(colors.text)
  love.graphics.print("Palette blocs", 14, ui.paletteBox.y - 22)
  love.graphics.setColor(colors.panel2)
  love.graphics.rectangle("fill", ui.paletteBox.x, ui.paletteBox.y, ui.paletteBox.w, ui.paletteBox.h, 8, 8)
  safeScissor(ui.paletteBox.x, ui.paletteBox.y, ui.paletteBox.w, ui.paletteBox.h)
  local pVisible = paletteVisibleLines()
  for i = 1, pVisible do
    local idx = ui.paletteScroll + i
    local blockId = app.filteredPalette[idx]
    if blockId then
      local rowY = ui.paletteBox.y + 4 + (i - 1) * ui.lineH
      local active = (blockId == app.currentBlockId)
      love.graphics.setColor(active and colors.accent or colors.panel)
      love.graphics.rectangle("fill", ui.paletteBox.x + 4, rowY, ui.paletteBox.w - 18, ui.lineH - 2, 6, 6)
      love.graphics.setColor(colors.text)
      love.graphics.print(blockId, ui.paletteBox.x + 10, rowY + 4)
    end
  end
  safeScissor()
  drawScrollBar(ui.paletteBox, #app.filteredPalette, ui.paletteScroll, ui.lineH)

  ui.stateBox.x, ui.stateBox.y, ui.stateBox.w = 14, ui.paletteBox.y + ui.paletteBox.h + 20, ui.sidebarW - 28
  ui.stateBox.h = 150
  love.graphics.setColor(colors.text)
  love.graphics.print("Édition du bloc sélectionné", 14, ui.stateBox.y - 22)
  love.graphics.setColor(colors.panel2)
  love.graphics.rectangle("fill", ui.stateBox.x, ui.stateBox.y, ui.stateBox.w, ui.stateBox.h, 8, 8)

  local selected = getSelectedBlock()
  if selected then
    love.graphics.setColor(colors.text)
    love.graphics.printf(selected.blockId, ui.stateBox.x + 8, ui.stateBox.y + 10, ui.stateBox.w - 16)
    love.graphics.setColor(colors.sub)
    love.graphics.printf(selected.blockStateString or "", ui.stateBox.x + 8, ui.stateBox.y + 30, ui.stateBox.w - 16)
    local presetType = getPresetType(selected.blockId)
    if presetType then
      local preset = statePresets[presetType]
      local props = parseBlockStateString(selected.blockStateString)
      local by = ui.stateBox.y + 62
      for _, prop in ipairs(preset.order) do
        local value = props[prop]
        if value == nil then value = preset.defaults[prop] end

        love.graphics.setColor(colors.text)
        love.graphics.print(prop .. ": " .. tostring(value), ui.stateBox.x + 8, by + 6)

        local leftRect = {x = ui.stateBox.x + ui.stateBox.w - 90, y = by, w = 34, h = 24}
        local rightRect = {x = ui.stateBox.x + ui.stateBox.w - 46, y = by, w = 34, h = 24}

        love.graphics.setColor(colors.warn)
        love.graphics.rectangle("fill", leftRect.x, leftRect.y, leftRect.w, leftRect.h, 6, 6)
        love.graphics.setColor(colors.accent)
        love.graphics.rectangle("fill", rightRect.x, rightRect.y, rightRect.w, rightRect.h, 6, 6)
        love.graphics.setColor(1,1,1)
        love.graphics.printf("<", leftRect.x, leftRect.y + 5, leftRect.w, "center")
        love.graphics.printf(">", rightRect.x, rightRect.y + 5, rightRect.w, "center")

        ui.stateButtons[#ui.stateButtons + 1] = {rect = leftRect, prop = prop, dir = -1}
        ui.stateButtons[#ui.stateButtons + 1] = {rect = rightRect, prop = prop, dir = 1}

        by = by + 28
        if by > ui.stateBox.y + ui.stateBox.h - 28 then break end
      end
    else
      love.graphics.setColor(colors.sub)
      love.graphics.print("Pas de state éditable pour ce bloc.", ui.stateBox.x + 8, ui.stateBox.y + 62)
    end
  else
    love.graphics.setColor(colors.sub)
    love.graphics.print("Shift + clic gauche sur un bloc posé pour le sélectionner.", ui.stateBox.x + 8, ui.stateBox.y + 12)
    love.graphics.print("Outil 'Sélection zone' puis 2 clics pour sélectionner et déplacer.", ui.stateBox.x + 8, ui.stateBox.y + 36)
  end

  ui.saveListBox.x, ui.saveListBox.y, ui.saveListBox.w = 14, ui.stateBox.y + ui.stateBox.h + 20, ui.sidebarW - 28
  ui.saveListBox.h = 110
  love.graphics.setColor(colors.text)
  love.graphics.print("JSON dans le dossier de l'application", 14, ui.saveListBox.y - 22)
  love.graphics.setColor(colors.panel2)
  love.graphics.rectangle("fill", ui.saveListBox.x, ui.saveListBox.y, ui.saveListBox.w, ui.saveListBox.h, 8, 8)
  safeScissor(ui.saveListBox.x, ui.saveListBox.y, ui.saveListBox.w, ui.saveListBox.h)
  ui.fileButtons = {}
  local sVisible = saveVisibleLines()
  for i = 1, sVisible do
    local idx = ui.saveScroll + i
    local name = app.saveFiles[idx]
    if name then
      local rowY = ui.saveListBox.y + 4 + (i - 1) * ui.lineH
      local rect = {x = ui.saveListBox.x + 4, y = rowY, w = ui.saveListBox.w - 18, h = ui.lineH - 2}
      local active = (name == app.loadPath)
      love.graphics.setColor(active and colors.accent or colors.panel)
      love.graphics.rectangle("fill", rect.x, rect.y, rect.w, rect.h, 6, 6)
      love.graphics.setColor(colors.text)
      love.graphics.print(name, rect.x + 8, rect.y + 4)
      ui.fileButtons[#ui.fileButtons + 1] = {rect = rect, name = name}
    end
  end
  safeScissor()
  drawScrollBar(ui.saveListBox, #app.saveFiles, ui.saveScroll, ui.lineH)

  ui.cmdBox.x, ui.cmdBox.y, ui.cmdBox.w = 14, ui.saveListBox.y + ui.saveListBox.h + 26, ui.sidebarW - 28
  ui.cmdBox.h = math.max(88, h - ui.cmdBox.y - 86)
  love.graphics.setColor(colors.text)
  love.graphics.print("Commandes", 14, ui.cmdBox.y - 22)
  love.graphics.setColor(colors.panel2)
  love.graphics.rectangle("fill", ui.cmdBox.x, ui.cmdBox.y, ui.cmdBox.w, ui.cmdBox.h, 8, 8)

  safeScissor(ui.cmdBox.x, ui.cmdBox.y, ui.cmdBox.w, ui.cmdBox.h)
  ui.actionButtons = {}

  local columns = ui.cmdColumns
  local colGap = 8
  local rowH = ui.cmdButtonH + ui.cmdButtonGap
  local innerW = ui.cmdBox.w - 18
  local buttonW = math.floor((innerW - (columns - 1) * colGap) / columns)
  local startRow = ui.cmdScroll + 1
  local maxRows = buttonVisibleRows()

  for row = 1, maxRows do
    local rowIndex = startRow + row - 1
    local rowY = ui.cmdBox.y + 6 + (row - 1) * rowH
    for col = 1, columns do
      local idx = (rowIndex - 1) * columns + col
      local item = commandButtons[idx]
      if item then
        local rect = {
          x = ui.cmdBox.x + 4 + (col - 1) * (buttonW + colGap),
          y = rowY,
          w = buttonW,
          h = ui.cmdButtonH
        }
        love.graphics.setColor(commandColor(item))
        love.graphics.rectangle("fill", rect.x, rect.y, rect.w, rect.h, 8, 8)
        love.graphics.setColor(0.95, 0.95, 0.98)
        love.graphics.rectangle("line", rect.x, rect.y, rect.w, rect.h, 8, 8)
        love.graphics.printf(item.label, rect.x + 6, rect.y + 9, rect.w - 12, "center")
        ui.actionButtons[#ui.actionButtons + 1] = {rect = rect, action = item.action}
      end
    end
  end
  safeScissor()
  drawScrollBar(ui.cmdBox, commandButtonRows(), ui.cmdScroll, rowH)

  love.graphics.setColor(colors.text)
  love.graphics.print("Message", 14, h - 58)
  love.graphics.setColor(colors.sub)
  love.graphics.printf(app.message, 14, h - 36, ui.sidebarW - 28)

  local gx0, gy0 = ui.offsetX, ui.offsetY
  local cols, rows = visibleGridSize()
  local mx, my = love.mouse.getPosition()
  local hoverWX, hoverWZ, hoverGX, hoverGZ = mouseToCell(mx, my)

  love.graphics.setColor(colors.sub)
  love.graphics.printf("Sauvegarde et chargement dans le dossier de l'application. Pas dans %appdata%%/LOVE.", gx0, 40, 860)

  love.graphics.setColor(colors.panel2)
  love.graphics.rectangle("fill", gx0 - 12, gy0 - 12, cols * ui.cell + 24, rows * ui.cell + 24, 8, 8)

  for gx = 0, cols - 1 do
    for gz = 0, rows - 1 do
      local wx = gx + app.cameraX
      local wz = gz + app.cameraZ
      if wx < app.sizeX and wz < app.sizeZ then
        local sx = gx0 + gx * ui.cell
        local sy = gy0 + gz * ui.cell
        local key = blockKey(wx, app.layer, wz)
        local b = app.blocks[key]

        if b then
          love.graphics.setColor(getBlockColor(b.blockId))
          love.graphics.rectangle("fill", sx, sy, ui.cell - 1, ui.cell - 1)

          if app.selectedBlockKey == key then
            love.graphics.setColor(colors.selected)
            love.graphics.rectangle("fill", sx, sy, ui.cell - 1, ui.cell - 1)
          end

          love.graphics.setColor(1, 1, 1)
          local text = getBlockGlyph(b)
          love.graphics.printf(text, sx, sy + math.max(2, ui.cell * 0.18), ui.cell - 1, "center")
        else
          love.graphics.setColor(colors.empty)
          love.graphics.rectangle("fill", sx, sy, ui.cell - 1, ui.cell - 1)
        end

        if app.showGrid then
          love.graphics.setColor(colors.grid)
          love.graphics.rectangle("line", sx, sy, ui.cell - 1, ui.cell - 1)
        end

        if app.selection and app.selection.y == app.layer and wx >= app.selection.minX and wx <= app.selection.maxX and wz >= app.selection.minZ and wz <= app.selection.maxZ then
          love.graphics.setColor(colors.selectionOutline)
          love.graphics.rectangle("line", sx + 1, sy + 1, ui.cell - 3, ui.cell - 3)
        end
      end
    end
  end

  if hoverWX then
    local sx = gx0 + hoverGX * ui.cell
    local sy = gy0 + hoverGZ * ui.cell
    love.graphics.setColor(colors.hover)
    love.graphics.rectangle("fill", sx, sy, ui.cell - 1, ui.cell - 1)

    love.graphics.setColor(colors.text)
    love.graphics.printf(("Layer %d | x=%d z=%d"):format(app.layer, hoverWX, hoverWZ), gx0, gy0 + rows * ui.cell + 12, 400)
  end

  if app.previewWrites and #app.previewWrites > 0 then
    for _, pw in ipairs(app.previewWrites) do
      if pw.y == app.layer then
        local pgx = pw.x - app.cameraX
        local pgz = pw.z - app.cameraZ
        if pgx >= 0 and pgz >= 0 and pgx < cols and pgz < rows then
          local psx = gx0 + pgx * ui.cell
          local psy = gy0 + pgz * ui.cell
          local pr, pg, pb = getBlockColor(pw.blockId)
          love.graphics.setColor(pr, pg, pb, 0.35)
          love.graphics.rectangle("fill", psx, psy, ui.cell - 1, ui.cell - 1)
          love.graphics.setColor(1, 1, 0.2, 0.9)
          love.graphics.rectangle("line", psx, psy, ui.cell - 1, ui.cell - 1)
        end
      end
    end
  elseif app.rectStart and app.tool == "selectRect" then
    local mx2, mz2 = hoverWX, hoverWZ
    if mx2 and mz2 then
      local minX, maxX = math.min(app.rectStart.x, mx2), math.max(app.rectStart.x, mx2)
      local minZ, maxZ = math.min(app.rectStart.z, mz2), math.max(app.rectStart.z, mz2)
      local sx = gx0 + (minX - app.cameraX) * ui.cell
      local sy = gy0 + (minZ - app.cameraZ) * ui.cell
      local sw = (maxX - minX + 1) * ui.cell
      local sh = (maxZ - minZ + 1) * ui.cell
      love.graphics.setColor(1, 1, 0.2, 0.8)
      love.graphics.rectangle("line", sx, sy, sw, sh)
    end
  end
end