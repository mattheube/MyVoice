# Audit et validation — 2.5.0 Beta 1

## Référence avant modification

Compilation Release 2.4.0 : zéro erreur et zéro avertissement. Sauvegarde complète du profil local et inventaire SHA-256 effectués avant édition. Les moteurs et datasets IA restent à leur emplacement existant et ne sont pas modifiés.

## Chaîne audio

MicrophoneService capture le microphone physique en WASAPI. AudioGraph traite des blocs de 480 échantillons à 48 kHz. L’IA remplace le canal vocal traité lorsqu’elle est active : elle n’est pas additionnée à une seconde copie de la voix. SoundboardService alimente séparément le Mixer et conserve Hear/Send par son. MixerRouter applique la matrice et le limiteur de chaque bus ; MixerDeviceManager ouvre les endpoints hors du thread de rendu.

Les anciens réglages audio résident dans config/config.json ; la matrice dans config/mixer.json. L’audit a identifié une divergence : le Mixer conservait un endpoint fixe de haut-parleurs déconnectés, alors que l’ancien réglage pointait vers VB-CABLE. Deux configurations ne doivent plus piloter séparément ce bus.

IVirtualAudioBackend et VBCableBackend résolvent maintenant la paire standard CABLE Input / CABLE Output. Un identifiant sauvegardé n’est accepté que s’il reste compatible. Les variantes 16 canaux et haut-parleurs sont exclues. Une paire absente reste sans sortie physique ; elle n’est jamais remplacée silencieusement par des enceintes. La récupération périodique réapplique la bonne sortie après reconnexion. Les réglages de volumes, mute et routes sont conservés. Connected nécessite aussi un statut de sortie ouverte dans le moteur.

Le futur driver MyVoice pourra implémenter la même interface ; aucune simulation de driver n’est exposée. La sélection du périphérique d’entrée dans Discord reste une configuration de Discord. [Fonctionnement officiel VB-CABLE](https://vb-audio.com/Cable/).

## Démarrage et mises à jour

Le précédent splash affichait des textes ponctuels ; le contrôle réseau utilisait Task.WhenAny et pouvait poursuivre le téléchargement après ouverture de la fenêtre principale. Le démarrage attend désormais une session de mise à jour bornée et annulable avant de construire cette fenêtre.

UpdateSession partage les étapes Checking, Downloading, Verifying, Current, Ready, Failed et Cancelled entre le démarrage et Settings. Le manifeste HTTPS a une échéance de cinq secondes ; le transfert possède une limite globale et une détection de blocage. La progression numérique ne provient que des octets reçus et d’une longueur connue. Le SHA-256 et l’en-tête Windows sont vérifiés avant exposition du fichier. Aucun téléchargement n’est exécuté par ce composant.

La source officielle reste mattheube/MyVoice : stable via GitHub Releases, bêta via l’index GitHub Pages construit depuis les releases. Le cache de vérification dure six heures, avec vérification manuelle possible. Aucun token personnel n’est embarqué. Les fichiers .part sont retirés après échec ; une ancienne version vérifiée reste intacte. Les notes s’ouvrent sur la page officielle.

**Limite de livraison : installation manuelle.** L’updater séparé, le remplacement atomique et le rollback demandés restent à réaliser ; aucun état Installing fictif n’est affiché. Le setup ne supprime pas les données AppData et aucun nouvel installateur de driver n’est exécuté automatiquement.

## Fenêtre et interface

Le dock occupait déjà une ligne dédiée. La cause de la coupure était la maximisation d’une fenêtre sans bordure. WM_GETMINMAXINFO emploie maintenant rcWork du moniteur courant. F11 utilise ses limites complètes, sans confondre cet état avec Maximize ; Échap et F11 restaurent la taille et l’état précédents. [Référence Microsoft](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-getminmaxinfo).

Le Mixer utilise des ScrollViewers horizontaux à tranches de largeur fixe dans un parent vertical. La molette est redirigée vers l’axe horizontal uniquement si la zone peut défiler, puis rendue au parent à sa limite. Les menus ouverts restent propriétaires de leur interaction. Le message horizontal Windows est pris en charge. Aucun réglage de volume n’est modifié par la molette.

Le jeu d’icônes de navigation est vectoriel, avec traits et proportions communs. Les anciens contrôles beige/gris (tooltip, slider, toggle, bouton rond) utilisent les ressources du thème. Six palettes, couleurs d’accent dérivées, densité et réduction de mouvements sont persistantes. Le splash reprend le même symbole vectoriel ; les ressources ICO du programme, setup, raccourcis et tray sont conservées.

Mic Setup expose les traitements réellement présents. Six presets modifient ces paramètres sans charger de modèle supplémentaire. L’annulation d’écho, le de-esser, Mica et la courbe EQ ne sont pas simulés.

## Tests et limites

- 156 tests déterministes : régression audio, sauvegardes, routage, backend absent/ambigu, anciens haut-parleurs, étapes de téléchargement, SHA invalide, annulation, aucune exécution du setup.
- Test physique : signal synthétique envoyé par WASAPI à CABLE Input et reçu par CABLE Output.
- WPF : neuf pages, menus et profils Mixer, volumes numériques, copie isolée des catalogues, recadrages, raccourcis et tray ; persistance Ocean/Compact ; paramètres du preset Distant Microphone.
- Fenêtre : comparaison des coordonnées natives avec rcWork et rcMonitor, sortie du plein écran vers fenêtre normale et maximisée.
- Démarrage : contrôle réel de la release officielle, lancement automatique du microphone dans un profil de test ; aucune attente du chargement complet de l’IA.
- À valider sur matériel supplémentaire : changements de moniteur à DPI différents, auto-hide taskbar, gestes de touchpad physique, ASIO et MIDI. Pas de test de réception dans un compte Discord distant.
- Pas d’installation propre exécutée automatiquement. Le cache d’icônes Windows et la désinstallation/réinstallation restent à valider par l’utilisateur.

Cette bêta ne prétend pas achever les 106 sections : les limitations ci-dessus restent explicites dans les notes de version.
