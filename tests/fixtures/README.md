# Test fixtures

`zipline-hook-mesh.txt` is the game's own zipline hook model, as the mod writes it when the hook is drawn
(`BepInEx\config\XIII-XR-mesh-zipline.txt`). It is the game's asset, so it is not distributed here.

`HandToolsTests` checks the hook's handle detection against it when the file is present, and skips that one case otherwise.
To run it, copy your `XIII-XR-mesh-zipline.txt` here as `zipline-hook-mesh.txt`.
