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

    def test_redact_preserves_pdf_status_but_omits_blob(self):
        value = {
            "pdf": {
                "status": "Failed",
                "errorCode": "RENDERER_TIMEOUT",
                "base64": "QUJDREVGRw==",
            }
        }
        redacted = harness.redact(value)
        self.assertEqual("Failed", redacted["pdf"]["status"])
        self.assertEqual("RENDERER_TIMEOUT", redacted["pdf"]["errorCode"])
        self.assertNotEqual("QUJDREVGRw==", redacted["pdf"]["base64"])

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

    def test_dry_run_with_unverified_environment_is_incomplete_not_failed(self):
        report = {
            "steps": [{"name": "diagnostic", "ok": True}],
            "checks": {"environmentVerified": False, "preflightValid": True},
            "missingEvidence": ["Emisión real no ejecutada"],
        }
        self.assertEqual("INCOMPLETE", harness.evaluate_overall_status(report, emission_requested=False))

    def test_emission_with_failed_renderer_expectation_fails_gate(self):
        report = {
            "steps": [{"name": "pdf", "ok": True}],
            "checks": {
                "environmentVerified": True,
                "preflightValid": True,
                "emissionSucceeded": True,
                "replaySameNumber": True,
                "replaySameCae": True,
                "pdfFiscalPreserved": True,
                "pdfExpectedStatusObserved": False,
            },
            "missingEvidence": ["PDF inesperado"],
        }
        self.assertEqual("FAILED", harness.evaluate_overall_status(report, emission_requested=True))


if __name__ == "__main__":
    unittest.main()
