"""Deterministic Config Toolsmith implementation."""

CLI_VERSION = "1.0.0"
CONTRACT_SCHEMA_VERSION = 1

from .source_normalization import install as _install_source_normalization

_install_source_normalization()
del _install_source_normalization
