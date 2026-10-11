# Mixer — architecture et vérification, Beta 2.4

## Réutilisation

L’audit a trouvé un AudioGraph mono 48 kHz pour la voix, deux sorties stéréo (monitor/virtual), le décodage Soundboard, VoiceProcessor, PeakLimiter, les raccourcis Windows, les sauvegardes de profil et NAudio 2.2.1. Mixer étend ce graphe. Il ne démarre pas un second moteur microphone et ne modifie pas les modèles IA. Mic Setup garde le gain et le nettoyage du micro ; Mixer gère sources, niveaux et destinations.

AudioGraph produit des blocs de 480 frames / 10 ms. Le décodage Soundboard, l’ouverture des périphériques et l’écriture WAV ont chacun leur worker. Les callbacks alimentent des tampons bornés. MixerRouter échange des plans entre deux blocs et conserve l’état des traitements ; les gains, pan et routes évoluent progressivement. Une génération de périphériques suspend le plan précédent pendant une reconnexion pour ne pas l’envoyer aux nouveaux endpoints.

Le son est float32 stéréo 48 kHz dans les bus. Mono est dupliqué ; les layouts surround courants 3/4/5/6/8 canaux sont ramenés à deux canaux ; un endpoint multicanal non standard utilise sa première paire. Le rééchantillonnage linéaire garde sa phase entre paquets. Les références au format original et les limites restent explicites.

Le canal mic reçoit soit la voix traitée, soit le résultat IA. Ils ne sont jamais additionnés. Soundboard conserve les options Hear Myself / Send to Mic de chaque son : trois mélanges sont dérivés d’une lecture unique du décodeur. Master Mix est un bus de sommation indépendant ; ce n’est pas un deuxième réglage global du micro.

## Périphériques et capture

WASAPI partagé est la sortie recommandée. MME utilise WaveOutEvent ; ASIO utilise un driver présent et expose sa taille de buffer après initialisation. Aucun choix WDM/KS fictif n’est ajouté. Les entrées physiques et virtuelles utilisent WASAPI ; System Audio utilise le loopback d’un endpoint de rendu. ProcessAudioCapture appelle l’API Windows ActivateAudioInterfaceAsync et inclut les enfants du processus sélectionné. Aucun remplacement silencieux par la capture système n’est effectué en cas d’échec.

Les nouvelles captures sont arrêtées jusqu’à l’action explicite Capture. Un profil local peut mémoriser ce choix. Les imports/exportations coupent les captures, suppriment les identifiants locaux et placent les sorties en bus internes. La configuration d’un périphérique fixe absent est conservée. Les sources et sorties supplémentaires peuvent suivre le périphérique Windows par défaut ; le microphone principal est une sélection fixe dans Mic Setup.

RoutingSafety refuse le retour direct du loopback vers sa sortie et les cycles connus entre câbles virtuels. Les connexions matérielles externes et le routage effectué dans d’autres programmes ne sont pas observables. Les limiteurs protègent le niveau numérique, sans garantir un niveau d’écoute acoustique.

## Données et contrôles

`config/mixer.json` est additif et atomiquement remplacé après validation ; une sauvegarde précédente est conservée. Les profils ont 24 sources / 12 bus maximum ; 50 profils et 200 associations MIDI sont acceptés. Les anciennes bibliothèques ne sont pas migrées ni réécrites par MixerStore. Les données personnelles et les enregistrements ne font pas partie du dépôt ni de la release.

WAV : sélection explicite d’un fichier avec confirmation d’écrasement, FIFO bornée et écriture hors callback. Un disque trop lent arrête l’enregistrement avec un message. L’arrêt vide la queue et finalise l’entête. La limite WAV RIFF de 4 Go produit un arrêt explicite. Le micro avant traitement reste après le gain/mute de la capture existante.

Les commandes MIDI CC / Note On sont mises en file puis consommées sur le thread de contrôle. Les mappings visent un identifiant de canal, bus ou profil. Les raccourcis utilisent HotkeyService existant ; le raccourci Enregistrer ouvre également le choix de fichier. Aucun enregistrement n’est lancé silencieusement.

## Identité Windows

Le symbole vectoriel, son wordmark dessiné, les SVG clair/sombre/monochrome, PNG et ICO sont dans assets/brand et assets. MyVoice.Signal.ico est l’icône embarquée et celle des nouveaux raccourcis ; MyVoice.Tray.ico simplifie le tray. Les tailles ICO sont 16/20/24/32/40/48/64/128/256. L’exécutable et les raccourcis utilisent MyVoice.App comme AppUserModelID. Le nouveau nom de fichier évite de réutiliser l’ancien chemin d’icône lors d’une installation.

Les épingles manuellement créées avec une ancienne copie portable peuvent rester liées à cette copie : les retirer puis épingler la nouvelle application. Aucun cache Windows n’est effacé et Explorer n’est pas relancé par MyVoice.

## Vérification et limites

145 contrôles C# locaux : traitement antérieur, store, mises à jour, voix, routing, cycles, capture arrêtée, changements de périphérique, isolation Hear/Send, profils, validation d’import, conversion stéréo, MIDI borné et finalisation WAV. Le test de charge du mix de base est inférieur à 1 ms par bloc de 10 ms sur la machine d’essai ; ce n’est pas une garantie sur tout PC.

Tests matériels optionnels : `--mixer-hardware` isole un signal à 733 Hz dans un processus alors qu’un autre joue à 1231 Hz, vérifie le loopback système, un endpoint absent/relié et la fermeture des captures. `--virtual` vérifie le trajet Soundboard 44.1 kHz mono → graphe → CABLE Input → CABLE Output avec Hear désactivé. Ils utilisent des signaux synthétiques de faible amplitude.

Le smoke WPF utilise une copie isolée du profil et vérifie les neuf pages, les volumes au-delà des curseurs, les profils et la sélection visible de l’enregistrement. Les essais physiques ASIO/MIDI, une déconnexion USB réelle et les applications Discord/OBS/Teams/Zoom elles-mêmes nécessitent encore des essais manuels. Echo Cancellation, WDM/KS et FLAC/MP3 ne sont pas proposés. Le serveur communautaire n’est pas connecté.

Références : [API Microsoft de capture par application](https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nf-mmdeviceapi-activateaudiointerfaceasync), [exemple officiel ApplicationLoopback](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/ApplicationLoopback), [NAudio 2.2.1](https://github.com/naudio/NAudio/tree/v2.2.1).
