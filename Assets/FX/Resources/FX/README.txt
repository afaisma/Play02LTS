Resources/FX/ — editor-authored FX assets (optional).

Drop an FXLibrary.asset here (Create ▸ FX ▸ Library) to override the code-built
default catalog, plus any FXEffect.asset (Create ▸ FX ▸ Effect), ParticleImage
prefabs, sprites, or AudioClips referenced by their Resources paths.

If this folder has no FXLibrary.asset, FX runs on FXLibrary.CreateDefault():
the built-in "stars", "book_done" (+ synthesized chime), and "lab" effects.
Nothing here is required for the system to work — it is the authoring surface.
