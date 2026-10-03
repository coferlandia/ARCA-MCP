from __future__ import annotations


class ArchitectError(Exception):
    """Base expected error."""


class ConfigurationError(ArchitectError):
    pass


class ValidationError(ArchitectError):
    pass


class ConflictError(ArchitectError):
    pass
