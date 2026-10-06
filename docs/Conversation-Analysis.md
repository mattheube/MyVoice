# Analyse avant implémentation : Direct / Expressive / Conversation

État inspecté : MyVoice 2.1.0, Seed-VC commit 51383efd921027683c89e5348211d93ff12ac2a8.

| Élément | Direct existant | Expressive existant |
|---|---|---|
| Modèle | V1 tiny XLSR, DiT 25 M | V2 CFM 67 M + AR 90 M pour transfert de style |
| Audio | 22 050 Hz interne, entrée/sortie 48 kHz | mêmes fréquences |
| Entrée | blocs 0,5 ou 1 s | phrases 2–8 s, pauses 700 ms |
| Contexte | 1 s gauche, pas de futur explicite | phrase complète, référence jusqu'à 25 s |
| Encodage | XLSR-large continu | HuBERT-large ll60k + quantification ASTRAL large ; étroite + AR pour le style |
| F0 | aucun conditionnement F0 explicite | aucun conditionnement F0 explicite ; YIN sert seulement à choisir une référence |
| Locuteur | CAMPPlus sur un prompt historique de 12 s | moyenne CAMPPlus de plusieurs références |
| Vocodeur | CosyVoice HiFT | BigVGAN V2 22 kHz |
| Étapes | 6 / 12 / 24 | 20 / 30 / 40 |
| Échantillonnage | diffusion, CFG similarity × 1,2, mélange dry/wet | AR temp 0,7, top-p 0,9, répétition 1,1 ; CFM similarity séparée |
| Jonctions | maintien/crossfade 20 ms | fondu de bord 10 ms, durée variable conservée |
| Capture | continue GPU, par phrases CPU | suspendue pendant calcul et lecture |
| Post-traitement | enveloppe dynamique, pré-nettoyage et post-EQ | enveloppe seulement sans transfert de style ; pré/post communs |

Le transfert de style AR peut modifier le nombre de tokens et la durée. Le découper naïvement en phrases indépendantes provoque des reprises et dérives temporelles : il n'est pas retenu pour Conversation. Les gains perceptifs d'Expressive ne sont pas attribuables à un seul facteur sans écoute comparative ; les différences vérifiables sont l'encodeur, le vocodeur, l'identité multi-références, l'AR et le contexte.

Piste à mesurer avant sélection : rendu V2 de timbre, sans AR, avec référence/identité pré-calculées, contexte acoustique roulant, futur borné et recouvrement. Il conserve les modules HuBERT/ASTRAL/BigVGAN d'Expressive et le rythme d'entrée. Ce n'est ni Direct avec davantage d'étapes, ni un transfert AR complet présenté comme temps réel. L'encodeur n'est pas causal : la fenêtre glissante explicite borne son futur ; aucun cache phonétique/F0 natif inexistant ne sera annoncé.

Benchmark prévu : plusieurs tailles de bloc, contextes, recouvrements, futurs et nombres d'étapes sur audio synthétique identique ; noyaux préchauffés. RTF, horodatage d'entrée et de sortie du mixeur, retards et drops suivis sur plusieurs minutes. Si le débit ne tient pas, afficher la limite et proposer Direct. L'adaptation est réservée à Conversation ; Expressive conserve la qualité choisie.

Références primaires : https://github.com/Plachtaa/seed-vc (table des modèles et paramètres V2), modules/v2/vc_wrapper.py, real-time-gui.py et configs/v2/vc_wrapper.yaml du commit local épinglé. Alternatives recherchées : StreamVC et StreamVoice (articles), Zero-VC (publication récente) ; leur existence ne suffit pas à garantir une implémentation Windows exploitable. La décision d'intégration doit reposer sur du code et des poids disponibles, puis des mesures locales.
