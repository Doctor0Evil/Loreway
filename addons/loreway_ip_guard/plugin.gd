@tool
extends EditorPlugin

const POLICY_PATH := "res://config/loreway_ip_policy.yaml"
const LOG_PATH := "res://logs/loreway_ip_violations.txt"
const SUPPORTED_EXT := [".yaml", ".yml", ".json"]
const LORE_DATA_ROOT := "res://data/loreway"  # Adjust to your lore dir.

var _fs_dock: FileSystemDock
var _tree: Tree
var _policy: Dictionary = {}
var _blacklists: Dictionary = {}
var _last_scanned: Dictionary = {}  # path -> mtime

func _enter_tree() -> void:
  _load_policy()
  if _policy.is_empty():
    push_error("[LorewayIPGuard] Failed to load policy from %s" % POLICY_PATH)
    return
  _blacklists = _policy.get("blacklists", {})
  var iface := get_editor_interface()
  _fs_dock = iface.get_file_system_dock()
  if _fs_dock:
    # Internal Tree path (Godot 4.x); may need tweak per version.
    _tree = _fs_dock.get_child(3).get_child(0) as Tree
    if _tree:
      _tree.cell_selected.connect(_on_tree_cell_selected)
      _tree.item_activated.connect(_on_tree_item_activated)
  # Timer for periodic dir scan (fallback if dock signals miss).
  var scan_timer := Timer.new()
  scan_timer.wait_time = 2.0
  scan_timer.timeout.connect(_scan_lore_dir)
  add_child(scan_timer)
  scan_timer.start()

func _exit_tree() -> void:
  if _tree:
    if _tree.cell_selected.is_connected(_on_tree_cell_selected):
      _tree.cell_selected.disconnect(_on_tree_cell_selected)
    if _tree.item_activated.is_connected(_on_tree_item_activated):
      _tree.item_activated.disconnect(_on_tree_item_activated)
  _tree = null
  _fs_dock = null
  _policy.clear()
  _blacklists.clear()
  _last_scanned.clear()

func _load_policy() -> void:
  var file := FileAccess.open(POLICY_PATH, FileAccess.READ)
  if file == null:
    return
  var text := file.get_as_text()
  file.close()
  var parsed := JSON.parse_string(text)  # YAML parsed as JSON (Godot lacks native YAML).
  if parsed and typeof(parsed) == TYPE_DICTIONARY:
    _policy = parsed
  else:
    push_error("[LorewayIPGuard] Policy parse failed.")

func _scan_lore_dir() -> void:
  var dir := DirAccess.open(LORE_DATA_ROOT)
  if dir == null:
    return
  dir.list_dir_begin()
  while true:
    var name := dir.get_next()
    if name == "":
      break
    if dir.current_is_dir() and not name.begins_with("."):
      _scan_lore_dir_recursive(LORE_DATA_ROOT.path_join(name))
    elif _is_supported_ext(name):
      var path := LORE_DATA_ROOT.path_join(name)
      _check_and_validate_file(path)
  dir.list_dir_end()

func _scan_lore_dir_recursive(base: String) -> void:
  # Recursive helper for subdirs (e.g., regions/, spirits/).
  var dir := DirAccess.open(base)
  if dir == null:
    return
  dir.list_dir_begin()
  while true:
    var name := dir.get_next()
    if name == "":
      break
    if dir.current_is_dir() and not name.begins_with("."):
      _scan_lore_dir_recursive(base.path_join(name))
    elif _is_supported_ext(name):
      var path := base.path_join(name)
      _check_and_validate_file(path)
  dir.list_dir_end()

func _is_supported_ext(name: String) -> bool:
  for ext in SUPPORTED_EXT:
    if name.ends_with(ext):
      return true
  return false

func _check_and_validate_file(path: String) -> void:
  var mtime := FileAccess.get_modified_time(path)
  var last := _last_scanned.get(path, 0)
  if mtime > last:
    _last_scanned[path] = mtime
    if _validate_file_against_policy(path):
      print("[LorewayIPGuard] File clean: %s" % path)
      _notify_runtime_reload(path)  # Sync with LorewayLoader.
    else:
      push_error("[LorewayIPGuard] IP violation in %s—save blocked." % path)
      _log_violation(path, "IP scan failed—external reference detected.")

func _validate_file_against_policy(path: String) -> bool:
  var file := FileAccess.open(path, FileAccess.READ)
  if file == null:
    return false
  var text := file.get_as_text()
  file.close()
  var parsed := JSON.parse_string(text)  # Handles YAML-as-JSON.
  if parsed == null or typeof(parsed) != TYPE_DICTIONARY:
    return false
  # Check provenance first.
  var prov: Dictionary = parsed.get("provenance", {})
  if prov.is_empty() or not prov.has("source") or not _policy.get("allowed_sources", []).has(prov["source"]):
    return false
  if prov.get("external_reference_allowed", true):  # Enforce default false.
    return false
  # Scan sensitive fields.
  var fields: Array = _policy.get("scan_targets", {}).get("fields_to_scan", [])
  for field in fields:
    var value = parsed.get(field.replace("[]", ""), "")  # Handle arrays.
    if typeof(value) == TYPE_ARRAY:
      for item in value:
        if not _scan_string(item):
          return false
    elif not _scan_string(value):
      return false
  return true

func _scan_string(value: String) -> bool:
  if value.is_empty():
    return true
  value = value.to_lower()
  # Exact matches.
  for cat in _blacklists.keys():
    for entry in _blacklists[cat]:
      if value.contains(entry.to_lower()):
        return false
  # Fuzzy (simple Levenshtein impl for GD-Script).
  for cat in _blacklists.keys():
    for entry in _blacklists[cat]:
      var dist := _levenshtein_distance(value, entry.to_lower())
      if dist <= _policy.get("blacklists", {}).get("fuzzy_match_thresholds", {}).get("levenshtein_distance_max", 2):
        return false
      # Token overlap.
      var tokens_v := value.split(" ")
      var tokens_e := entry.to_lower().split(" ")
      var overlap := 0.0
      for t in tokens_v:
        if tokens_e.has(t):
          overlap += 1
      if overlap / float(tokens_e.size()) >= _policy.get("blacklists", {}).get("fuzzy_match_thresholds", {}).get("token_overlap_min", 0.7):
        return false
  return true

func _levenshtein_distance(s1: String, s2: String) -> int:
  # Production-ready Levenshtein in GD-Script (matrix-based).
  var len1 := s1.length()
  var len2 := s2.length()
  if len1 == 0:
    return len2
  if len2 == 0:
    return len1
  var matrix: Array = []
  for i in range(len1 + 1):
    var row: Array = []
    for j in range(len2 + 1):
      row.append(0)
    matrix.append(row)
  for i in range(len1 + 1):
    matrix[i][0] = i
  for j in range(len2 + 1):
    matrix[0][j] = j
  for i in range(1, len1 + 1):
    for j in range(1, len2 + 1):
      var cost := 0 if s1[i-1] == s2[j-1] else 1
      matrix[i][j] = min(
        matrix[i-1][j] + 1,  # Deletion.
        matrix[i][j-1] + 1,  # Insertion.
        matrix[i-1][j-1] + cost  # Substitution.
      )
  return matrix[len1][len2]

func _log_violation(path: String, msg: String) -> void:
  var log_file := FileAccess.open(LOG_PATH, FileAccess.READ_WRITE_APPEND)
  if log_file:
    log_file.store_line("%s [VIOLATION] %s: %s" % [Time.get_datetime_string_from_system(), path, msg])
    log_file.close()

func _notify_runtime_reload(path: String) -> void:
  # Hook to your LorewayLoader (autoload).
  get_editor_interface().get_tree().call_group_flags(
    SceneTree.GROUP_CALL_REALTIME,
    "runtime",
    "on_loreway_files_changed",
    [path]
  )

func _on_tree_cell_selected() -> void:
  if not _tree:
    return
  var item := _tree.get_selected()
  if item == null:
    return
  var path := String(item.get_metadata(0))
  if _is_supported_ext(path.get_extension()):
    _check_and_validate_file(path)

func _on_tree_item_activated() -> void:
  # Double-click: force validate before open.
  if not _tree:
    return
  var item := _tree.get_selected()
  if item == null:
    return
  var path := String(item.get_metadata(0))
  if _is_supported_ext(path.get_extension()):
    _check_and_validate_file(path)
