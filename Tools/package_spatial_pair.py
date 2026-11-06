#!/usr/bin/env python3
"""Build a fixed content/engine pair after tests have finished.

The standard content packager deletes bin. Do not run this concurrently with
tests or a game from this checkout. The standalone client retains the sandbox.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile


ROOT = Path(__file__).resolve().parents[1]
ENGINE = ROOT / "RobustToolbox"


def git(root, *args):
    return subprocess.check_output(["git", "-C", str(root), *args], text=True).strip()


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def run(*args):
    subprocess.run(args, cwd=ROOT, check=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", required=True, choices=("linux-x64", "win-x64"))
    parser.add_argument("--output", required=True, type=Path, help="New artifact directory")
    parser.add_argument("--engine-client", type=Path, help="Previously built matching standalone engine ZIP")
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists():
        parser.error("Output directory already exists; choose a new path to preserve previous evidence")
    for root in (ROOT, ENGINE):
        if git(root, "status", "--porcelain", "--untracked-files=normal"):
            parser.error(f"Commit or isolate changes before packaging: {root}")
    content_sha = git(ROOT, "rev-parse", "HEAD")
    engine_sha = git(ENGINE, "rev-parse", "HEAD")
    if engine_sha != git(ROOT, "rev-parse", "HEAD:RobustToolbox"):
        parser.error("Engine checkout does not match the committed content gitlink")
    output.mkdir(parents=True)

    engine_archive = args.engine_client.resolve() if args.engine_client else output / "engine-client.zip"
    if not args.engine_client:
        run(sys.executable, str(ENGINE / "Tools/package_standalone_client.py"),
            "--runtime", args.runtime, "--output", str(engine_archive))
    with zipfile.ZipFile(engine_archive) as archive:
        engine_manifest = json.loads(archive.read("engine-manifest.json"))
        if (engine_manifest["kind"] != "standalone-engine-client"
                or engine_manifest["engine_commit"] != engine_sha
                or engine_manifest["runtime"] != args.runtime
                or engine_manifest["source_dirty"]):
            raise RuntimeError("Engine artifact does not describe this clean, fixed pair")
        names = archive.namelist()
        expected = set(engine_manifest["files"]) | {"engine-manifest.json"}
        if len(names) != len(set(names)) or set(names) != expected:
            raise RuntimeError("Engine archive file set differs from its manifest")
        for name, digest in engine_manifest["files"].items():
            if hashlib.sha256(archive.read(name)).hexdigest() != digest:
                raise RuntimeError(f"Engine payload hash mismatch: {name}")

    # Content.Server references Content.Packaging. Its recursive Rebuild must not
    # overwrite DLLs of the running packager, which are locked on Windows.
    # Execute a published copy outside those build outputs instead.
    with tempfile.TemporaryDirectory(prefix="ss14-packaging-") as temporary:
        run("dotnet", "publish", "Content.Packaging/Content.Packaging.csproj",
            "-c", "Release", "--no-self-contained", "-m:2", "--output", temporary)
        packager = str(Path(temporary) / "Content.Packaging.dll")
        # Each stage completes before the next one can touch its outputs.
        run("dotnet", packager, "server", "--platform", args.runtime,
            "--configuration", "Release", "--no-wipe-release")
        server = output / f"SS14.Server_{args.runtime}.zip"
        shutil.copyfile(ROOT / "release" / server.name, server)
        run("dotnet", packager, "client", "--configuration", "Release", "--no-wipe-release")
    content_archive = ROOT / "release/SS14.Client.zip"
    content_digest = sha256(content_archive)

    pair = {
        "manifest_version": 1,
        "purpose": "G0 qualification pair; not spatial feature acceptance",
        "content_commit": content_sha,
        "engine_commit": engine_sha,
        "engine_upstream_commit": "4058566ed460e156d77d32b1810fd97ba5a57ff9",
        "runtime": args.runtime,
        "sdk": engine_manifest["sdk"],
        "physics_backend": engine_manifest["physics_backend"],
        "physics_version": engine_manifest["physics_version"],
        "spatial_schema": None,
        "spatial_protocol": None,
        "content_archive_sha256": content_digest,
        "engine_archive_sha256": sha256(engine_archive),
        "server_archive_sha256": sha256(server),
        "server_prerequisite": ".NET 10 runtime",
        "client_runtime_bundled": True,
        "target_ram_gb": 8
    }
    client = output / f"SS14.Client.Standalone_{args.runtime}.zip"
    with zipfile.ZipFile(engine_archive) as source, zipfile.ZipFile(client, "x", zipfile.ZIP_DEFLATED) as target:
        for info in source.infolist():
            target.writestr(info, source.read(info))
        target.write(content_archive, "Content.Client.zip")
        target.writestr("pair-manifest.json", json.dumps(pair, indent=2) + "\n")
        if args.runtime.startswith("win-"):
            target.writestr("start.cmd", '@echo off\r\ncd /d "%~dp0"\r\nRobust.Client.exe --self-contained --mount-zip Content.Client.zip %*\r\n')
        else:
            info = zipfile.ZipInfo("start.sh")
            info.create_system = 3
            info.external_attr = 0o100755 << 16
            target.writestr(info, '#!/bin/sh\nset -eu\ncd -- "$(dirname -- "$0")"\nexec ./Robust.Client --self-contained --mount-zip Content.Client.zip "$@"\n')
    # Check again so concurrent source edits cannot produce a clean-looking manifest.
    for root, commit in ((ROOT, content_sha), (ENGINE, engine_sha)):
        if git(root, "rev-parse", "HEAD") != commit or git(root, "status", "--porcelain", "--untracked-files=normal"):
            raise RuntimeError(f"Source changed during packaging: {root}; discard these artifacts")
    pair["client_archive_sha256"] = sha256(client)
    (output / "pair-manifest.json").write_text(json.dumps(pair, indent=2) + "\n", encoding="utf-8")
    print(f"Created fixed pair in {output}")


if __name__ == "__main__":
    main()
