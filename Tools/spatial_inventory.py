#!/usr/bin/env python3
"""Collect B00 review candidates; lexical matches are NOT a compatibility verdict.

Writes JSON to stdout. Includes every tracked Content C# source file, including
files with no matches, so the review queue cannot silently omit those systems.
Comments and strings may match; semantic classification remains a review task.
"""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[1]
PATTERNS = {
    "coordinates": r"\b(?:Vector2|MapCoordinates|EntityCoordinates|TransformComponent|SharedTransformSystem|WorldPosition|LocalPosition|GridUid|MapId)\b",
    "queries": r"\b(?:RayCast\w*|Raycast\w*|ShapeCast\w*|GetEntitiesInRange|GetEntitiesIntersecting|InRangeUnobstructed|GetDistance|DistanceSquared|TryDistance)\b",
    "physics": r"\b(?:PhysicsComponent|SharedPhysicsSystem|Fixture\w*|Collision\w*|LinearVelocity|AngularVelocity)\b",
    "topology": r"\b(?:TileRef|TileIndices|Tile|GetTileRef|GetTile|TryGetTileRef|MapGridComponent|Atmosphere\w*|NavMesh\w*)\b",
}


def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args])


def main():
    patterns = {name: re.compile(pattern) for name, pattern in PATTERNS.items()}
    rows = []
    for raw in sorted(git("ls-files", "-z", "--", "Content.*/*.cs").split(b"\0")):
        if not raw:
            continue
        path = raw.decode("utf-8")
        source = (ROOT / path).read_bytes()
        findings = []
        for number, line in enumerate(source.decode("utf-8-sig").splitlines(), 1):
            for category, pattern in patterns.items():
                tokens = sorted(set(pattern.findall(line)))
                if tokens:
                    findings.append({"line": number, "kind": category, "tokens": tokens})
        rows.append({"path": path, "sha256": hashlib.sha256(source).hexdigest(),
                     "review_status": "unreviewed", "classification": None,
                     "owner": None, "tests": [], "candidates": findings})
    json.dump({"schema_version": 1,
               "content_commit": git("rev-parse", "HEAD").decode().strip(),
               "engine_commit": subprocess.check_output(
                   ["git", "-C", str(ROOT / "RobustToolbox"), "rev-parse", "HEAD"]
               ).decode().strip(),
               "scope": "tracked Content.* C# files; lexical review queue, not completed B00 matrix",
               "allowed_classifications": ["non_spatial", "explicit_local_plane", "requires_3d_query", "requires_topology"],
               "files": rows}, sys.stdout, ensure_ascii=False, indent=2)
    print()


if __name__ == "__main__":
    main()
