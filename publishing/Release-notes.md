# MyVoice 2.5.0 Beta 1

Cette bêta corrige le routage du microphone virtuel et améliore le démarrage et l’interface Windows. La stable 2.2.0 reste le téléchargement par défaut.

- Virtual Microphone : détection automatique de la paire VB-CABLE standard, réparation d’une ancienne sortie vers les haut-parleurs, reconnexion et diagnostics. Le bus virtuel est géré par MyVoice ; aucun périphérique physique n’est choisi à sa place. Le statut connecté dépend de la sortie réellement ouverte.
- Démarrage : logo vectoriel, thème conservé, spinner et progression réelle du téléchargement, vérification d’intégrité, possibilité de continuer avec la version actuelle, récupération après échec. L’IA se prépare en arrière-plan.
- Fenêtre : maximisation dans la zone de travail Windows, F11 et Échap, restauration de la taille et de l’état précédents, boutons redessinés.
- Mixer : défilement à la molette dans les zones horizontales, événements horizontaux Windows, menus déroulants et contrôles accordés au thème.
- Mic Setup : sections Input, Cleanup, Tone, Dynamics, Monitoring et Advanced ; six préréglages réels, aide et remise à zéro par section.
- Apparence : six thèmes, accents dérivés, densité Compact/Comfortable, icônes de navigation vectorielles cohérentes, tooltips, sliders et boutons thématisés.

Les sons, voix, modèles, comptes locaux, raccourcis et réglages restent hors du dossier programme. Une sauvegarde de version précède le chargement de la bibliothèque. Les algorithmes IA sont conservés.

## Vérification

156 tests C# ; neuf pages WPF avec copie isolée des réglages ; limites physiques de fenêtre maximisée et plein écran ; retour à la taille précédente ; préréglages réels ; sauvegarde du thème et de la densité. La paire VB-CABLE est testée sur Windows avec un signal synthétique.

## Limites explicites

- Les mises à jour sont recherchées, téléchargées et vérifiées depuis les releases officielles. **L’installation reste manuelle** ; remplacement automatique et rollback ne sont pas livrés.
- Le driver MyVoice propriétaire n’est pas développé. L’interface IVirtualAudioBackend prépare son intégration ; seul VB-CABLE standard est actuellement pris en charge automatiquement. VB-CABLE doit être installé depuis son site officiel.
- Les transitions entre écrans de DPI différents et le touchpad physique nécessitent encore une validation matérielle. Les notes de version s’ouvrent sur GitHub.
- Pas de Mica, annulation d’écho acoustique ou de-esser ajouté. ASIO et MIDI n’ont pas de nouveau test matériel. Community reste hors ligne sans projet Supabase connecté. L’installateur n’est pas signé.
