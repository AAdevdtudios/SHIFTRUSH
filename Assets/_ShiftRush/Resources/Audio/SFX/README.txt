SHIFT//RUSH game sounds
=======================
The game has its own built-in sounds (made in code, see Scripts/Audio/SfxSynth.cs). To replace one, drop an
audio file here with that sound's name (any of .wav, .ogg, .mp3). No code or menu rebuild needed.

  countdown_tick   3, 2, 1 (played a step higher each count)
  countdown_go     GO
  shift_open       SHIFT ramp: time slows, the diamond appears
  shift_hit        a diamond hit (the second hit plays a fifth higher)
  shift_miss       a missed diamond
  shift_warp       warping into a SHIFT world
  shift_arrive     landing back in the race
  nitro_gain       nitro handed out after a SHIFT
  ring_pass        Ring Rush: a ring passed (climbs up the scale ring by ring)
  trap_hit         Trap Run: a trap hit
  lap              lap completed
  best_lap         new best lap
  final_lap        final lap
  finish           finish line, outside the top three
  finish_podium    finish line, top three
  respawn          car put back on the track
  overtake         you took a place

Sounds play through the "SFX" mixer group, so the Car & Effects volume slider controls them.
Asset Store sounds must not go in the public repo: audio files in this folder are git-ignored.
