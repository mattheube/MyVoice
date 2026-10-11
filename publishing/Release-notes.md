# MyVoice 2.4.0 Beta 1

Cette Beta ajoute un Mixer à l’application Windows. La version stable 2.2.0 reste le téléchargement par défaut.

- Mixer distinct de Mic Setup : sources physiques/virtuelles, applications Windows et audio système ; les nouvelles sources sont ajoutées à l’arrêt avec une commande Capture explicite.
- Volumes numériques, mute/solo, balance, EQ, filtre, compression, delay/reverb, niveaux peak/RMS et matrice vers Headphones, Virtual Mic, Stream, Recording et Master.
- Sorties WASAPI et MME ; ASIO proposé si un driver est installé. Limiteur sur chaque bus, détection des boucles et conservation des périphériques indisponibles.
- Enregistrement WAV 48 kHz stéréo float32, avec choix du fichier avant démarrage et écriture sur un worker dédié.
- Profils locaux : sauvegarde, chargement, duplication, renommage, suppression, import/export sans identifiants matériels. Raccourcis et MIDI Learn pour niveaux, mute/solo et profils.
- Nouveau symbole et wordmark MyVoice, icônes Windows à neuf tailles et variante tray. Le site officiel utilise cette identité.
- Recherche des périphériques en arrière-plan et correction du défilement horizontal des tranches.

Les voix, sons, références et réglages existants restent locaux. Le fichier Mixer est additif et récupère les anciens réglages de monitoring. Les moteurs IA Direct, Conversation et Expressive conservent leurs algorithmes.

Validation locale : 145 contrôles C#, capture Windows isolant deux applications, capture système, retour de périphérique, véritable trajet CABLE Input → CABLE Output et neuf pages WPF. Les profils, volumes, recadrages, raccourcis, zone de notification et persistance ont aussi été exercés dans une copie du profil utilisateur.

## Limites

- ASIO et contrôleur MIDI physique n’ont pas été testés sur matériel dédié. La file MIDI est testée avec des événements synthétiques.
- WAV uniquement ; arrêt explicite avant la limite RIFF de 4 Go. Pas d’annulation d’écho acoustique ni de WDM/KS dans cette Beta. Les entrées restent en WASAPI ; le choix MME/ASIO concerne les sorties.
- Capture par application : Windows build 20348 ou supérieur. Une application inactive reste indisponible jusqu’à sa reprise audio.
- L’estimation de tampon n’est pas une mesure de latence de bout en bout ; le matériel, les modèles IA et le réseau ajoutent leur délai.
- Community reste hors ligne tant qu’un projet Supabase n’est pas connecté.
- Les mises à jour officielles sont recherchées et téléchargées avec vérification d’intégrité. Leur installation reste manuelle. L’installateur n’est pas signé ; VB-CABLE s’installe depuis son site officiel.
