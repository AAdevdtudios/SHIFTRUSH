SHIFT//RUSH game sounds
=======================
A place for one-shot game sounds (impacts, whooshes, stingers). Anything here is imported as a short,
decompressed-on-load clip so it plays instantly. Game code can load one by file name:
    Resources.Load<AudioClip>("Audio/SFX/<file name without extension>")
and should play it through the "SFX" mixer group so the Car & Effects volume slider controls it.
Asset Store sounds must not go in the public repo: audio files in this folder are git-ignored.
