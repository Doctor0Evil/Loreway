# Path: res://Cell/Loreway/Config/LorewayPersonality.gd
# Purpose: Personality-vector + brutality automation for Loreway in Godot.
# Any Storyteller, AI chat, or editor tool uses this to align content with Cell.

extends Node
class_name LorewayPersonality

# -------------------------------------------------------------------
# Core data containers
# -------------------------------------------------------------------

class BrutalityProfile:
	var global: float = 0.85
	var physical: float = 0.80
	var psychological: float = 0.95
	var social: float = 0.90

class HorrorVector:
	var dread: float = 0.95
	var shock: float = 0.80
	var disgust: float = 0.75
	var uncanny: float = 0.85
	var moral_anxiety: float = 1.00

class SlavicToneVector:
	var rural_decay: float = 0.95
	var bureaucratic_horror: float = 0.90
	var domestic_haunting: float = 0.85
	var cosmic_rot: float = 0.70

class PersonalityProfile:
	var id: String = "CELL_CORE_DEFAULT"
	var label: String = "Cell Core – Wet Forest Bureaucratic Rot"

	var brutality: BrutalityProfile = BrutalityProfile.new()
	var horror: HorrorVector = HorrorVector.new()
	var slavic: SlavicToneVector = SlavicToneVector.new()

	var surreal_temperature: float = 0.55
	var narrative_temperature: float = 0.70
	var horror_temperature: float = 0.80

# -------------------------------------------------------------------
# Utility
# -------------------------------------------------------------------

static func _clamp01(x: float) -> float:
	return clamp(x, 0.0, 1.0)

static func _lerp(a: float, b: float, t: float) -> float:
	return a + (b - a) * t

static func default_profile() -> PersonalityProfile:
	var p := PersonalityProfile.new()
	return p

static func new_profile(id: String, label: String, overrides: Dictionary = {}) -> PersonalityProfile:
	var p := default_profile()
	if id != "":
		p.id = id
	if label != "":
		p.label = label

	if overrides.has("brutality"):
		var b := overrides["brutality"]
		if b.has("global"):
			p.brutality.global = _clamp01(b["global"])
		if b.has("physical"):
			p.brutality.physical = _clamp01(b["physical"])
		if b.has("psychological"):
			p.brutality.psychological = _clamp01(b["psychological"])
		if b.has("social"):
			p.brutality.social = _clamp01(b["social"])

	if overrides.has("horror"):
		var h := overrides["horror"]
		if h.has("dread"):
			p.horror.dread = _clamp01(h["dread"])
		if h.has("shock"):
			p.horror.shock = _clamp01(h["shock"])
		if h.has("disgust"):
			p.horror.disgust = _clamp01(h["disgust"])
		if h.has("uncanny"):
			p.horror.uncanny = _clamp01(h["uncanny"])
		if h.has("moral_anxiety"):
			p.horror.moral_anxiety = _clamp01(h["moral_anxiety"])

	if overrides.has("slavic"):
		var s := overrides["slavic"]
		if s.has("rural_decay"):
			p.slavic.rural_decay = _clamp01(s["rural_decay"])
		if s.has("bureaucratic_horror"):
			p.slavic.bureaucratic_horror = _clamp01(s["bureaucratic_horror"])
		if s.has("domestic_haunting"):
			p.slavic.domestic_haunting = _clamp01(s["domestic_haunting"])
		if s.has("cosmic_rot"):
			p.slavic.cosmic_rot = _clamp01(s["cosmic_rot"])

	if overrides.has("surreal_temperature"):
		p.surreal_temperature = _clamp01(overrides["surreal_temperature"])
	if overrides.has("narrative_temperature"):
		p.narrative_temperature = _clamp01(overrides["narrative_temperature"])
	if overrides.has("horror_temperature"):
		p.horror_temperature = _clamp01(overrides["horror_temperature"])

	return p

static func blend_profiles(a: PersonalityProfile, b: PersonalityProfile, t: float) -> PersonalityProfile:
	t = _clamp01(t)
	var p := PersonalityProfile.new()
	p.id = a.id + "_BLEND_" + b.id
	p.label = "Blend(" + a.label + ", " + b.label + ")"

	# Brutality
	p.brutality.global        = _clamp01(_lerp(a.brutality.global,        b.brutality.global,        t))
	p.brutality.physical      = _clamp01(_lerp(a.brutality.physical,      b.brutality.physical,      t))
	p.brutality.psychological = _clamp01(_lerp(a.brutality.psychological, b.brutality.psychological, t))
	p.brutality.social        = _clamp01(_lerp(a.brutality.social,        b.brutality.social,        t))

	# Horror
	p.horror.dread         = _clamp01(_lerp(a.horror.dread,         b.horror.dread,         t))
	p.horror.shock         = _clamp01(_lerp(a.horror.shock,         b.horror.shock,         t))
	p.horror.disgust       = _clamp01(_lerp(a.horror.disgust,       b.horror.disgust,       t))
	p.horror.uncanny       = _clamp01(_lerp(a.horror.uncanny,       b.horror.uncanny,       t))
	p.horror.moral_anxiety = _clamp01(_lerp(a.horror.moral_anxiety, b.horror.moral_anxiety, t))

	# Slavic tone
	p.slavic.rural_decay         = _clamp01(_lerp(a.slavic.rural_decay,         b.slavic.rural_decay,         t))
	p.slavic.bureaucratic_horror = _clamp01(_lerp(a.slavic.bureaucratic_horror, b.slavic.bureaucratic_horror, t))
	p.slavic.domestic_haunting   = _clamp01(_lerp(a.slavic.domestic_haunting,   b.slavic.domestic_haunting,   t))
	p.slavic.cosmic_rot          = _clamp01(_lerp(a.slavic.cosmic_rot,          b.slavic.cosmic_rot,          t))

	# Temperatures
	p.surreal_temperature   = _clamp01(_lerp(a.surreal_temperature,   b.surreal_temperature,   t))
	p.narrative_temperature = _clamp01(_lerp(a.narrative_temperature, b.narrative_temperature, t))
	p.horror_temperature    = _clamp01(_lerp(a.horror_temperature,    b.horror_temperature,    t))

	return p

# -------------------------------------------------------------------
# KG-aware scoring helpers
# Expect nodes shaped like Loreway EventNode, RumorNode, etc.
# -------------------------------------------------------------------

static func score_event(event_node: Dictionary, personality: PersonalityProfile) -> float:
	if event_node.get("external_reference_allowed", false):
		return -INF

	var score := 0.0
	var b = personality.brutality
	var h = personality.horror

	var t: String = str(event_node.get("type", ""))
	if t == "localcatastrophe":
		score += 2.0 * personality.horror_temperature
		score += 1.5 * b.global
	elif t == "ritualfailure":
		score += 1.5 * personality.narrative_temperature
		score += 1.0 * b.physical
	elif t == "disappearance":
		score += 1.2 * h.dread + 1.2 * b.psychological
	else:
		score += 0.5

	var cons := event_node.get("consequences", [])
	var harm_count := cons.size()
	score += float(harm_count) * 0.3 * b.global

	var tags := event_node.get("narrativetags", [])
	for tag in tags:
		match tag:
			"originlegend":
				score += 0.5 * h.uncanny
			"plague", "disease":
				score += 0.7 * b.physical
			"betrayal":
				score += 0.8 * b.social + 0.5 * b.psychological
			"justifiestabu":
				score += 0.5 * h.moral_anxiety
			_:
				pass

	if b.global > 0.8 and personality.horror_temperature > 0.7:
		if t == "localcatastrophe" or t == "ritualfailure":
			score += 1.5

	return score

static func score_rumor(rumor_node: Dictionary, personality: PersonalityProfile) -> float:
	if rumor_node.get("external_reference_allowed", false):
		return -INF

	var score := 0.0
	var b = personality.brutality
	var h = personality.horror

	score += 1.0 * b.psychological + 0.8 * b.social

	var truth := str(rumor_node.get("truthstatus", "unknown"))
	if truth == "partial" or truth == "unknown":
		score += 0.8 * h.moral_anxiety
	elif truth == "false":
		score += 0.3 * h.uncanny

	if str(rumor_node.get("topic", "")) == "disappearance":
		score += 0.6 * h.dread

	return score

static func score_dialogue(dialogue_unit: Dictionary, personality: PersonalityProfile) -> float:
	if dialogue_unit.get("external_reference_allowed", false):
		return -INF

	var score := 0.0
	var b = personality.brutality
	var h = personality.horror

	var hf: String = str(dialogue_unit.get("horrorfunction", ""))
	match hf:
		"dread":
			score += 1.0 * h.dread
		"shock":
			score += 1.0 * h.shock + 0.5 * b.physical
		"disgust":
			score += 1.0 * h.disgust + 0.5 * b.physical
		"uncanny":
			score += 1.0 * h.uncanny
		"moralanxiety":
			score += 1.2 * h.moral_anxiety + 0.6 * b.psychological
		_:
			score += 0.2

	var tags := dialogue_unit.get("narrativetags", [])
	for tag in tags:
		match tag:
			"family_conflict", "betrayal":
				score += 0.8 * b.social
			"bureaucratic_cruelty":
				score += 0.9 * personality.slavic.bureaucratic_horror
			_:
				pass

	return score

static func score_scene(scene: Dictionary, personality: PersonalityProfile) -> float:
	if scene.get("external_reference_allowed", false):
		return -INF

	var score := 0.0
	var hf: String = str(scene.get("horrorfunction", ""))
	score += _score_horror_function(hf, personality)

	var scope: String = str(scene.get("scope", "local"))
	if scope == "local":
		score += 0.5
	elif scope == "area":
		score += 0.3

	var tags := scene.get("environmenttags", [])
	for tag in tags:
		match tag:
			"wetforestedge", "postsovietdecay":
				score += 0.6 * personality.slavic.rural_decay
			"apartmentblock", "domestic":
				score += 0.6 * personality.slavic.domestic_haunting
			"bureaucratic":
				score += 0.6 * personality.slavic.bureaucratic_horror
			_:
				pass

	return score

static func _score_horror_function(hf: String, personality: PersonalityProfile) -> float:
	var h = personality.horror
	match hf:
		"dread":
			return 1.0 * h.dread
		"shock":
			return 1.0 * h.shock
		"disgust":
			return 1.0 * h.disgust
		"uncanny":
			return 1.0 * h.uncanny
		"moralanxiety":
			return 1.2 * h.moral_anxiety
		_:
			return 0.2

# -------------------------------------------------------------------
# Chat / IDE integration
# -------------------------------------------------------------------

static func personality_from_task(task: Dictionary) -> PersonalityProfile:
	var overrides := {}
	if task.has("BrutalityProfile"):
		var bp := task["BrutalityProfile"]
		overrides["brutality"] = {
			"global": bp.get("Global", 0.85),
			"physical": bp.get("PhysicalBrutality", 0.80),
			"psychological": bp.get("PsychologicalBrutality", 0.95),
			"social": bp.get("SocialBrutality", 0.90),
		}
	return new_profile(
		str(task.get("ID", "LOREWAY-TASK")),
		str(task.get("Intent", "Loreway Task")),
		overrides
	)

static func task_from_user_prompt(prompt: String) -> Dictionary:
	var lower := prompt.to_lower()
	var profile := {
		"Global": 0.85,
		"PhysicalBrutality": 0.80,
		"PsychologicalBrutality": 0.95,
		"SocialBrutality": 0.90,
	}

	if lower.find("as brutal as possible") != -1 or lower.find("maximum") != -1:
		profile["Global"] = 0.98
		profile["PhysicalBrutality"] = 0.95
		profile["PsychologicalBrutality"] = 1.00
		profile["SocialBrutality"] = 0.95
	elif lower.find("psychological") != -1:
		profile["PsychologicalBrutality"] = 1.0
		profile["PhysicalBrutality"] = 0.6
		profile["SocialBrutality"] = 0.8
	elif lower.find("social") != -1 or lower.find("village turning") != -1:
		profile["SocialBrutality"] = 1.0
		profile["PsychologicalBrutality"] = 0.9
		profile["PhysicalBrutality"] = 0.6

	return {
		"ID": "USER_PROMPT",
		"Intent": prompt,
		"BrutalityProfile": profile,
	}
