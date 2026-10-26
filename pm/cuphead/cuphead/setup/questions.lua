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
  {
    id = "ASPECT",
    question = "Picture shape? Fill fits the game to the screen (on 3:2 and 16:10 screens the sides are cropped a little; 4:3 screens keep the bars). 16:9 shows the whole picture with bars above and below. (Change it later in ports/cuphead/aspect.txt.)",
    options = {
      { "fill", "Fill the screen (recommended)" },
      { "16:9", "16:9 with bars" },
    },
  },
  {
    id = "RESOLUTION",
    question = "Resolution the game renders at? The screen shows it full size either way; lower is softer but faster. (Change it later in ports/cuphead/resolution.txt.)",
    options = {
      { "device", "Device resolution (recommended)" },
      { "0.833", "5/6 of the device's" },
      { "0.75", "3/4 of the device's" },
      { "0.5", "1/2 of the device's" },
    },
  },
}
