#!/usr/bin/env python3
"""Harness reproducible de homologación real para Epic #14 / issue #21.

No contiene secretos ni materializa certificados. Consume un MCP ya desplegado y toma
endpoint/token desde variables de entorno. La emisión fiscal requiere opt-in explícito.
"""

from __future__ import annotations

import argparse
import copy
import datetime as dt
import hashlib
import json
import os
import pathlib
import secrets
import sys
import urllib.error
import urllib.request
from typing import Any

SENSITIVE_KEY_PARTS = (
    "authorization", "token", "password", "certificate", "credential",
    "idempotencykey", "cuit", "cae", "sign", "base64", "path", "secret",
)


def utc_now() -> str:
    return dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")


def digest(value: Any) -> str:
    raw = str(value).encode("utf-8", errors="replace")
    return hashlib.sha256(raw).hexdigest()[:12]


def collect_sensitive_values(value: Any) -> set[str]:
    found: set[str] = set()
    if isinstance(value, dict):
        for key, item in value.items():
            lowered = key.lower()
            if any(part in lowered for part in SENSITIVE_KEY_PARTS):
                if item not in (None, "", 0, False) and not isinstance(item, (dict, list)):
                    found.add(str(item))
            found.update(collect_sensitive_values(item))
    elif isinstance(value, list):
        for item in value:
            found.update(collect_sensitive_values(item))
    return found


def redact(value: Any, known_secrets: set[str] | None = None) -> Any:
    known_secrets = known_secrets or set()
    if isinstance(value, dict):
        result: dict[str, Any] = {}
        for key, item in value.items():
            lowered = key.lower()
            if any(part in lowered for part in SENSITIVE_KEY_PARTS):
                if item in (None, "", 0, False):
                    result[key] = item
                else:
                    result[key] = f"<redacted:sha256:{digest(item)}>"
            else:
                result[key] = redact(item, known_secrets)
        return result
    if isinstance(value, list):
        return [redact(item, known_secrets) for item in value]
    if isinstance(value, str):
        for secret in sorted(known_secrets, key=len, reverse=True):
            if secret and secret in value:
                value = value.replace(secret, f"<redacted:sha256:{digest(secret)}>")
        if len(value) > 4096:
            return f"<omitted:{len(value)} chars:sha256:{digest(value)}>"
    return value


def parse_transport_body(raw: str) -> Any:
    raw = raw.strip()
    if not raw:
        return None
    if raw.startswith("data:") or "\ndata:" in raw:
        payloads = [line[5:].strip() for line in raw.splitlines() if line.startswith("data:")]
        for payload in reversed(payloads):
            try:
                return json.loads(payload)
            except json.JSONDecodeError:
                continue
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        return {"raw": raw}


def unwrap_tool_result(envelope: Any) -> Any:
    if not isinstance(envelope, dict):
        return envelope
    result = envelope.get("result")
    if not isinstance(result, dict):
        return envelope
    content = result.get("content")
    if not isinstance(content, list):
        return result
    texts = [item.get("text") for item in content if isinstance(item, dict) and item.get("type") == "text"]
    if len(texts) == 1 and isinstance(texts[0], str):
        try:
            return json.loads(texts[0])
        except json.JSONDecodeError:
            return texts[0]
    return result


class McpClient:
    def __init__(self, base_url: str, token: str, timeout: int = 45) -> None:
        self.base_url = base_url.rstrip("/") + "/"
        self.token = token
        self.timeout = timeout
        self._rpc_id = 0

    def _request(self, request: urllib.request.Request) -> tuple[int, Any]:
        try:
            with urllib.request.urlopen(request, timeout=self.timeout) as response:
                body = response.read().decode("utf-8", errors="replace")
                return response.status, parse_transport_body(body)
        except urllib.error.HTTPError as exc:
            body = exc.read().decode("utf-8", errors="replace")
            return exc.code, parse_transport_body(body)

    def health(self, path: str) -> tuple[int, Any]:
        target = self.base_url.rstrip("/") + path
        return self._request(urllib.request.Request(target, method="GET"))

    def tool(self, name: str, arguments: dict[str, Any]) -> tuple[int, Any, Any]:
        self._rpc_id += 1
        payload = json.dumps({
            "jsonrpc": "2.0",
            "id": self._rpc_id,
            "method": "tools/call",
            "params": {"name": name, "arguments": arguments},
        }).encode("utf-8")
        request = urllib.request.Request(
            self.base_url,
            data=payload,
            method="POST",
            headers={
                "Authorization": f"Bearer {self.token}",
                "Content-Type": "application/json",
                "Accept": "application/json, text/event-stream",
            },
        )
        status, envelope = self._request(request)
        return status, envelope, unwrap_tool_result(envelope)


def find_key(value: Any, wanted: str) -> Any:
    wanted = wanted.lower()
    if isinstance(value, dict):
        for key, item in value.items():
            if key.lower() == wanted:
                return item
        for item in value.values():
            found = find_key(item, wanted)
            if found is not None:
                return found
    elif isinstance(value, list):
        for item in value:
            found = find_key(item, wanted)
            if found is not None:
                return found
    return None


def step(report: dict[str, Any], name: str, status: int, envelope: Any, tool_result: Any, secrets_set: set[str]) -> Any:
    rpc_error = isinstance(envelope, dict) and envelope.get("error") is not None
    is_error = isinstance(envelope, dict) and isinstance(envelope.get("result"), dict) and envelope["result"].get("isError") is True
    ok = 200 <= status < 300 and not rpc_error and not is_error
    report["steps"].append({
        "name": name,
        "httpStatus": status,
        "ok": ok,
        "result": redact(tool_result, secrets_set),
    })
    return tool_result


def evaluate_overall_status(report: dict[str, Any], emission_requested: bool) -> str:
    failed_steps = [item["name"] for item in report["steps"] if not item["ok"]]
    false_checks: list[str] = []
    if emission_requested:
        critical_checks = (
            "environmentVerified",
            "preflightValid",
            "emissionSucceeded",
            "replaySameNumber",
            "replaySameCae",
            "pdfFiscalPreserved",
            "pdfExpectedStatusObserved",
            "controlledRejectionObserved",
        )
        false_checks = [name for name in critical_checks if name in report["checks"] and report["checks"][name] is False]
    if failed_steps or false_checks:
        return "FAILED"
    if report["missingEvidence"]:
        return "INCOMPLETE"
    return "CANDIDATE_COMPLETE_REQUIRES_HUMAN_REVIEW"


def load_json(path: str) -> dict[str, Any]:
    with open(path, "r", encoding="utf-8") as handle:
        value = json.load(handle)
    if not isinstance(value, dict):
        raise ValueError("El archivo de configuración debe contener un objeto JSON.")
    return value


def write_report(report: dict[str, Any], output_dir: pathlib.Path) -> tuple[pathlib.Path, pathlib.Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    json_path = output_dir / f"epic14-homologacion-{stamp}.json"
    md_path = output_dir / f"epic14-homologacion-{stamp}.md"
    json_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    lines = [
        "# Epic #14 / #21 — evidencia de homologación",
        "",
        f"- Fecha UTC: `{report['startedAt']}`",
        f"- Estado: `{report['overallStatus']}`",
        f"- Build SHA declarado: `{report.get('buildSha') or 'NO_DECLARADO'}`",
        f"- Entorno esperado: `{report.get('expectedEnvironment')}`",
        "- Secretos/PII: redactados; el reporte no contiene token, CUIT, CAE ni idempotency key en claro.",
        "",
        "## Pasos",
        "",
    ]
    for item in report["steps"]:
        lines.append(f"- {'OK' if item['ok'] else 'FAIL'} `{item['name']}` — HTTP {item['httpStatus']}")
    if report.get("checks"):
        lines.extend(["", "## Checks", ""])
        for name, value in report["checks"].items():
            lines.append(f"- `{name}`: `{value}`")
    if report.get("missingEvidence"):
        lines.extend(["", "## Evidencia faltante", ""])
        for item in report["missingEvidence"]:
            lines.append(f"- {item}")
    md_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    return json_path, md_path


def main() -> int:
    parser = argparse.ArgumentParser(description="Homologación controlada de Epic #14 contra un MCP desplegado.")
    parser.add_argument("--config", required=True, help="JSON local con datos de homologación. No debe contener token/certificado.")
    parser.add_argument("--execute-emission", action="store_true", help="Autoriza crear el comprobante de homologación configurado.")
    parser.add_argument("--execute-controlled-rejection", action="store_true", help="Autoriza el escenario ARCA de rechazo configurado.")
    parser.add_argument("--execute-associated-note", action="store_true", help="Autoriza crear la nota asociada configurada.")
    parser.add_argument("--allow-unverified-environment", action="store_true", help="Permite emitir aunque el diagnóstico no confirme homologación.")
    args = parser.parse_args()

    endpoint = os.environ.get("ARCA_MCP_URL", "").strip()
    token = os.environ.get("ARCA_MCP_TOKEN", "").strip()
    build_sha = os.environ.get("ARCA_MCP_BUILD_SHA", "").strip() or None
    if not endpoint or not token:
        print("Faltan ARCA_MCP_URL y/o ARCA_MCP_TOKEN.", file=sys.stderr)
        return 2

    config = load_json(args.config)
    context_id = str(config.get("contextId") or "").strip()
    expected_environment = str(config.get("expectedEnvironment") or "homologacion").strip()
    invoice = config.get("invoice")
    if not context_id or not isinstance(invoice, dict):
        print("config requiere contextId e invoice.", file=sys.stderr)
        return 2

    run_key = f"epic14-hml-{dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')}-{secrets.token_hex(4)}"
    known_secrets = {token, run_key}
    known_secrets.update(collect_sensitive_values(config))
    report: dict[str, Any] = {
        "schema": "arca-epic14-homologation/1.0",
        "startedAt": utc_now(),
        "buildSha": build_sha,
        "expectedEnvironment": expected_environment,
        "overallStatus": "INCOMPLETE",
        "steps": [],
        "checks": {},
        "missingEvidence": [],
    }
    client = McpClient(endpoint, token, int(config.get("timeoutSeconds", 45)))

    for health_path in ("/health/live", "/health/ready"):
        status, body = client.health(health_path)
        report["steps"].append({"name": health_path, "httpStatus": status, "ok": status == 200, "result": redact(body, known_secrets)})
        if status != 200:
            report["missingEvidence"].append(f"{health_path} no está verde.")

    status, envelope, result = client.tool("obtener_capacidades_fiscales", {"contextId": context_id})
    capabilities = step(report, "obtener_capacidades_fiscales", status, envelope, result, known_secrets)
    contract_version = find_key(capabilities, "contractVersion") or find_key(capabilities, "version")
    report["checks"]["contractVersion"] = contract_version or "NO_DETECTADA"

    status, envelope, result = client.tool("diagnosticar_contexto_fiscal", {"contextId": context_id})
    diagnostic = step(report, "diagnosticar_contexto_fiscal", status, envelope, result, known_secrets)
    detected_environment = find_key(diagnostic, "environment")
    report["checks"]["detectedEnvironment"] = detected_environment or "NO_DETECTADO"
    environment_verified = isinstance(detected_environment, str) and detected_environment.lower() == expected_environment.lower()
    report["checks"]["environmentVerified"] = environment_verified

    status, envelope, result = client.tool("validar_comprobante", {"contextId": context_id, "factura": invoice})
    validation = step(report, "validar_comprobante", status, envelope, result, known_secrets)
    validation_valid = find_key(validation, "valid")
    report["checks"]["preflightValid"] = validation_valid

    if not args.execute_emission:
        report["missingEvidence"].append("Emisión real no ejecutada: falta --execute-emission.")
    elif not environment_verified and not args.allow_unverified_environment:
        report["missingEvidence"].append("Entorno de homologación no verificado; emisión bloqueada. Use --allow-unverified-environment sólo tras verificar externamente.")
    elif validation_valid is False:
        report["missingEvidence"].append("Preflight inválido; no se intenta emisión fiscal.")
    else:
        emission_args = {"contextId": context_id, "idempotencyKey": run_key, "factura": copy.deepcopy(invoice)}
        status, envelope, result = client.tool("emitir_comprobante_avanzado", emission_args)
        emission = step(report, "emitir_comprobante_avanzado", status, envelope, result, known_secrets)
        number = find_key(emission, "numeroComprobante")
        operation_id = find_key(emission, "operationId")
        first_cae = find_key(emission, "cae")
        report["checks"]["emissionSucceeded"] = bool(find_key(emission, "success"))
        report["checks"]["operationIdPresent"] = bool(operation_id)
        report["checks"]["invoiceNumberPresent"] = isinstance(number, int) and number > 0

        status, envelope, replay = client.tool("emitir_comprobante_avanzado", emission_args)
        replay = step(report, "replay_same_idempotency_key", status, envelope, replay, known_secrets)
        report["checks"]["replaySameNumber"] = find_key(replay, "numeroComprobante") == number
        report["checks"]["replaySameCae"] = find_key(replay, "cae") == first_cae and first_cae not in (None, "")
        replay_operation = find_key(replay, "operationId")
        if operation_id and replay_operation:
            report["checks"]["replaySameOperationId"] = replay_operation == operation_id

        status, envelope, result = client.tool("consultar_operacion", {"contextId": context_id, "idempotencyKey": run_key})
        step(report, "consultar_operacion", status, envelope, result, known_secrets)

        status, envelope, result = client.tool("reconciliar_operacion", {"contextId": context_id, "idempotencyKey": run_key})
        step(report, "reconciliar_operacion", status, envelope, result, known_secrets)

        if isinstance(number, int) and number > 0:
            invoice_type = invoice.get("tipoComprobante")
            status, envelope, result = client.tool("consultar_comprobante", {
                "contextId": context_id,
                "numeroComprobante": number,
                "tipoComprobante": invoice_type,
            })
            step(report, "consultar_comprobante", status, envelope, result, known_secrets)

            pdf = config.get("pdf")
            if isinstance(pdf, dict) and pdf.get("enabled"):
                expected_pdf_status = str(pdf.get("expectedStatus") or "").strip()
                if not expected_pdf_status:
                    report["missingEvidence"].append("PDF habilitado sin expectedStatus; no se puede validar el escenario esperado.")
                status, envelope, result = client.tool("generar_pdf_comprobante", {
                    "contextId": context_id,
                    "numeroComprobante": number,
                    "tipoComprobante": invoice_type,
                    "templateId": pdf.get("templateId"),
                    "templateVersion": pdf.get("templateVersion"),
                    "templateData": pdf.get("templateData") or {},
                })
                pdf_result = step(report, "generar_pdf_comprobante", status, envelope, result, known_secrets)
                observed_pdf_status = str(find_key(pdf_result, "status") or "")
                observed_pdf_error = find_key(pdf_result, "errorCode")
                report["checks"]["pdfStatus"] = observed_pdf_status or "NO_DETECTADO"
                report["checks"]["pdfErrorCode"] = observed_pdf_error or "NONE"
                report["checks"]["pdfFiscalPreserved"] = bool(find_key(pdf_result, "success")) and find_key(pdf_result, "cae") == first_cae
                if expected_pdf_status:
                    report["checks"]["pdfExpectedStatusObserved"] = observed_pdf_status.lower() == expected_pdf_status.lower()
                    if observed_pdf_status.lower() != expected_pdf_status.lower():
                        report["missingEvidence"].append(f"PDF esperaba status={expected_pdf_status} y devolvió {observed_pdf_status or 'NO_DETECTADO'}.")
            else:
                report["missingEvidence"].append("Escenario PDF/renderer no configurado.")

            note = config.get("associatedNote")
            if isinstance(note, dict) and note.get("enabled"):
                if args.execute_associated_note:
                    note_invoice = copy.deepcopy(note.get("invoice") or {})
                    note_invoice["cbteAsociadoTipo"] = invoice_type
                    note_invoice["cbteAsociadoNro"] = number
                    if "cbteAsociadoPtoVta" not in note_invoice and config.get("pointOfSale"):
                        note_invoice["cbteAsociadoPtoVta"] = config["pointOfSale"]
                    note_key = run_key + "-note"
                    known_secrets.add(note_key)
                    status, envelope, result = client.tool("emitir_comprobante_avanzado", {
                        "contextId": context_id,
                        "idempotencyKey": note_key,
                        "factura": note_invoice,
                    })
                    note_result = step(report, "emitir_nota_asociada", status, envelope, result, known_secrets)
                    report["checks"]["associatedNoteSucceeded"] = bool(find_key(note_result, "success"))
                else:
                    report["missingEvidence"].append("Nota asociada configurada pero no ejecutada: falta --execute-associated-note.")

    rejection = config.get("controlledRejection")
    if isinstance(rejection, dict) and rejection.get("enabled"):
        if args.execute_controlled_rejection:
            rejection_key = run_key + "-reject"
            known_secrets.add(rejection_key)
            status, envelope, result = client.tool("emitir_comprobante_avanzado", {
                "contextId": context_id,
                "idempotencyKey": rejection_key,
                "factura": rejection.get("invoice") or {},
            })
            rejected = step(report, "controlled_fiscal_rejection", status, envelope, result, known_secrets)
            rejection_outcome = str(find_key(rejected, "emissionOutcome") or "")
            report["checks"]["controlledRejectionOutcome"] = rejection_outcome or "NO_DETECTADO"
            report["checks"]["controlledRejectionObserved"] = rejection_outcome.lower() == "fiscalrejected"
            if rejection_outcome.lower() != "fiscalrejected":
                report["missingEvidence"].append("El escenario de rechazo no produjo EmissionOutcome=FiscalRejected; no cuenta como rechazo ARCA homologado.")
        else:
            report["missingEvidence"].append("Rechazo fiscal configurado pero no ejecutado: falta --execute-controlled-rejection.")
    else:
        report["missingEvidence"].append("Escenario de rechazo fiscal controlado no configurado.")

    if not build_sha:
        report["missingEvidence"].append("ARCA_MCP_BUILD_SHA no fue declarado; falta trazabilidad exacta del artefacto desplegado.")

    report["overallStatus"] = evaluate_overall_status(report, args.execute_emission)

    output_dir = pathlib.Path(config.get("outputDirectory") or ".homologation-evidence")
    json_path, md_path = write_report(report, output_dir)
    print(f"Reporte JSON: {json_path}")
    print(f"Reporte Markdown: {md_path}")
    print(f"Estado: {report['overallStatus']}")
    return 1 if report["overallStatus"] == "FAILED" else 0


if __name__ == "__main__":
    raise SystemExit(main())
