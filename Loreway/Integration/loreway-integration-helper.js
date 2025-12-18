/**
 * Loreway Integration Helper
 *
 * A single, portable module that:
 * - Adapts Loreway-style narrative outlines to different AI chat platforms
 * - Provides modular formats (JSON, YAML-like text, prompt blocks)
 * - Designs adjustable prompts for filmmakers, musicians, game devs, authors
 * - Suggests export options for procedural narratives across engines
 * - Exposes a user-friendly API surface for integrations
 *
 * This is framework-agnostic: drop into Node, browser, or any JS runtime.
 * It assumes upstream systems (like the C# NarrativeCoPilotEngine) already
 * generate semantic beats / outlines, but it can also be used standalone
 * to template prompts and payloads.
 */

const Loreway = (() => {
  // ---------------------------
  // Core type helpers
  // ---------------------------

  const NarrativeMedium = Object.freeze({
    GAME_QUESTLINE: "GameQuestline",
    OPEN_WORLD_LORE: "OpenWorldLore",
    LINEAR_FILM: "LinearFilm",
    SHORT_FILM: "ShortFilm",
    NOVEL: "Novel",
    SHORT_STORY: "ShortStory",
    CONCEPT_ALBUM: "ConceptAlbum",
    VISUAL_ALBUM: "VisualAlbum",
    PODCAST: "PodcastEpisode"
  });

  const EmotionalAxis = Object.freeze({
    HOPE: "Hope",
    DREAD: "Dread",
    GRIEF: "Grief",
    RAGE: "Rage",
    WONDER: "Wonder",
    TENDERNESS: "Tenderness",
    NUMBNESS: "Numbness"
  });

  const NarrativeIntent = Object.freeze({
    COMFORT: "Comfort",
    CATHARSIS: "Catharsis",
    SHOCK: "Shock",
    TENSION: "Tension",
    EMPOWERMENT: "Empowerment",
    REFLECTION: "Reflection"
  });

  const defaultEmotionVector = () => ({
    [EmotionalAxis.HOPE]: 0.2,
    [EmotionalAxis.DREAD]: 0.6,
    [EmotionalAxis.GRIEF]: 0.4,
    [EmotionalAxis.RAGE]: 0.1,
    [EmotionalAxis.WONDER]: 0.3,
    [EmotionalAxis.TENDERNESS]: 0.2,
    [EmotionalAxis.NUMBNESS]: 0.1
  });

  const dominantEmotion = (vector) => {
    return Object.entries(vector || defaultEmotionVector())
      .sort((a, b) => b[1] - a[1])[0][0];
  };

  // ---------------------------
  // 1. Platform adapters
  // ---------------------------

  /**
   * Adapt a generic Loreway "beat" or outline element into a payload
   * that works cleanly with different AI chat platforms.
   *
   * @param {"openai"|"anthropic"|"perplexity"|"generic"} platform
   * @param {Object} beat - { type, humanSummary, llmPromptSeed, tags, intensity, emotion }
   * @param {Object} options - { systemRole, maxTokens, temperature, userNote }
   */
  function adaptForChatPlatform(platform, beat, options = {}) {
    const {
      systemRole = "You are a professional narrative designer assisting a creator.",
      maxTokens = 700,
      temperature = 0.8,
      userNote = ""
    } = options;

    const content = [
      `CONTEXT: ${userNote || "Expand this narrative beat into a vivid scene or quest step."}`,
      `GUIDANCE: ${beat.humanSummary}`,
      `SEED_PROMPT: ${beat.llmPromptSeed || ""}`,
      `TAGS: ${(beat.tags || []).join(", ")}`,
      `TARGET_INTENSITY: ${beat.targetIntensity ?? beat.intensity ?? 0.6}`,
      `DOMINANT_EMOTION: ${beat.dominantEmotion || beat.emotion || dominantEmotion()}`
    ].join("\n");

    switch (platform) {
      case "openai":
        return {
          model: "gpt-4.1-mini",
          max_tokens: maxTokens,
          temperature,
          messages: [
            { role: "system", content: systemRole },
            { role: "user", content }
          ]
        };

      case "anthropic":
        return {
          model: "claude-3-5-sonnet-20241022",
          max_tokens: maxTokens,
          temperature,
          system: systemRole,
          messages: [
            { role: "user", content }
          ]
        };

      case "perplexity":
        return {
          model: "sonar-reasoning",
          max_tokens: maxTokens,
          temperature,
          messages: [
            { role: "system", content: systemRole },
            { role: "user", content }
          ]
        };

      default: // "generic"
        return {
          system: systemRole,
          input: content,
          generation: { maxTokens, temperature }
        };
    }
  }

  // ---------------------------
  // 2. Modular formats for reuse
  // ---------------------------

  /**
   * Serialize a Loreway outline or beat list in multiple reusable formats:
   * - "json": strict JSON, ideal for engines & REST APIs
   * - "yaml": YAML-like text optimized for LLM & human editing
   * - "prompt-block": inline prompt with metadata headers
   *
   * @param {"json"|"yaml"|"prompt-block"} format
   * @param {Object} outline - { logline, medium, intent, emotionVector, beats: [...] }
   */
  function exportNarrative(format, outline) {
    if (format === "json") {
      return JSON.stringify(outline, null, 2);
    }

    if (format === "yaml") {
      // Lightweight YAML-like serializer focused on readability for prompts.[web:17][web:20]
      const lines = [];
      lines.push(`logline: "${outline.logline || ""}"`);
      lines.push(`medium: ${outline.medium || NarrativeMedium.SHORT_STORY}`);
      lines.push(`intent: ${outline.intent || NarrativeIntent.REFLECTION}`);
      lines.push("emotionVector:");
      const ev = outline.emotionVector || defaultEmotionVector();
      Object.entries(ev).forEach(([axis, value]) => {
        lines.push(`  ${axis}: ${Number(value).toFixed(2)}`);
      });
      lines.push("beats:");
      (outline.beats || []).forEach((b) => {
        lines.push(`  - id: "${b.id || b.beatId || ""}"`);
        lines.push(`    type: ${b.type || b.beatType || "Unknown"}`);
        lines.push(`    dominantEmotion: ${b.dominantEmotion || b.emotion || dominantEmotion(ev)}`);
        lines.push(`    targetIntensity: ${Number(b.targetIntensity ?? b.intensity ?? 0.6).toFixed(2)}`);
        lines.push(`    tags: [${(b.tags || []).join(", ")}]`);
        lines.push(`    humanSummary: |`);
        (b.humanSummary || "").split("\n").forEach(line => {
          lines.push(`      ${line}`);
        });
        if (b.llmPromptSeed) {
          lines.push(`    promptSeed: |`);
          b.llmPromptSeed.split("\n").forEach(line => {
            lines.push(`      ${line}`);
          });
        }
      });
      return lines.join("\n");
    }

    // "prompt-block" – for quick copy-paste into any chat box
    const header = [
      "### LOREWAY NARRATIVE OUTLINE",
      `Medium: ${outline.medium || "Unspecified"}`,
      `Intent: ${outline.intent || "Unspecified"}`,
      `Dominant emotion: ${dominantEmotion(outline.emotionVector || defaultEmotionVector())}`,
      "",
      `Logline: ${outline.logline || ""}`,
      "",
      "---",
      ""
    ].join("\n");

    const body = (outline.beats || []).map((b, i) => {
      return [
        `Beat ${i + 1} – ${b.type || b.beatType || "Unknown"}`,
        `Dominant emotion: ${b.dominantEmotion || b.emotion || "Mixed"}`,
        `Target intensity (0–1): ${Number(b.targetIntensity ?? b.intensity ?? 0.6).toFixed(2)}`,
        `Tags: ${(b.tags || []).join(", ")}`,
        `Summary: ${b.humanSummary || ""}`,
        b.llmPromptSeed ? `Prompt hint: ${b.llmPromptSeed}` : "",
        ""
      ].join("\n");
    }).join("\n");

    return header + body;
  }

  // ---------------------------
  // 3. Adjustable prompt builders
  //    for filmmakers & musicians
  // ---------------------------

  /**
   * Build a high-level adjustable prompt template for filmmakers.
   * Caller can pass this directly to an AI chat as a one-shot query.
   */
  function buildFilmmakerPrompt(outline, options = {}) {
    const {
      targetLength = "short scene",
      visualFocus = "lighting, blocking, and sound design",
      rating = "R",
      styleRefs = "slow-burn psychological cinema"
    } = options;

    const base = exportNarrative("prompt-block", {
      ...outline,
      medium: outline.medium || NarrativeMedium.LINEAR_FILM
    });

    return [
      "You are a professional screenwriter and visual storyteller.",
      `Write a ${targetLength} for a ${rating}-rated film in the style of ${styleRefs}.`,
      `Focus on ${visualFocus}. Avoid generic dialogue; prefer specific, grounded moments.`,
      "",
      "Use this Loreway outline as structural guidance. Do not copy text; interpret it:",
      "",
      base
    ].join("\n");
  }

  /**
   * Build a high-level adjustable prompt template for musicians.
   * Works for concept albums, singles, or visual albums.
   */
  function buildMusicianPrompt(outline, options = {}) {
    const {
      format = "concept album tracklist",
      musicalStyle = "dark electronic with subtle folk motifs",
      outputType = "track titles with 1–2 line narrative descriptions",
      includeLyricsGuide = true
    } = options;

    const base = exportNarrative("prompt-block", {
      ...outline,
      medium: outline.medium || NarrativeMedium.CONCEPT_ALBUM
    });

    const lines = [
      "You are a creative director and songwriter.",
      `Turn this narrative outline into a ${format} in the style of ${musicalStyle}.`,
      `Output: ${outputType}.`,
      includeLyricsGuide
        ? "For each track, optionally suggest 1–2 vivid lyric images or phrases (no full lyrics, just sparks)."
        : "Do not write lyrics, just conceptual guidance.",
      "",
      "Use this Loreway outline as thematic and emotional scaffolding:",
      "",
      base
    ];

    return lines.join("\n");
  }

  // ---------------------------
  // 4. Export options for engines
  // ---------------------------

  /**
   * Suggest export payloads for common game/engine ecosystems.
   * This does not lock you in; it gives ready-to-use shapes.
   *
   * @param {"unity"|"godot"|"unreal"|"generic"} engine
   * @param {Object} outline
   */
  function buildEngineExport(engine, outline) {
    switch (engine) {
      case "unity":
        return {
          assetType: "ScriptableObject-Ready",
          description: "Map this JSON into a C# ScriptableObject for quests/scenes.",
          data: {
            id: outline.outlineId || outline.id || null,
            logline: outline.logline,
            beats: (outline.beats || []).map((b) => ({
              id: b.id || b.beatId,
              type: b.type || b.beatType,
              summary: b.humanSummary,
              tags: b.tags || [],
              intensity: b.targetIntensity ?? b.intensity ?? 0.6,
              dominantEmotion: b.dominantEmotion || b.emotion
            }))
          }
        };

      case "godot":
        return {
          resourceType: "ConfigFile",
          description: "Serialize this to .cfg or .tres for Godot; each beat is a section.",
          data: {
            outline: {
              id: outline.outlineId || outline.id || null,
              logline: outline.logline
            },
            beats: (outline.beats || []).map((b, idx) => ({
              section: `beat_${idx + 1}`,
              type: b.type || b.beatType,
              summary: b.humanSummary,
              tags: (b.tags || []).join(","),
              intensity: b.targetIntensity ?? b.intensity ?? 0.6,
              dominantEmotion: b.dominantEmotion || b.emotion
            }))
          }
        };

      case "unreal":
        return {
          assetType: "DataTable-Row",
          description: "Use as rows in a UE DataTable (e.g., FQuestBeatRow).",
          columns: ["Id", "Type", "Summary", "TagsCsv", "Intensity", "DominantEmotion"],
          rows: (outline.beats || []).map((b) => ({
            Id: b.id || b.beatId,
            Type: b.type || b.beatType,
            Summary: b.humanSummary,
            TagsCsv: (b.tags || []).join(","),
            Intensity: b.targetIntensity ?? b.intensity ?? 0.6,
            DominantEmotion: b.dominantEmotion || b.emotion
          }))
        };

      default: // "generic"
        return {
          description: "Generic engine export; adapt fields as needed.",
          beats: (outline.beats || []).map((b) => ({
            id: b.id || b.beatId,
            type: b.type || b.beatType,
            summary: b.humanSummary,
            metadata: {
              tags: b.tags || [],
              intensity: b.targetIntensity ?? b.intensity ?? 0.6,
              emotion: b.dominantEmotion || b.emotion
            }
          }))
        };
    }
  }

  // ---------------------------
  // 5. User-friendly API surface
  // ---------------------------

  /**
   * High-level convenience wrapper: given a minimal description
   * of what the user wants, returns:
   * - outlineAdapter: functions to get JSON/YAML/prompt-block
   * - platformAdapter: function to adapt a specific beat to a chat payload
   * - domainPrompts: helpers for filmmakers/musicians
   * - engineExport: normalized payload for a target engine
   */
  function createIntegrationSession(config = {}) {
    const {
      outline = {
        outlineId: null,
        logline: config.logline || "A small story about someone trying anyway.",
        medium: config.medium || NarrativeMedium.SHORT_STORY,
        intent: config.intent || NarrativeIntent.REFLECTION,
        emotionVector: config.emotionVector || defaultEmotionVector(),
        beats: config.beats || []
      }
    } = config;

    return {
      outline,
      outlineAdapter: {
        toJSON: () => exportNarrative("json", outline),
        toYAML: () => exportNarrative("yaml", outline),
        toPromptBlock: () => exportNarrative("prompt-block", outline)
      },
      platformAdapter: {
        /**
         * Adapt the Nth beat (or provided beat) for a given chat platform.
         */
        adaptBeat(platform, index = 0, options = {}) {
          const beat = outline.beats?.[index];
          if (!beat) throw new Error("No beat at that index.");
          return adaptForChatPlatform(platform, beat, options);
        }
      },
      domainPrompts: {
        forFilmmaker: (options = {}) => buildFilmmakerPrompt(outline, options),
        forMusician: (options = {}) => buildMusicianPrompt(outline, options)
      },
      engineExport: {
        forUnity: () => buildEngineExport("unity", outline),
        forGodot: () => buildEngineExport("godot", outline),
        forUnreal: () => buildEngineExport("unreal", outline),
        generic: () => buildEngineExport("generic", outline)
      }
    };
  }

  // Public API
  return {
    NarrativeMedium,
    EmotionalAxis,
    NarrativeIntent,
    createIntegrationSession,
    exportNarrative,
    adaptForChatPlatform,
    buildFilmmakerPrompt,
    buildMusicianPrompt,
    buildEngineExport
  };
})();

// Example (commented):
// const session = Loreway.createIntegrationSession({ logline: "A wanderer returns to a frozen hometown.", beats: [...] });
// console.log(session.outlineAdapter.toYAML());
// const perplexityPayload = session.platformAdapter.adaptBeat("perplexity", 0, { userNote: "Focus on quiet dread, not jump scares." });
// console.log(JSON.stringify(perplexityPayload, null, 2));
