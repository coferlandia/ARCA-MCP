/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.Globalization;
using System.Text;

namespace dcArca.Core.Services;

/// <summary>
/// Excepción lanzada cuando se detecta que el token/sign es inválido (ej: cambio de entorno).
/// Se usa para forzar refresh automático del token.
/// </summary>
public class dcTokenInvalidException : Exception
{
    public dcTokenInvalidException(string message) : base(message) { }
}

internal static class dcTokenFaultDetector
{
    public static bool IsInvalidSignature(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return false;

        var normalized = new string(reason.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return normalized.Contains("firma", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("valida", StringComparison.OrdinalIgnoreCase);
    }
}
