import importlib.util
import pathlib
import unittest

MODULE_PATH = pathlib.Path(__file__).with_name("epic14_homologacion.py")
SPEC = importlib.util.spec_from_file_location("epic14_homologacion", MODULE_PATH)
assert SPEC and SPEC.loader
harness = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(harness)


class HomologationHarnessTests(unittest.TestCase):
    def test_redact_removes_named_and_embedded_sensitive_values(self):
        bearer_value = "dummy-bearer-value"
        taxpayer_id = "20123456786"
        value = {
            "cae": "12345678901234",
            "cuitReceptor": taxpayer_id,
            "message": f"auth={bearer_value}; receptor={taxpayer_id}",
            "operationId": "opaque-operation",
        }

        redacted = harness.redact(value, {bearer_value, taxpayer_id})

        serialized = str(redacted)
        self.assertNotIn(bearer_value, serialized)
        self.assertNotIn(taxpayer_id, serialized)
        self.assertNotIn("12345678901234", serialized)
        self.assertEqual("opaque-operation", redacted["operationId"])

    def test_parse_transport_body_accepts_sse(self):
        body = 'event: message\ndata: {"jsonrpc":"2.0","id":1,"result":{"ok":true}}\n\n'
        parsed = harness.parse_transport_body(body)
        self.assertTrue(parsed["result"]["ok"])

    def test_unwrap_tool_result_parses_json_text(self):
        envelope = {
            "result": {
                "content": [
                    {"type": "text", "text": '{"success":true,"operationId":"abc"}'}
                ]
            }
        }
        result = harness.unwrap_tool_result(envelope)
        self.assertTrue(result["success"])
        self.assertEqual("abc", result["operationId"])

    def test_collect_sensitive_values_finds_nested_taxpayer_data(self):
        values = harness.collect_sensitive_values({
            "invoice": {"cuitReceptor": 20123456786},
            "nested": [{"credentialReference": "cred-ref-test"}],
        })
        self.assertIn("20123456786", values)
        self.assertIn("cred-ref-test", values)


if __name__ == "__main__":
    unittest.main()
