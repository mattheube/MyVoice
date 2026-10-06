# MyVoice 2.2.0

- Ajout de Conversation : timbre V2 en continu, contexte roulant, VAD, crossfade, anticipation et benchmark par voix. Direct et Expressive restent disponibles.
- Comparateur local : même phrase convertie dans les trois modes, avec écoute séparée.
- Mesures développeur : calcul, file, RTF, recouvrement, données écartées et temps capture → soumission à la sortie virtuelle.
- GitHub Releases préconfiguré, téléchargement HTTPS vérifié par SHA-256, cache de version et démarrage hors ligne.
- Voix, sons, raccourcis et paramètres restent locaux ; sauvegarde par version.

Validation locale : 87 contrôles déterministes, trois minutes de Conversation vers le câble virtuel sans perte ni retard cumulatif ; environ 1,26–1,33 s jusqu’à WASAPI sur RTX 4060 Ti. Discord et le réseau ne sont pas inclus ; la similarité perceptive n'est pas garantie.

L'installation automatique n'est pas livrée : le téléchargement vérifié s'ouvre dans son dossier pour installation manuelle. Pas de navigateur de packs dans cette version. Les binaires ne sont pas encore signés sous Windows.
