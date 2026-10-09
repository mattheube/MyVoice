# Refonte produit — analyse préalable

La version de départ est MyVoice 2.2.0, WPF / .NET 10, MVVM CommunityToolkit. Les 87 contrôles déterministes passent avant modification. Les captures des quatre écrans existants et une sauvegarde du profil sont conservées hors du dépôt public.

## Données et frontières

`%APPDATA%/MyVoice` contient config, soundboards/library.json, sounds, images (et originaux), voices, voice-models et datasets. `LocalStore` conserve les champs inconnus, `SoundboardService` maintient son historique, `DatasetService` gère les révisions. Les modèles partagés et Python restent dans `%LOCALAPPDATA%/MyVoice/AI`. Cette refonte ne remplace ni ce runtime ni les adaptateurs Direct / Conversation / Expressive.

Audio : capture WASAPI → nettoyage micro → effet classique ou conversion IA → mélange soundboard → casque et câble virtuel. Les hotkeys, la réduction dans la barre des tâches et le lancement Windows restent gérés par les services existants. La sélection automatique du câble existe déjà. Aucun pilote n'est redistribué.

## Déplacement des écrans

| Écran initial | Destination |
|---|---|
| Voice Changer : presets / AI Voices | Voice |
| Voice Changer : entrée / niveau / calibration | Mic Setup |
| Settings : périphériques / traitement / tests audio | Mic Setup |
| Soundboard : lecture et organisation | Soundboard |
| Création / import de boards | Studio → Soundboard Builder |
| Voice Lab / références / fichiers / modes IA | Studio → Voice Lab |
| Paramètres application / raccourcis / stockage | Settings |

Home, Profile et Community sont de nouvelles destinations. Aucun contenu personnel n'est publié automatiquement. Les fonctionnalités de compte doivent rester désactivées honnêtement en l'absence du backend.

## Migration

Le format audio et les bibliothèques existants sont conservés. Le nouvel état produit est un fichier séparé : thèmes, profil local, onboarding, favoris et créations classiques. Sa première création exige une sauvegarde des métadonnées existantes et une validation. Les données anciennes ne sont pas réécrites par cette migration.

## Distribution

GitHub gère les sources, releases, paquets officiels et le site statique. Supabase est prévu pour Auth, profils, relations sociales, permissions et stockage privé. Aucun projet Supabase n'est encore connecté. Il ne faut pas annoncer les comptes en ligne comme opérationnels avant déploiement et tests des règles serveur.

La version actuelle vérifie et télécharge les releases ; l’utilisateur lance l’installation. Le remplacement automatique de l’application reste hors du périmètre livré de cette Beta.
