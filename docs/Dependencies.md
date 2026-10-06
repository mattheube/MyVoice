# Dépendances et médias — 2.2.0

- .NET / WPF : Microsoft ; notices du runtime conservées dans le portable.
- CommunityToolkit.Mvvm 8.4.0 : MIT, https://github.com/CommunityToolkit/dotnet
- NAudio 2.2.1 : MIT, https://github.com/naudio/NAudio
- Seed-VC : GPL-3.0, commit 51383efd921027683c89e5348211d93ff12ac2a8, https://github.com/Plachtaa/seed-vc/tree/51383efd921027683c89e5348211d93ff12ac2a8. Dépôt amont archivé ; version épinglée testée sur Windows.
- Modèle tiny : Plachta/Seed-VC, DiT_uvit_tat_xlsr_ema.pth ; licence affichée GPL-3.0 : https://huggingface.co/Plachta/Seed-VC.
- Dépendances/modèles auxiliaires téléchargés par le moteur : CAMPPlus, CosyVoice HIFT, facebook/wav2vec2-xls-r-300m. Sources : https://huggingface.co/funasr/campplus ; https://huggingface.co/FunAudioLLM/CosyVoice-300M ; https://huggingface.co/facebook/wav2vec2-xls-r-300m. Leurs notices restent applicables séparément.
- Python 3.11.15, PyTorch/torchaudio 2.4.1 CUDA 12.4 et dépendances verrouillées : voir voice-engine/requirements-lock.txt. uv 0.11.25 installe l'environnement séparé dans le profil utilisateur.
- Inno Setup 6.7.3 sert à construire l'installateur ; aucune dépendance au compilateur sur le PC destinataire.

Le worker Python MyVoice est fourni sous GPL-3.0 pour son intégration Seed-VC ; son source et la licence accompagnent le portable. Seed-VC et les modèles ne sont pas intégrés dans l'installateur : ils sont téléchargés par SetupEngine et lors du premier démarrage. Sources exactes et versions figurent dans le script et le lockfile.

Les six sons WAV (Airhorn, Click, Pulse, Rise, Signal, Success) ont été synthétisés pour ce projet. Le logo est une composition vectorielle originale de barres audio. Aucun asset Voicemod ou son tiers n'est fourni. Aucun pilote VB-CABLE n'est redistribué.

L'ensemble est livré pour usage personnel avec sources. Avant une redistribution commerciale, examiner les obligations des dépendances et modèles ; ce document n'affirme pas qu'ils ont tous une licence uniforme.

## Ajouts du moteur Studio 2.2.0

Le code V2 provient du même commit Seed-VC épinglé. L'adaptateur `voice-engine/studio.py` est fourni en source sous GPL-3.0-only, comme le worker historique. Les checkpoints sont téléchargés séparément, sans être inclus dans l'installateur :

- Seed-VC V2 : `v2/cfm_small.pth` et `v2/ar_base.pth`, https://huggingface.co/Plachta/Seed-VC.
- Quantification ASTRAL : `bsq32/bsq32_light.pth`, `bsq2048/bsq2048_light.pth`, https://huggingface.co/Plachta/ASTRAL-quantization.
- Extracteur HuBERT : https://huggingface.co/facebook/hubert-large-ll60k.
- Vocodeur : https://huggingface.co/nvidia/bigvgan_v2_22khz_80band_256x.
- CAMPPlus et tokenizer Whisper chargés par les modules amont ; leurs notices et celles des modèles précédents restent distinctes.

Hydra/OmegaConf construisent les modules depuis la configuration amont V2. Les dépendances sont déjà verrouillées dans `requirements-lock.txt`. La conversion MyVoice collecte le PCM directement ; elle ne requiert pas un encodage MP3 intermédiaire par ffmpeg.

Conversation utilise WebRTC VAD via webrtcvad-wheels 2.0.14 (https://pypi.org/project/webrtcvad-wheels/2.0.14/). Le module Python et le code WebRTC conservent leurs licences séparées (voir les notices du paquet). Aucun modèle personnel ne figure dans les assets publics.