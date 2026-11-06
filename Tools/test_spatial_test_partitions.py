import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from run_spatial_tests import PARTITIONS, inspect_result, partition_for, test_filter, verify


class TestPartitions(unittest.TestCase):
    def result(self, name, outcome="Completed", failed=0):
        return f'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <Results><UnitTestResult outcome="Passed"/></Results>
          <TestDefinitions><UnitTest><TestMethod className="{name}" name="Check"/></UnitTest></TestDefinitions>
          <ResultSummary outcome="{outcome}"><Counters total="1" executed="1" passed="1" failed="{failed}"/></ResultSummary>
        </TestRun>'''

    def test_complement_includes_new_and_nonstandard_namespaces(self):
        cases = {"Content.IntegrationTests.Tests.GameRules.RoundTest.Check": "game-rules",
                 "Content.IntegrationTests.Tests.GameTestTests.Test.Check": "early",
                 "Content.IntegrationTests.Tests.Atmos.Test.Check": "early",
                 "Content.IntegrationTests.Tests.SpatialLifecycleTest.Check": "late",
                 "A.New.Namespace.Test.Check": "late"}
        for name, expected in cases.items():
            self.assertEqual(partition_for(name), expected)
        self.assertNotIn("GameRules|", test_filter("early"))
        self.assertIn("FullyQualifiedName!~Content.IntegrationTests.Tests.GameRules", test_filter("late"))

    def test_aborted_failed_missing_and_wrong_partition_evidence_is_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "result.trx"
            for xml in (self.result("Content.IntegrationTests.Tests.Atmos.Test", failed=1),
                        self.result("Content.IntegrationTests.Tests.Atmos.Test", outcome="Aborted"),
                        "<TestRun/>", self.result("Content.IntegrationTests.Tests.SpatialLifecycleTest"),
                        self.result("Content.IntegrationTests.Tests.Atmos.Test").replace('<UnitTestResult outcome="Passed"/>', '')):
                path.write_text(xml)
                with self.subTest(xml=xml), self.assertRaises(ValueError):
                    inspect_result(path, "early")

    def test_release_needs_every_matching_untampered_partition(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            names = ("Content.IntegrationTests.Tests.GameRules.Test", "Content.IntegrationTests.Tests.Atmos.Test",
                     "Content.IntegrationTests.Tests.SpatialLifecycleTest")
            for part, name in zip(PARTITIONS, names):
                folder = directory / part; folder.mkdir()
                trx = folder / ("content-" + part + ".trx"); trx.write_text(self.result(name))
                data = dict(partition=part, filter=test_filter(part), content="content", engine="engine", runtime="linux-x64",
                            source_dirty=False, counters=inspect_result(trx, part),
                            trx_sha256=hashlib.sha256(trx.read_bytes()).hexdigest())
                (folder / "partition.json").write_text(json.dumps(data))
            verify(directory, "linux-x64", "content", "engine")
            with self.assertRaises(ValueError): verify(directory, "linux-x64", "different-content", "engine")
            with self.assertRaises(ValueError): verify(directory, "win-x64", "content", "engine")
            trx.write_text(self.result(names[-1], failed=1))
            with self.assertRaises(ValueError): verify(directory, "linux-x64", "content", "engine")
            (folder / "partition.json").unlink()
            with self.assertRaises(ValueError): verify(directory, "linux-x64", "content", "engine")


if __name__ == "__main__":
    unittest.main()
