"""Export the pinned Qwen3 tool model with ONNX Runtime GenAI's model builder."""

import subprocess
import sys
import shutil
from pathlib import Path

from huggingface_hub import snapshot_download

folder = Path(__file__).resolve().parent
source = folder / ".tool-model-source"
destination = folder / "Qwen3-0.6B"

snapshot_download(
    "Qwen/Qwen3-0.6B",
    revision="c1899de289a04d12100db370d81485cdf75e47ca",
    local_dir=source,
    allow_patterns=["*.json", "*.jinja", "*.safetensors", "*.txt", "LICENSE"],
)

subprocess.run(
    [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-i", str(source), "-o", str(destination),
        "-p", "int4", "-e", "cpu",
        "--extra_options", "algo_config=k_quant", "shared_embeddings=true",
    ],
    check=True,
)
shutil.copyfile(source / "LICENSE", destination / "LICENSE")

print(f"Tool model ready: {destination}")
