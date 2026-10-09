# MyVoice 2.3 Beta

Application Windows WPF de transformation vocale locale et soundboard.


[Site officiel](https://mattheube.github.io/MyVoice/) · [Stable 2.2.0](https://github.com/mattheube/MyVoice/releases/tag/v2.2.0) · [Beta 2.3.0-beta.1](https://github.com/mattheube/MyVoice/releases/tag/v2.3.0-beta.1)

La Beta introduit une nouvelle identité MV, huit espaces, quatre thèmes, un dock global et Voice Designer. Les moteurs IA existants sont conservés. Voir les [notes et limites de la Beta](publishing/Release-notes.md).

**Community n’est pas encore en ligne.** L’interface locale, les migrations Supabase et la fonction de distribution de packages sont préparées. Le propriétaire doit connecter un projet avant d’activer les comptes, les suivis et les publications. [Procédure](backend/README.md).

## Trois modes IA

- **Direct** : moteur Seed-VC classique, pour privilégier la latence.
- **Conversation** : conversion continue du timbre V2, contexte roulant, détection de parole WebRTC, anticipation et crossfade. Le débit est mesuré au chargement de la voix ; un matériel trop lent est signalé, avec proposition de passer en Direct.
- **Expressive** : transfert de style V2, adapté aux fichiers et au traitement microphone par phrases.

Conversation ne reproduit pas la régénération autorégressive du style d'Expressive. Il utilise son encodeur HuBERT/ASTRAL, son vocodeur BigVGAN et une identité consolidée à partir des références, en conservant le rythme de l'entrée. L'amélioration perceptive dépend de la voix et doit être jugée à l'écoute.

Sur RTX 4060 Ti, un test synthétique continu de trois minutes a mesuré environ 1,26 à 1,33 seconde entre l'entrée dans le graphe audio et la soumission WASAPI au câble virtuel, sans perte de blocs. Cela exclut le délai matériel final, Discord et le réseau, et ne garantit pas les mêmes performances sur un autre PC. Le CPU peut ne pas tenir le temps réel.

## Installation

Télécharger `MyVoiceSetup.exe` depuis les [releases](https://github.com/mattheube/MyVoice/releases), ou extraire tout le ZIP portable. La signature Windows n'est pas encore configurée. Les modèles IA se téléchargent séparément lors de l'installation du moteur. Un ancien runtime peut nécessiter « Installer / réparer le moteur » pour ajouter WebRTC VAD.

Les paramètres, sons, voix et datasets restent dans le profil local Windows. GitHub ne reçoit pas ces données. Chaque version crée une sauvegarde des données avant leur chargement. Aucun modèle vocal personnel ni enregistrement n'est inclus dans les releases.

## Mises à jour

L'adresse officielle GitHub Releases est préconfigurée. Le téléchargement est vérifié par SHA-256 et ne remplace pas les bibliothèques utilisateur. La consultation réseau est limitée dans le temps ; l'application continue hors ligne. Les informations de version sont mises en cache six heures.

**Limite de cette version : l'installation et le redémarrage ne sont pas automatiques.** Une fois le fichier vérifié, « Afficher le téléchargement » ouvre son dossier ; l'installation reste manuelle. Le mécanisme autonome de remplacement n'a pas été livré.

Le dépôt prépare un workflow de build/test et de release en brouillon avec attestation. La publication stable doit porter un nouveau numéro de version ; les releases immuables ne sont pas remplacées. `soundpacks.json` réserve un catalogue officiel vide ; il n'y a pas encore de navigateur de packs.

## Développement

Windows x64, SDK .NET 10.0.401. `dotnet build MyVoice.sln -c Release`, puis `dotnet run --project tests/MyVoice.Tests -c Release`. Les tests ordinaires n'exigent aucun microphone ni modèle IA. Les tests matériels et IA sont explicitement optionnels.

Les adaptateurs Python sont fournis avec leur licence GPL-3.0. Les licences des modèles et des autres composants s'appliquent séparément ; voir `docs/Dependencies.md`. La publication du code n'ajoute pas une licence globale aux composants qui n'en possédaient pas.
