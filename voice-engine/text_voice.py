"""Local multilingual synthesis with the selected voice reference. No user audio upload."""
import argparse, json, time, re
from pathlib import Path

MODEL_REVISION = "5bb1f6ee58e50c3b8d408bc82a6d3740c2db6e18"

def text_chunks(text, limit=180):
    """Bound each synthesis request without dropping words or punctuation."""
    chunks, current = [], ""
    for word in re.findall(r"\S+", text):
        if current and len(current) + len(word) + 1 > limit:
            chunks.append(current)
            current = ""
        current = (current + " " + word).strip()
        if len(current) >= 65 and re.search(r"[.!?。！？]$", word):
            chunks.append(current)
            current = ""
    if current:
        chunks.append(current)
    return chunks

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--request", required=True)
    args = parser.parse_args()
    request = json.loads(Path(args.request).read_text(encoding="utf-8-sig"))
    import torch, soundfile as sf, numpy as np
    from huggingface_hub import snapshot_download
    from chatterbox.mtl_tts import ChatterboxMultilingualTTS
    requested = request.get("device", "auto")
    device = "cuda" if requested != "cpu" and torch.cuda.is_available() else "cpu"
    if requested == "cuda" and device != "cuda":
        raise RuntimeError("CUDA is not available for Text to Voice.")
    torch.set_num_threads(max(1, min(16, int(request.get("threads", 4)))))
    torch.manual_seed(2026)
    checkpoint = snapshot_download("ResembleAI/chatterbox", revision=MODEL_REVISION,
        allow_patterns=["ve.pt", "t3_mtl23ls_v2.safetensors", "s3gen.pt",
            "grapheme_mtl_merged_expanded_v1.json", "conds.pt", "Cangjie5_TC.json"])
    model = ChatterboxMultilingualTTS.from_local(checkpoint, device)
    text = request["text"].strip()
    if not text or len(text) > 1000:
        raise ValueError("Text must contain between 1 and 1000 characters.")
    language = request["language"]
    if language not in model.get_supported_languages():
        raise ValueError("Unsupported language.")
    reference = request["reference"]
    if not Path(reference).is_file():
        raise ValueError("Select a prepared AI voice first.")
    started = time.perf_counter()
    chunks = text_chunks(text)
    rendered = []
    with torch.inference_mode():
        for index, chunk in enumerate(chunks):
            print(f"Text to Voice {index+1}/{len(chunks)}", flush=True)
            part = model.generate(chunk, language_id=language, audio_prompt_path=reference,
                exaggeration=max(0.0, min(1.0, float(request.get("expression", .5)))),
                cfg_weight=.5, temperature=.7).detach().cpu().numpy().reshape(-1)
            if not np.isfinite(part).all() or len(part) < model.sr / 4:
                raise RuntimeError("The model did not generate a valid phrase.")
            if rendered:
                rendered.append(np.zeros(int(model.sr * .15), dtype=part.dtype))
            rendered.append(part)
    audio = np.concatenate(rendered)
    if not np.isfinite(audio).all() or len(audio) < model.sr / 4:
        raise RuntimeError("The model did not generate valid audio.")
    sf.write(request["output"], audio, model.sr, subtype="PCM_24")
    result = {"device": device, "seconds": time.perf_counter()-started,
        "duration": len(audio)/model.sr, "peak": float(np.max(np.abs(audio))),
        "vram_mb": torch.cuda.max_memory_allocated()/1048576 if device == "cuda" else 0,
        "revision": MODEL_REVISION}
    Path(request["output"]+".json").write_text(json.dumps(result), encoding="utf-8")
    print(json.dumps(result), flush=True)

if __name__ == "__main__":
    main()
