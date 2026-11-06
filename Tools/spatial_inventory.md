# B00 vanilla spatial inventory

Run from the content root:

```sh
python3 Tools/spatial_inventory.py > /path/outside/checkout/vanilla-spatial-inventory.json
```

The inventory includes all tracked C# sources under Content.* and records their
hashes, the content HEAD, and the actual engine HEAD. Lexical candidates cover
coordinates, queries, physics and topology. Comments and strings may match;
indirect calls may not match. Every file starts unreviewed, including files with
no candidates. This is a review queue, not proof of complete runtime coverage.

B00 review must follow call paths and classify every applicable system as
non-spatial, explicitly planar, requiring 3D queries, or requiring topology.
Each decision needs an owner and a regression test or a documented reason it
has no spatial dependency. Do not automatically mark no-match files compatible.

The initial scan of content 366c3c13cab39c3ccf22fba75eb9a3af2a6e7cb7 contained
8,149 C# files, with 6,759 candidate lines/categories across 1,495 files.
Regenerate after source changes; these counts are not acceptance targets.
