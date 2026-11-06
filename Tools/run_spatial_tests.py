"""Run exhaustive, disjoint integration partitions and verify fixed-pair evidence."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
PARTITIONS = ("game-rules", "early", "late")
RULES = "Content.IntegrationTests.Tests.GameRules"
EARLY = tuple("Content.IntegrationTests.Tests." + letter for letter in "ABCDEFGHIJKL")
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def partition_for(full_name):
    if RULES in full_name:
        return "game-rules"
    return "early" if any(prefix in full_name for prefix in EARLY) else "late"


def test_filter(partition):
    if partition == "game-rules":
        return "FullyQualifiedName~" + RULES
    no_rules = "FullyQualifiedName!~" + RULES
    if partition == "early":
        return no_rules + "&(" + "|".join("FullyQualifiedName~" + p for p in EARLY) + ")"
    if partition == "late":
        return no_rules + "&" + "&".join("FullyQualifiedName!~" + p for p in EARLY)
    raise ValueError("Unknown integration partition")


def inspect_result(path, partition):
    root = ET.parse(path).getroot()
    summary = root.find("t:ResultSummary", NS)
    counters = root.find("t:ResultSummary/t:Counters", NS)
    if summary is None or counters is None or summary.get("outcome") not in ("Completed", "Passed"):
        raise ValueError("Missing or unsuccessful test summary")
    counts = {key: int(value) for key, value in counters.attrib.items()}
    if counts.get("executed", 0) <= 0 or counts.get("passed", 0) <= 0:
        raise ValueError("Partition executed no successful tests")
    if any(counts.get(key, 0) for key in ("failed", "error", "timeout", "aborted", "inconclusive",
                                        "passedButRunAborted", "notRunnable", "disconnected", "warning", "inProgress", "pending")):
        raise ValueError("Partition has failures or unfinished tests")
    results = root.findall("t:Results/t:UnitTestResult", NS)
    if len(results) != counts.get("total") or any(r.get("outcome") not in ("Passed", "NotExecuted") for r in results):
        raise ValueError("Individual results are missing or disagree with successful completion")
    definitions = root.findall("t:TestDefinitions/t:UnitTest/t:TestMethod", NS)
    if not definitions:
        raise ValueError("Partition contains no test definitions")
    for method in definitions:
        full_name = method.get("className", "") + "." + method.get("name", "")
        if partition_for(full_name) != partition:
            raise ValueError("Test ran in the wrong partition: " + full_name)
    return counts


def git(*args, engine=False):
    return subprocess.check_output(["git", "-C", str(ROOT / "RobustToolbox" if engine else ROOT), *args], text=True).strip()


def verify(directory, runtime, content, engine):
    receipts = list(directory.glob("**/partition.json"))
    seen = set()
    for path in receipts:
        data = json.loads(path.read_text(encoding="utf-8"))
        part = data.get("partition")
        if part not in PARTITIONS or part in seen:
            raise ValueError("Duplicate or unknown test partition")
        if (data.get("content") != content or data.get("engine") != engine or data.get("runtime") != runtime
                or data.get("source_dirty") is not False or data.get("filter") != test_filter(part)):
            raise ValueError("Partition does not belong to this clean fixed pair/filter/runtime")
        trx = path.parent / ("content-" + part + ".trx")
        if hashlib.sha256(trx.read_bytes()).hexdigest() != data.get("trx_sha256"):
            raise ValueError("Test result hash mismatch")
        if inspect_result(trx, part) != data.get("counters"):
            raise ValueError("Test counters disagree with receipt")
        seen.add(part)
    if seen != set(PARTITIONS):
        raise ValueError("Not all integration partitions passed")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--partition", choices=PARTITIONS)
    mode.add_argument("--verify-directory", type=Path)
    parser.add_argument("--runtime", required=True, choices=("linux-x64", "win-x64"))
    parser.add_argument("--results", type=Path, default=ROOT / "release/qualification")
    args = parser.parse_args()
    content, engine = git("rev-parse", "HEAD"), git("rev-parse", "HEAD", engine=True)
    if engine != git("rev-parse", "HEAD:RobustToolbox"):
        parser.error("Engine does not match the committed content gitlink")
    if args.verify_directory:
        verify(args.verify_directory, args.runtime, content, engine)
        print("All three integration partitions verified for the fixed pair.")
        return
    part = args.partition
    args.results.mkdir(parents=True, exist_ok=True)
    trx = args.results / ("content-" + part + ".trx")
    receipt = args.results / "partition.json"
    if trx.exists() or receipt.exists():
        parser.error("Use a new result directory; refusing stale test evidence")
    dirty_before = bool(git("status", "--porcelain") or git("status", "--porcelain", engine=True))
    subprocess.run(["dotnet", "test", "Content.IntegrationTests/Content.IntegrationTests.csproj",
                    "-c", "DebugOpt", "--no-build", "--no-restore", "--filter", test_filter(part),
                    "--logger", "trx;LogFileName=" + trx.name, "--results-directory", str(args.results.resolve()),
                    "--", "NUnit.ConsoleOut=0", "NUnit.MapWarningTo=Failed"], cwd=ROOT, check=True)
    counts = inspect_result(trx, part)
    if git("rev-parse", "HEAD") != content or git("rev-parse", "HEAD", engine=True) != engine:
        raise RuntimeError("Source revision changed during qualification")
    dirty = dirty_before or bool(git("status", "--porcelain") or git("status", "--porcelain", engine=True))
    receipt.write_text(json.dumps(dict(partition=part, filter=test_filter(part), content=content,
        engine=engine, runtime=args.runtime, source_dirty=dirty, counters=counts,
        trx_sha256=hashlib.sha256(trx.read_bytes()).hexdigest()), indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
