extends Node
class_name LorewayPersonalitySingleton

var profile: LorewayPersonality.PersonalityProfile

func _ready() -> void:
	# Default: Cell core brutal profile
	profile = LorewayPersonality.default_profile()

func set_from_task(task: Dictionary) -> void:
	profile = LorewayPersonality.personality_from_task(task)

func set_from_prompt(prompt: String) -> void:
	var task := LorewayPersonality.task_from_user_prompt(prompt)
	set_from_task(task)
