import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("catalogue", ROOT / "tools/compatibility-catalogue.py")
catalogue = importlib.util.module_from_spec(spec)
spec.loader.exec_module(catalogue)


class CatalogueTests(unittest.TestCase):
    def setUp(self):
        self.data = json.loads(catalogue.CATALOGUE.read_text())

    def test_current_evidence_valid(self):
        catalogue.validate(self.data)

    def test_upstream_report_cannot_be_promoted_to_local_support(self):
        device = next(d for d in self.data["devices"] if d["evidence"] == "upstream_author")
        device["tizenhalo"] = "validated"
        device["firmware"] = "example-firmware"
        with self.assertRaisesRegex(ValueError, "requires local working evidence"):
            catalogue.validate(self.data)

    def test_moving_branch_is_not_a_source_pin(self):
        self.data["sources"][0]["commit"] = "main"
        with self.assertRaisesRegex(ValueError, "full commit"):
            catalogue.validate(self.data)

    def test_duplicate_devices_and_broken_references_rejected(self):
        self.data["devices"].append(copy.deepcopy(self.data["devices"][0]))
        with self.assertRaisesRegex(ValueError, "Duplicate device"):
            catalogue.validate(self.data)
        self.data["devices"].pop()
        self.data["devices"][0]["source_ids"] = ["missing"]
        with self.assertRaisesRegex(ValueError, "Unknown source reference"):
            catalogue.validate(self.data)


if __name__ == "__main__":
    unittest.main()
