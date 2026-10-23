"""Float32 content encoder adapter for the pinned Seed-VC CPU path."""
def adapt(semantic,torch):
    cells=dict(zip(semantic.__code__.co_freevars,(c.cell_contents for c in semantic.__closure__ or ())))
    model=cells.get("wav2vec_model")
    extractor=cells.get("wav2vec_feature_extractor")
    if model is None or extractor is None:
        raise RuntimeError("Version du moteur Direct incompatible avec l’adaptateur CPU. Réparez les moteurs.")
    model.float()
    @torch.inference_mode()
    def cpu_semantic(waves):
        inputs=extractor([wave.detach().float().cpu().numpy() for wave in waves],
            return_tensors="pt",return_attention_mask=True,padding=True,sampling_rate=16000)
        return model(inputs.input_values.float()).last_hidden_state.float()
    return cpu_semantic
