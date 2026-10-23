import hashlib,json,tempfile,unittest
from pathlib import Path
from model_assets import valid,catalog

class ModelAssetsTests(unittest.TestCase):
    def test_changed_and_truncated_weights_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/"model";path.write_bytes(b"model-a")
            entry={"size":7,"sha256":hashlib.sha256(b"model-a").hexdigest()}
            self.assertTrue(valid(path,entry,None,False))
            prior=[7,path.stat().st_mtime_ns,entry["sha256"]]
            self.assertTrue(valid(path,entry,prior,False))
            path.write_bytes(b"bad")
            self.assertFalse(valid(path,entry,prior,False))
            path.write_bytes(b"model-b")
            self.assertFalse(valid(path,entry,None,True))
    def test_catalog_has_pinned_revisions_hashes_and_no_traversal(self):
        models=catalog()
        self.assertEqual(len(models),9)
        for model in models:
            self.assertRegex(model["revision"],r"^[a-f0-9]{40}$")
            for entry in model["files"]:
                self.assertRegex(entry["sha256"],r"^[a-f0-9]{64}$")
                self.assertNotIn("..",Path(entry["path"]).parts)
                self.assertGreater(entry["size"],0)

if __name__=="__main__":unittest.main()
