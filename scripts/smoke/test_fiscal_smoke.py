#!/usr/bin/env python3
import pathlib
import sys
import unittest
from decimal import Decimal

HERE = pathlib.Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import fiscal_smoke as smoke


class FiscalSmokeTests(unittest.TestCase):
    def receptor(self):
        return {
            "documentNumber": 20123456789,
            "documentType": 80,
            "vatCondition": "ResponsableInscripto",
        }

    def test_type_matrix_covers_invoice_debit_and_credit_notes_for_abc(self):
        self.assertEqual(smoke.type_name("A", "invoice"), "FacturaA")
        self.assertEqual(smoke.type_name("A", "debit"), "NotaDebitoA")
        self.assertEqual(smoke.type_name("A", "credit"), "NotaCreditoA")
        self.assertEqual(smoke.type_name("B", "invoice"), "FacturaB")
        self.assertEqual(smoke.type_name("B", "debit"), "NotaDebitoB")
        self.assertEqual(smoke.type_name("B", "credit"), "NotaCreditoB")
        self.assertEqual(smoke.type_name("C", "invoice"), "FacturaC")
        self.assertEqual(smoke.type_name("C", "debit"), "NotaDebitoC")
        self.assertEqual(smoke.type_name("C", "credit"), "NotaCreditoC")

    def test_a_and_b_use_one_peso_total_with_21_percent_iva(self):
        for letter in ("A", "B"):
            invoice = smoke.build_invoice(letter, "invoice", self.receptor(), "20261003")
            self.assertEqual(Decimal(str(invoice["importeNeto"])), Decimal("0.83"))
            self.assertEqual(Decimal(str(invoice["importeIva"])), Decimal("0.17"))
            self.assertEqual(Decimal(str(invoice["importeTotal"])), Decimal("1.00"))
            self.assertEqual(invoice["alicuotaIva"], "Veintiuno")

    def test_c_uses_one_peso_total_without_iva_detail(self):
        invoice = smoke.build_invoice("C", "invoice", self.receptor(), "20261003")
        self.assertEqual(Decimal(str(invoice["importeNeto"])), Decimal("1.00"))
        self.assertEqual(Decimal(str(invoice["importeIva"])), Decimal("0.00"))
        self.assertEqual(Decimal(str(invoice["importeTotal"])), Decimal("1.00"))
        self.assertNotIn("alicuotaIva", invoice)

    def test_notes_are_associated_to_the_requested_document(self):
        note = smoke.build_invoice(
            "B",
            "credit",
            self.receptor(),
            "20261003",
            association={"tipo": "NotaDebitoB", "puntoVenta": 77, "numero": 123},
        )
        self.assertEqual(note["cbteAsociadoTipo"], "NotaDebitoB")
        self.assertEqual(note["cbteAsociadoPtoVta"], 77)
        self.assertEqual(note["cbteAsociadoNro"], 123)

    def test_template_items_are_two_lines_totalling_one_peso(self):
        items = smoke.smoke_items()
        self.assertEqual(len(items), 2)
        self.assertEqual(sum(Decimal(str(i["total"])) for i in items), Decimal("1.00"))
        self.assertEqual([Decimal(str(i["total"])) for i in items], [Decimal("0.45"), Decimal("0.55")])

    def test_hard_guard_rejects_any_document_over_one_peso(self):
        invoice = smoke.build_invoice("A", "invoice", self.receptor(), "20261003")
        invoice["importeTotal"] = 1.01
        with self.assertRaisesRegex(ValueError, "ARS 1.00"):
            smoke.assert_safe_invoice(invoice)

    def test_production_requires_execute_and_allow_production(self):
        with self.assertRaisesRegex(ValueError, "--execute"):
            smoke.assert_execution_allowed("produccion", execute=False, allow_production=False)
        with self.assertRaisesRegex(ValueError, "--allow-production"):
            smoke.assert_execution_allowed("produccion", execute=True, allow_production=False)
        smoke.assert_execution_allowed("produccion", execute=True, allow_production=True)

    def test_homologation_requires_execute_but_not_allow_production(self):
        with self.assertRaisesRegex(ValueError, "--execute"):
            smoke.assert_execution_allowed("homologacion", execute=False, allow_production=False)
        smoke.assert_execution_allowed("homologacion", execute=True, allow_production=False)

    def test_receiver_can_be_overridden_per_letter(self):
        default = self.receptor()
        consumer = {"documentNumber": 0, "documentType": 99, "vatCondition": "ConsumidorFinal"}
        context = {"receiver": default, "receivers": {"B": consumer}}
        self.assertEqual(smoke.receiver_for(context, "A"), default)
        self.assertEqual(smoke.receiver_for(context, "B"), consumer)


if __name__ == "__main__":
    unittest.main()
