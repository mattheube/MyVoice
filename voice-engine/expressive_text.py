"""Supported synthesis controls, interpreted locally and never spoken by the model."""
import re

def parse(text, expression=.5):
    if not text.strip() or len(text)>1000:
        raise ValueError("Le texte doit contenir entre 1 et 1000 caractères.")
    result=[]
    for part in re.split(r"(\[[^\[\]\r\n]*\])",text):
        if not part:continue
        if part=="[pause]":result.append(("pause",.35))
        elif part=="[long-pause]":result.append(("pause",.8))
        elif part=="[expressive]":expression=.8
        elif part=="[neutral]":expression=.5
        elif part.startswith("[") or "[" in part or "]" in part:
            raise ValueError("Cette indication n’est pas prise en charge. Utilisez les boutons Pause ou Expression.")
        elif part.strip():result.append(("text",(part.strip(),expression)))
    if not any(kind=="text" for kind,_ in result):
        raise ValueError("Ajoutez au moins une phrase à prononcer.")
    return result
