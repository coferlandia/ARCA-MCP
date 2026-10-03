#!/usr/bin/env python3
"""Production-safe fiscal smoke runner for ARCA-MCP.

The runner intentionally uses tiny ARS values and balances every confirmed
fiscal authorization with compensating credit notes. It can run against
homologation or production; production requires an explicit double opt-in.
"""

from __future__ import annotations

import argparse
import base64
import copy
import datetime as dt
import json
import os
import pathlib
import secrets
import sys
from decimal import Decimal
from typing import Any

HERE = pathlib.Path(__file__).resolve().parent
REPO_ROOT = HERE.parents[1]
HOMOLOGACION_DIR = REPO_ROOT / "scripts" / "homologacion"
if str(HOMOLOGACION_DIR) not in sys.path:
    sys.path.insert(0, str(HOMOLOGACION_DIR))

from epic14_homologacion import McpClient, collect_sensitive_values, find_key, redact  # noqa: E402

MAX_DOCUMENT_TOTAL = Decimal("1.00")
HOMOLOGATION_ENVIRONMENTS = {"homologacion", "homologation", "homo"}
PRODUCTION_ENVIRONMENTS = {"produccion", "production", "prod"}
TYPE_MATRIX = {
    "A": {"invoice": "FacturaA", "debit": "NotaDebitoA", "credit": "NotaCreditoA"},
    "B": {"invoice": "FacturaB", "debit": "NotaDebitoB", "credit": "NotaCreditoB"},
    "C": {"invoice": "FacturaC", "debit": "NotaDebitoC", "credit": "NotaCreditoC"},
}


def type_name(letter: str, kind: str) -> str:
    try:
        return TYPE_MATRIX[letter.upper()][kind]
    except KeyError as exc:
        raise ValueError(f"Combinación de comprobante no soportada: {letter}/{kind}") from exc


def smoke_items() -> list[dict[str, Any]]:
    return [
        {"description": "ARCA-MCP smoke item 1", "quantity": 1, "unitPrice": 0.45, "total": 0.45},
        {"description": "ARCA-MCP smoke item 2", "quantity": 1, "unitPrice": 0.55, "total": 0.55},
    ]


def receiver_for(context: dict[str, Any], letter: str) -> dict[str, Any]:
    overrides = context.get("receivers") or {}
    if isinstance(overrides, dict) and isinstance(overrides.get(letter.upper()), dict):
        return overrides[letter.upper()]
    receiver = context.get("receiver")
    if not isinstance(receiver, dict):
        raise ValueError(f"No hay receiver configurado para letra {letter.upper()}.")
    return receiver


def build_invoice(letter: str, kind: str, receptor: dict[str, Any], invoice_date: str,
                  association: dict[str, Any] | None = None) -> dict[str, Any]:
    letter = letter.upper()
    invoice: dict[str, Any] = {
        "tipoComprobante": type_name(letter, kind),
        "concepto": "Productos",
        "cuitReceptor": int(receptor["documentNumber"]),
        "tipoDocReceptor": int(receptor.get("documentType", 80)),
        "condicionIvaReceptor": receptor["vatCondition"],
        "importeNoGravado": 0.00,
        "importeExento": 0.00,
        "monedaId": "PES",
        "monedaCotizacion": 1.00,
        "fechaComprobante": invoice_date,
    }
    if letter in ("A", "B"):
        invoice.update({"importeNeto": 0.83, "importeIva": 0.17, "importeTotal": 1.00, "alicuotaIva": "Veintiuno"})
    elif letter == "C":
        invoice.update({"importeNeto": 1.00, "importeIva": 0.00, "importeTotal": 1.00})
    else:
        raise ValueError(f"Letra no soportada: {letter}")
    if kind != "invoice":
        if not association:
            raise ValueError("Las notas requieren comprobante asociado.")
        invoice.update({
            "cbteAsociadoTipo": association["tipo"],
            "cbteAsociadoPtoVta": int(association["puntoVenta"]),
            "cbteAsociadoNro": int(association["numero"]),
        })
    assert_safe_invoice(invoice)
    return invoice


def assert_safe_invoice(invoice: dict[str, Any]) -> None:
    if str(invoice.get("monedaId", "PES")).upper() != "PES":
        raise ValueError("Smoke fiscal sólo admite moneda PES.")
    total = Decimal(str(invoice.get("importeTotal", 0)))
    if total <= 0 or total > MAX_DOCUMENT_TOTAL:
        raise ValueError(f"Cada comprobante smoke debe ser > 0 y <= ARS {MAX_DOCUMENT_TOTAL:.2f}.")


def assert_execution_allowed(environment: str, *, execute: bool, allow_production: bool) -> None:
    env = environment.strip().lower()
    if env not in HOMOLOGATION_ENVIRONMENTS | PRODUCTION_ENVIRONMENTS:
        raise ValueError(f"Entorno fiscal no reconocido: {environment!r}.")
    if not execute:
        raise ValueError("La emisión requiere --execute.")
    if env in PRODUCTION_ENVIRONMENTS and not allow_production:
        raise ValueError("Producción requiere además --allow-production.")


def must_compensate(result: dict[str, Any] | None) -> bool:
    if not isinstance(result, dict) or not bool(result.get("fiscalAuthorized")):
        return False
    number = result.get("number")
    return isinstance(number, int) and number > 0


def _load_json(path: pathlib.Path) -> dict[str, Any]:
    value = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(value, dict):
        raise ValueError("La configuración smoke debe ser un objeto JSON.")
    return value


def _tool_ok(http_status: int, envelope: Any) -> bool:
    return (
        200 <= http_status < 300
        and not (isinstance(envelope, dict) and envelope.get("error") is not None)
        and not (isinstance(envelope, dict) and isinstance(envelope.get("result"), dict)
                 and envelope["result"].get("isError") is True)
    )


def _pdf_base64(value: Any) -> str | None:
    for key in ("base64", "pdfBase64", "contentBase64"):
        found = find_key(value, key)
        if isinstance(found, str) and found.strip():
            return found.strip()
    return None


def _call(client: McpClient, name: str, args: dict[str, Any]) -> tuple[Any, bool]:
    status, envelope, result = client.tool(name, args)
    return result, _tool_ok(status, envelope)


def _merge_template_data(base: dict[str, Any] | None, label: str) -> dict[str, Any]:
    data = copy.deepcopy(base or {})
    data.setdefault("smokeTest", True)
    data.setdefault("smokeLabel", label)
    data.setdefault("items", smoke_items())
    return data


def _write_pdf(result: Any, path: pathlib.Path) -> bool:
    encoded = _pdf_base64(result)
    if not encoded:
        return False
    try:
        payload = base64.b64decode(encoded, validate=True)
    except Exception:
        return False
    if not payload.startswith(b"%PDF"):
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)
    return True


def _emit_document(*, client: McpClient, context_id: str, letter: str, label: str,
                   invoice: dict[str, Any], pdf_cfg: dict[str, Any], output_dir: pathlib.Path,
                   run_key: str) -> dict[str, Any]:
    assert_safe_invoice(invoice)
    result: dict[str, Any] = {
        "label": label,
        "type": invoice["tipoComprobante"],
        "status": "FAIL",
        "fiscalAuthorized": False,
    }
    validation, validation_ok = _call(client, "validar_comprobante", {"contextId": context_id, "factura": invoice})
    is_valid = find_key(validation, "valid")
    result["preflightValid"] = bool(is_valid)
    if not validation_ok or is_valid is False:
        result.update({"status": "SKIPPED_UNSUPPORTED", "reason": "preflight_invalid"})
        return result

    emission_args = {"contextId": context_id, "idempotencyKey": run_key, "factura": invoice}
    emission, emission_ok = _call(client, "emitir_comprobante_avanzado", emission_args)
    number = find_key(emission, "numeroComprobante")
    cae = find_key(emission, "cae")
    fiscal_authorized = (
        emission_ok
        and bool(find_key(emission, "success"))
        and isinstance(number, int)
        and number > 0
        and cae not in (None, "")
    )
    if not fiscal_authorized:
        result.update({"reason": "emission_failed", "emission": emission})
        return result

    # From this point forward the fiscal side effect is confirmed. Any ancillary
    # failure must preserve enough state for _run_letter to compensate it.
    result.update({"fiscalAuthorized": True, "number": number})

    replay, replay_ok = _call(client, "emitir_comprobante_avanzado", emission_args)
    if not replay_ok or find_key(replay, "numeroComprobante") != number or find_key(replay, "cae") != cae:
        result["reason"] = "idempotency_failed"
        return result

    operation, operation_ok = _call(client, "consultar_operacion", {"contextId": context_id, "idempotencyKey": run_key})
    reconciled, reconcile_ok = _call(client, "reconciliar_operacion", {"contextId": context_id, "idempotencyKey": run_key})
    official, official_ok = _call(client, "consultar_comprobante", {
        "contextId": context_id,
        "numeroComprobante": number,
        "tipoComprobante": invoice["tipoComprobante"],
    })
    if not (operation_ok and reconcile_ok and official_ok and bool(find_key(official, "success"))):
        result["reason"] = "verification_failed"
        return result

    template_id = str(pdf_cfg.get("templateId") or "").strip()
    template_version = str(pdf_cfg.get("templateVersion") or "").strip()
    if not template_id or not template_version:
        result["reason"] = "pdf_template_missing"
        return result

    pdf_result, pdf_ok = _call(client, "generar_pdf_comprobante", {
        "contextId": context_id,
        "numeroComprobante": number,
        "tipoComprobante": invoice["tipoComprobante"],
        "templateId": template_id,
        "templateVersion": template_version,
        "templateData": _merge_template_data(pdf_cfg.get("templateData"), label),
    })
    pdf_path = output_dir / letter / f"{label}-{number}.pdf"
    if not (pdf_ok and _write_pdf(pdf_result, pdf_path)):
        result.update({"reason": "pdf_failed", "pdfStatus": find_key(pdf_result, "status"),
                       "pdfErrorCode": find_key(pdf_result, "errorCode")})
        return result

    result.update({
        "status": "PASS",
        "pdf": str(pdf_path),
        "operationIdPresent": bool(find_key(operation, "operationId")),
        "reconcileOutcome": find_key(reconciled, "emissionOutcome"),
    })
    return result


def _run_letter(*, client: McpClient, context: dict[str, Any], letter: str, date: str,
                output_dir: pathlib.Path, run_prefix: str) -> dict[str, Any]:
    context_id = str(context["contextId"])
    receptor = receiver_for(context, letter)
    point_of_sale = int(context["pointOfSale"])
    pdf_cfg = context.get("pdf") or {}
    docs: list[dict[str, Any]] = []

    invoice = build_invoice(letter, "invoice", receptor, date)
    invoice_result = _emit_document(client=client, context_id=context_id, letter=letter,
        label=f"factura-{letter.lower()}", invoice=invoice, pdf_cfg=pdf_cfg,
        output_dir=output_dir, run_key=f"{run_prefix}-{letter.lower()}-invoice")
    docs.append(invoice_result)

    if not must_compensate(invoice_result):
        return {
            "letter": letter,
            "status": invoice_result["status"],
            "documents": docs,
            "fiscallyBalanced": True,
            "unbalanced": False,
            "netEffectArs": "0.00",
        }

    invoice_assoc = {"tipo": type_name(letter, "invoice"), "puntoVenta": point_of_sale,
                     "numero": invoice_result["number"]}

    debit_result: dict[str, Any] | None = None
    credit_debit_result: dict[str, Any] | None = None
    # Only extend the functional smoke circuit when the invoice itself passed all
    # checks. If an ancillary invoice check failed, prioritize compensation.
    if invoice_result["status"] == "PASS":
        debit = build_invoice(letter, "debit", receptor, date, invoice_assoc)
        debit_result = _emit_document(client=client, context_id=context_id, letter=letter,
            label=f"nota-debito-{letter.lower()}", invoice=debit, pdf_cfg=pdf_cfg,
            output_dir=output_dir, run_key=f"{run_prefix}-{letter.lower()}-debit")
        docs.append(debit_result)

        if must_compensate(debit_result):
            debit_assoc = {"tipo": type_name(letter, "debit"), "puntoVenta": point_of_sale,
                           "numero": debit_result["number"]}
            credit_debit = build_invoice(letter, "credit", receptor, date, debit_assoc)
            credit_debit_result = _emit_document(client=client, context_id=context_id, letter=letter,
                label=f"nota-credito-{letter.lower()}-anula-nd", invoice=credit_debit, pdf_cfg=pdf_cfg,
                output_dir=output_dir, run_key=f"{run_prefix}-{letter.lower()}-credit-debit")
            docs.append(credit_debit_result)

    # Once the invoice exists, always attempt its compensating credit note,
    # regardless of replay/verification/PDF failures on the invoice itself.
    credit_invoice = build_invoice(letter, "credit", receptor, date, invoice_assoc)
    credit_invoice_result = _emit_document(client=client, context_id=context_id, letter=letter,
        label=f"nota-credito-{letter.lower()}-anula-factura", invoice=credit_invoice, pdf_cfg=pdf_cfg,
        output_dir=output_dir, run_key=f"{run_prefix}-{letter.lower()}-credit-invoice")
    docs.append(credit_invoice_result)

    invoice_compensated = must_compensate(credit_invoice_result)
    debit_needs_compensation = must_compensate(debit_result)
    debit_compensated = not debit_needs_compensation or must_compensate(credit_debit_result)
    fiscally_balanced = invoice_compensated and debit_compensated

    full_pass = (
        invoice_result["status"] == "PASS"
        and debit_result is not None and debit_result["status"] == "PASS"
        and credit_debit_result is not None and credit_debit_result["status"] == "PASS"
        and credit_invoice_result["status"] == "PASS"
    )
    return {
        "letter": letter,
        "status": "PASS" if full_pass else "FAIL",
        "documents": docs,
        "compensationAttempted": True,
        "fiscallyBalanced": fiscally_balanced,
        "unbalanced": not fiscally_balanced,
        "netEffectArs": "0.00" if fiscally_balanced else "UNKNOWN",
    }


def _write_summary(report: dict[str, Any], output_dir: pathlib.Path, known_secrets: set[str]) -> None:
    safe = redact(report, known_secrets)
    (output_dir / "summary.json").write_text(json.dumps(safe, ensure_ascii=False, indent=2), encoding="utf-8")
    lines = ["# ARCA-MCP fiscal smoke", "", f"- Environment: `{safe['environment']}`",
             f"- Overall: `{safe['overallStatus']}`", f"- Started UTC: `{safe['startedAt']}`", "", "## Results", ""]
    for ctx in safe["contexts"]:
        lines.append(f"### Context `{ctx['contextId']}`")
        for result in ctx["results"]:
            balance = result.get("fiscallyBalanced")
            lines.append(f"- {result['letter']}: `{result['status']}` — fiscallyBalanced=`{balance}`")
            for doc in result.get("documents", []):
                suffix = f" → `{doc.get('pdf')}`" if doc.get("pdf") else ""
                lines.append(f"  - {doc['label']}: `{doc['status']}` fiscalAuthorized=`{doc.get('fiscalAuthorized')}`{suffix}")
        lines.append("")
    (output_dir / "summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")


def _add_receiver_secrets(known_secrets: set[str], contexts: list[Any]) -> None:
    for context in contexts:
        if not isinstance(context, dict):
            continue
        receivers: list[Any] = [context.get("receiver")]
        overrides = context.get("receivers")
        if isinstance(overrides, dict):
            receivers.extend(overrides.values())
        for receiver in receivers:
            if isinstance(receiver, dict):
                number = receiver.get("documentNumber")
                if number not in (None, "", 0, "0"):
                    known_secrets.add(str(number))


def main() -> int:
    parser = argparse.ArgumentParser(description="Smoke fiscal ARCA-MCP apto para homologación y producción.")
    parser.add_argument("--config", required=True)
    parser.add_argument("--execute", action="store_true", help="Habilita emisiones fiscales reales.")
    parser.add_argument("--allow-production", action="store_true", help="Segundo opt-in obligatorio si el MCP reporta producción.")
    parser.add_argument("--output-dir", default=".smoke-evidence")
    args = parser.parse_args()

    endpoint = os.environ.get("ARCA_MCP_URL", "").strip()
    token = os.environ.get("ARCA_MCP_TOKEN", "").strip()
    if not endpoint or not token:
        print("Faltan ARCA_MCP_URL y/o ARCA_MCP_TOKEN.", file=sys.stderr)
        return 2

    config = _load_json(pathlib.Path(args.config))
    contexts = config.get("contexts")
    if not isinstance(contexts, list) or not contexts:
        print("La configuración requiere contexts[].", file=sys.stderr)
        return 2

    stamp = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    run_prefix = f"arca-smoke-{stamp}-{secrets.token_hex(4)}"
    output_dir = pathlib.Path(args.output_dir) / stamp
    output_dir.mkdir(parents=True, exist_ok=True)
    client = McpClient(endpoint, token, int(config.get("timeoutSeconds", 45)))
    known_secrets = {token, run_prefix}
    known_secrets.update(collect_sensitive_values(config))
    _add_receiver_secrets(known_secrets, contexts)
    report: dict[str, Any] = {
        "schema": "arca-fiscal-smoke/1.0",
        "startedAt": dt.datetime.now(dt.timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        "environment": "UNKNOWN",
        "overallStatus": "FAIL",
        "contexts": [],
    }

    any_skips = False
    any_fail = False
    for context in contexts:
        context_id = str(context.get("contextId") or "").strip()
        if not context_id:
            print("Cada contexto requiere contextId.", file=sys.stderr)
            return 2
        diagnostic, diag_ok = _call(client, "diagnosticar_contexto_fiscal", {"contextId": context_id})
        environment = str(find_key(diagnostic, "environment") or "").strip()
        if not diag_ok or not environment:
            print(f"No se pudo diagnosticar environment para {context_id}.", file=sys.stderr)
            return 3
        report["environment"] = environment if report["environment"] == "UNKNOWN" else report["environment"]
        try:
            assert_execution_allowed(environment, execute=args.execute, allow_production=args.allow_production)
        except ValueError as exc:
            print(str(exc), file=sys.stderr)
            return 4

        letters = [str(x).upper() for x in context.get("letters", ["A", "B", "C"])]
        ctx_report = {"contextId": context_id, "environment": environment, "results": []}
        for letter in letters:
            result = _run_letter(client=client, context=context, letter=letter,
                date=dt.date.today().strftime("%Y%m%d"), output_dir=output_dir, run_prefix=run_prefix)
            ctx_report["results"].append(result)
            any_skips = any_skips or result["status"].startswith("SKIPPED")
            any_fail = any_fail or result["status"] == "FAIL"
        report["contexts"].append(ctx_report)

    report["overallStatus"] = "FAIL" if any_fail else ("PASS_WITH_SKIPS" if any_skips else "PASS")
    _write_summary(report, output_dir, known_secrets)
    print(f"Smoke result: {report['overallStatus']}")
    print(f"Evidence: {output_dir}")
    return 1 if any_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
