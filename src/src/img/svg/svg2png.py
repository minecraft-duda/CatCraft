#!/usr/bin/env python3
"""
SVG to PNG Converter
Usage: python svg2png.py <filename_without_extension>
Example: python svg2png.py logo
"""

import sys
import os
import cairosvg


def convert_svg_to_png(svg_path: str, png_path: str) -> None:
    """Convert SVG file to PNG using cairosvg."""
    try:
        cairosvg.svg2png(url=svg_path, write_to=png_path)
        print(f"✓ Converted: {svg_path} → {png_path}")
    except Exception as e:
        print(f"✗ Conversion failed for {svg_path}: {e}")
        sys.exit(1)


def main() -> None:
    # Get base filename from command line or user input
    if len(sys.argv) > 1:
        base_name = sys.argv[1]
    else:
        base_name = input("Enter SVG filename (without extension): ").strip()
        if not base_name:
            print("No filename provided. Exiting.")
            sys.exit(1)

    svg_file = base_name + ".svg"
    png_file = base_name + ".png"

    if not os.path.isfile(svg_file):
        print(f"Error: File '{svg_file}' not found in current directory.")
        sys.exit(1)

    convert_svg_to_png(svg_file, png_file)


if __name__ == "__main__":
    main()