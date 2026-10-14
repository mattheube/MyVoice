MyVoice 2.7.0-beta.3

Conversation : réparation indépendante de WebRTC VAD dans le runtime contrôlé, vérification du silence avant chargement, détails des erreurs et correction du résultat de benchmark non sérialisable. Test réel de trois minutes sans perte audio dans un profil isolé.

Mixer : Mono, Solo limité au monitoring, exclusion persistante « Jamais vers le micro virtuel » prioritaire sur les routes, politique des nouvelles applications (Monitor only par défaut). Les règles existantes restent conservées. La capture d'une application est une copie : désactiver son Monitor si Windows l'écoute déjà pour éviter un doublage.

VB-CABLE : reconnaissance du périphérique réénuméré/localisé Haut-parleurs (2- VB-Audio Virtual Cable), sans sélectionner la sortie 16 canaux.

Community : connexion au serveur MyVoice, confirmation de sauvegarde du profil uniquement lorsque le serveur renvoie la ligne modifiée. Publish & Share devient une page dédiée ; accès depuis les sons et presets locaux possédés.

Interface : menus contextuels sans gouttière Windows blanche, accueil et logo accordés au thème, transition 180 ms, séparation des commandes du haut. Recherche des mises à jour à chaque démarrage, canal Stable/Beta conservé.

Validation : compilation sans avertissement ; 192 tests C# ; 22 tests SQL ; tests Python de protocole/VAD ; dix pages WPF et tests dropdown/brouillons/bibliothèque. Les données personnelles restent hors du dossier programme.

Le master n'est pas intégralement terminé : moteur complètement absent/installation sur PC vierge, health dashboard complet, fallback Requested/Active, réglages temporels gate/compression, EQ paramétrique de sortie, pad XY, annulation d'écho, audio réseau, macros, publication de modèles IA sans références brutes, sessions/avatars en ligne et pilote Windows propriétaire My Voice restent à développer ou valider. La réception réelle des emails Auth n'est pas validée. ASIO/MIDI nécessitent du matériel compatible. VB-CABLE reste le périphérique virtuel utilisé ; aucun faux renommage.
