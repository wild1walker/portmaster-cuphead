-- Asked by PortMaster's patcher before the setup starts (PATCHER_QUESTIONS in Cuphead.sh); each
-- answer reaches setup/patchscript as <id>=<value> in its environment.
return {
  {
    id = "TEXTURE_QUALITY",
    question = "Texture quality? Medium is what this port is tested with. Fast makes the setup about an hour shorter, with slightly less exact textures.",
    options = {
      { "medium", "Medium (recommended)" },
      { "fast", "Fast (shorter setup)" },
    },
  },
}
