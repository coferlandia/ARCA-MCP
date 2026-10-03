# Epic #14 — correctivos del code review independiente

El code review independiente de `main@3f45387714e9ac37992901be8778bc4b3f023bfc` identificó cuatro correctivos que bloquean la homologación real de #21:

- #38: fijación de `CredentialAssignmentRevision` antes de cualquier side effect fiscal.
- #39: estados terminales fiscales monotónicos frente a reconciliación/emisión concurrente.
- #40: preservación de presencia/validez de evidencia monetaria proveniente de `FECompConsultar`.
- #41: eliminación de la carrera del certificado placeholder en la suite MCP.

La homologación real debe ejecutarse únicamente sobre un `main` que contenga los cuatro correctivos y tenga CI/review verdes. Esta nota no sustituye la evidencia de homologación requerida por #21.
