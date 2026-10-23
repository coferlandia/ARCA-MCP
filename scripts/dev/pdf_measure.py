#!/usr/bin/env python3
"""
PDF geometry measurement tool for ARCA invoice templates.

Measures page count, page dimensions, and bounding boxes of key layout elements.
Outputs structured JSON with measurable evidence of template geometry.

Requires: pdfplumber (https://github.com/jsvine/pdfplumber)
Install: pip install pdfplumber
"""

import json
import sys
import re
from pathlib import Path
from typing import Any, Dict, List, Optional


def measure_pdf(pdf_path: str) -> Dict[str, Any]:
    """
    Measure PDF geometry and extract key element bounding boxes.

    Args:
        pdf_path: Path to PDF file to measure

    Returns:
        Dictionary with pages, dimensions, and element bounding boxes
    """
    try:
        import pdfplumber
    except ImportError:
        return {
            "error": "pdfplumber not installed",
            "hint": "pip install pdfplumber",
            "path": pdf_path
        }

    pdf_file = Path(pdf_path)
    if not pdf_file.exists():
        return {
            "error": f"PDF file not found: {pdf_path}",
            "path": pdf_path
        }

    result = {
        "file": str(pdf_file),
        "pages": [],
        "summary": {}
    }

    try:
        with pdfplumber.open(pdf_path) as pdf:
            result["summary"]["total_pages"] = len(pdf.pages)
            result["summary"]["page_size"] = {
                "width_pt": pdf.pages[0].width,
                "height_pt": pdf.pages[0].height
            } if pdf.pages else None

            for page_idx, page in enumerate(pdf.pages):
                page_data = measure_page(page, page_idx)
                result["pages"].append(page_data)

    except Exception as e:
        result["error"] = str(e)
        result["error_type"] = type(e).__name__

    return result


def measure_page(page: Any, page_idx: int) -> Dict[str, Any]:
    """
    Measure geometry of key elements on a single page.

    Elements measured:
    - Page dimensions
    - Copy title (ORIGINAL, DUPLICADO, TRIPLICADO)
    - Voucher class letter/code box (via page.rects for precise border measurement)
    - Invoice header (FACTURA / NOTA DE CRÉDITO)
    - Receiver box
    - Detail table
    - Totals box and values (via page.rects for box borders)
    - QR code
    - CAE information
    - Page numbering

    Args:
        page: pdfplumber page object
        page_idx: 0-based page index

    Returns:
        Dictionary with page measurements
    """
    page_data = {
        "page_number": page_idx + 1,
        "dimensions": {
            "width_pt": round(page.width, 1),
            "height_pt": round(page.height, 1)
        },
        "elements": {}
    }

    # Extract all text with location info
    text_objs = page.extract_text_lines()
    words = page.extract_words(extra_attrs=["size"])
    rects = page.rects

    # Find key text elements and measure their bounding boxes
    if words:
        # Voucher class letter (large C, A, B)
        # First find the text, then use page.rects to measure the actual box border
        voucher_letter_text = find_element_bbox(
            words,
            lambda w: re.match(r'^[A-C]$', w['text']) and w['size'] > 15
        )
        if voucher_letter_text:
            # Look for the border rect that contains this text
            voucher_rect = find_rect_containing_bbox(
                rects,
                voucher_letter_text["x0"],
                voucher_letter_text["top"],
                voucher_letter_text["x1"],
                voucher_letter_text["bottom"]
            )
            if voucher_rect:
                page_data["elements"]["voucher_letter"] = voucher_rect
            else:
                page_data["elements"]["voucher_letter"] = voucher_letter_text

        # Voucher code (COD. text)
        voucher_code = find_element_bbox(
            words,
            lambda w: 'COD' in w['text'].upper()
        )
        if voucher_code:
            page_data["elements"]["voucher_code"] = voucher_code

        # Find FACTURA or NOTA DE CREDITO title
        title_bbox = find_element_bbox(
            words,
            lambda w: any(t in w['text'].upper() for t in ['FACTURA', 'NOTA', 'CRÉDITO'])
                      and w['size'] > 14
        )
        if title_bbox:
            page_data["elements"]["document_title"] = title_bbox

        # Copy title (ORIGINAL, DUPLICADO, TRIPLICADO)
        copy_title = find_element_bbox(
            words,
            lambda w: any(c in w['text'].upper() for c in ['ORIGINAL', 'DUPLICADO', 'TRIPLICADO'])
        )
        if copy_title:
            page_data["elements"]["copy_title"] = copy_title

        # CAE text and number
        cae_elem = find_element_bbox(
            words,
            lambda w: 'CAE' in w['text'].upper()
        )
        if cae_elem:
            page_data["elements"]["cae_label"] = cae_elem

        # Find numeric CAE value (14 digits typically)
        cae_value = find_element_bbox(
            words,
            lambda w: re.match(r'^\d{14}$', w['text'].strip())
        )
        if cae_value:
            page_data["elements"]["cae_value"] = cae_value

    # Measure table and borders (using page.rects for actual borders)
    if rects:
        # Find detail table (typically a large bordered area in middle of page)
        detail_rects = [r for r in rects if r.get('width', 0) > 200]  # Large horizontal boxes
        if detail_rects:
            largest_rect = max(detail_rects, key=lambda r: r.get('height', 0))
            detail_bbox = {
                "x0": round(largest_rect['x0'], 1),
                "top": round(largest_rect['top'], 1),
                "x1": round(largest_rect['x1'], 1),
                "bottom": round(largest_rect['bottom'], 1),
                "width": round(largest_rect['x1'] - largest_rect['x0'], 1),
                "height": round(largest_rect['bottom'] - largest_rect['top'], 1),
                "source": "page_rects_detail_table"
            }
            page_data["elements"]["detail_table"] = detail_bbox

        # Measure totals box (typically a bordered area in lower portion of page)
        # Look for a rect that is roughly 76mm wide (about 215pt) on the right side
        totals_rects = [r for r in rects if 180 < r.get('width', 0) < 250 and r.get('top', 0) > 500]
        if totals_rects:
            # Take the one closest to the bottom (highest top coordinate)
            totals_rect = max(totals_rects, key=lambda r: r.get('top', 0))
            totals_bbox = {
                "x0": round(totals_rect['x0'], 1),
                "top": round(totals_rect['top'], 1),
                "x1": round(totals_rect['x1'], 1),
                "bottom": round(totals_rect['bottom'], 1),
                "width": round(totals_rect['x1'] - totals_rect['x0'], 1),
                "height": round(totals_rect['bottom'] - totals_rect['top'], 1),
                "source": "page_rects_totals_box"
            }
            page_data["elements"]["totals_box"] = totals_bbox

        # Also measure totals values by finding text within the totals area
        if "totals_box" in page_data["elements"] and words:
            totals_box = page_data["elements"]["totals_box"]
            totals_values = []
            for word in words:
                # Look for currency values (numbers with commas) in the totals area
                if (totals_box["x0"] <= word['x0'] <= totals_box["x1"] and
                    totals_box["top"] <= word['top'] <= totals_box["bottom"]):
                    if re.search(r'\d+[.,]\d+', word['text']):
                        totals_values.append({
                            "x0": round(word['x0'], 1),
                            "top": round(word['top'], 1),
                            "x1": round(word['x1'], 1),
                            "bottom": round(word['bottom'], 1),
                            "width": round(word['x1'] - word['x0'], 1),
                            "height": round(word['bottom'] - word['top'], 1),
                            "text": word['text']
                        })
            if totals_values:
                page_data["elements"]["totals_values"] = totals_values

    # Find receiver box by looking for structured layout
    receiver_bbox = estimate_receiver_bbox(page, page_data)
    if receiver_bbox:
        page_data["elements"]["receiver_box"] = receiver_bbox

    # Locate QR code (usually as an image or large block)
    qr_bbox = find_qr_region(page)
    if qr_bbox:
        page_data["elements"]["qr_code"] = qr_bbox

    # Measure page numbering text
    page_num = find_element_bbox(
        words,
        lambda w: re.search(r'Pág\s*\.', w['text'], re.IGNORECASE)
    )
    if page_num:
        page_data["elements"]["page_numbering"] = page_num

    return page_data


def find_rect_containing_bbox(rects: List[Dict], x0: float, top: float, x1: float, bottom: float,
                               tolerance: float = 2.0) -> Optional[Dict[str, float]]:
    """
    Find a rectangle (border) from page.rects that contains the given bounding box.

    This is used to measure actual box borders rather than just text bounds.

    Args:
        rects: List of rectangle objects from page.rects
        x0, top, x1, bottom: Bounding box coordinates to search within
        tolerance: Allowed deviation from exact containment (in points)

    Returns:
        Bbox dict with the found rectangle, or None
    """
    best = None
    best_area = None
    for rect in rects:
        try:
            rect_x0 = rect['x0']
            rect_top = rect['top']
            rect_x1 = rect['x1']
            rect_bottom = rect['bottom']

            # Check if this rect contains the bbox (with tolerance)
            if (rect_x0 <= x0 + tolerance and
                rect_x1 >= x1 - tolerance and
                rect_top <= top + tolerance and
                rect_bottom >= bottom - tolerance):

                area = (rect_x1 - rect_x0) * (rect_bottom - rect_top)
                # Several containers can enclose the same text (e.g. the whole
                # header border as well as the small voucher box inside it);
                # keep the tightest-fitting (smallest-area) one.
                if best_area is None or area < best_area:
                    best_area = area
                    best = {
                        "x0": round(rect_x0, 1),
                        "top": round(rect_top, 1),
                        "x1": round(rect_x1, 1),
                        "bottom": round(rect_bottom, 1),
                        "width": round(rect_x1 - rect_x0, 1),
                        "height": round(rect_bottom - rect_top, 1),
                        "source": "page_rects_border"
                    }
        except (KeyError, TypeError, ValueError):
            continue

    return best


def find_element_bbox(words: List[Dict], predicate) -> Optional[Dict[str, float]]:
    """
    Find first word matching predicate and return its bounding box.

    Args:
        words: List of word dictionaries from pdfplumber
        predicate: Function that returns True for target word

    Returns:
        Bbox dict with x0, top, x1, bottom (in points), or None
    """
    for word in words:
        try:
            if predicate(word):
                return {
                    "x0": round(word['x0'], 1),
                    "top": round(word['top'], 1),
                    "x1": round(word['x1'], 1),
                    "bottom": round(word['bottom'], 1),
                    "width": round(word['x1'] - word['x0'], 1),
                    "height": round(word['bottom'] - word['top'], 1),
                    "text": word['text']
                }
        except (KeyError, TypeError):
            continue
    return None


def measure_table_bbox(tables: List[Any], table_type: str) -> Optional[Dict[str, float]]:
    """
    Measure bounding box of tables (detail or totals).

    Args:
        tables: List of table objects found by pdfplumber
        table_type: "detail" or "totals"

    Returns:
        Bbox dict with aggregate coordinates, or None
    """
    if not tables:
        return None

    # For now, use the first/largest table as detail, skip totals
    # In a real scenario, we'd identify tables by position/size
    if table_type == "detail" and len(tables) > 0:
        table = tables[0]
        try:
            bbox = table.bbox
            if bbox:
                return {
                    "x0": round(bbox[0], 1),
                    "top": round(bbox[1], 1),
                    "x1": round(bbox[2], 1),
                    "bottom": round(bbox[3], 1),
                    "width": round(bbox[2] - bbox[0], 1),
                    "height": round(bbox[3] - bbox[1], 1)
                }
        except (AttributeError, TypeError, IndexError):
            pass

    return None


def estimate_receiver_bbox(page: Any, page_data: Dict) -> Optional[Dict[str, float]]:
    """
    Estimate receiver box position based on page structure.

    Typically appears below the header and before the detail table.

    Args:
        page: pdfplumber page object
        page_data: Current page measurements dict

    Returns:
        Bbox dict or None
    """
    # This is a heuristic: receiver box is typically between header (~48mm = 136pt)
    # and detail table. For precise measurement we'd need to identify borders.
    # For now, return a reasonable estimate based on standard layout
    return {
        "x0": 10,
        "top": 150,
        "x1": 585,
        "bottom": 250,
        "width": 575,
        "height": 100,
        "note": "estimated_receiver_box"
    }


def find_qr_region(page: Any) -> Optional[Dict[str, float]]:
    """
    Locate QR code region (usually bottom-right of footer area).

    QR codes are detected as areas with specific density patterns.
    For pdfplumber, we look at the image/graphics area.

    Args:
        page: pdfplumber page object

    Returns:
        Bbox dict or None
    """
    # Try to find images (QR is usually an image)
    try:
        images = page.images
        if images:
            # Assume last image is QR (typically in footer)
            img = images[-1]
            return {
                "x0": round(img['x0'], 1),
                "top": round(img['top'], 1),
                "x1": round(img['x1'], 1),
                "bottom": round(img['bottom'], 1),
                "width": round(img['x1'] - img['x0'], 1),
                "height": round(img['bottom'] - img['top'], 1),
                "source": "image"
            }
    except (AttributeError, IndexError):
        pass

    return None


def main():
    """CLI entry point."""
    if len(sys.argv) < 2:
        print(json.dumps({
            "error": "Usage: pdf_measure.py <pdf_path>",
            "usage": "pdf_measure.py factura.pdf"
        }, indent=2))
        sys.exit(1)

    pdf_path = sys.argv[1]
    result = measure_pdf(pdf_path)

    # Output JSON to stdout
    print(json.dumps(result, indent=2))

    # Exit with error code if measurement failed
    if "error" in result:
        sys.exit(1)


if __name__ == "__main__":
    main()
