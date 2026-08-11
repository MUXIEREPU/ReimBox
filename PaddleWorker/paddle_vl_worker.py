"""Local PaddleOCR‑VL 1.6 bridge for the ReimBox desktop app."""
from __future__ import annotations

import argparse
import json
import os
from pathlib import Path

# Prefer Paddle's BOS host for predictable local model downloads; skip host probing.
os.environ.setdefault("PADDLE_PDX_MODEL_SOURCE", "bos")
os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")

from paddleocr import PaddleOCRVL


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--device", default="auto")
    args = parser.parse_args()

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)
    options = {
        "pipeline_version": "v1.6",
        "use_doc_orientation_classify": True,
        "use_doc_unwarping": True,
        "use_layout_detection": True,
    }
    if args.device.lower() != "auto":
        options["device"] = args.device

    pipeline = PaddleOCRVL(**options)
    for index, result in enumerate(pipeline.predict(input=args.input)):
        (output_dir / f"page_{index:03}.json").write_text(
            json.dumps(result.json, ensure_ascii=False, indent=2), encoding="utf-8"
        )


if __name__ == "__main__":
    main()
