# MyVoice 2.3.0 Beta 1

Première Beta de la nouvelle interface. La version stable 2.2.0 reste le téléchargement par défaut.

- Nouvelle identité MV, fenêtre et dock repensés ; navigation Home, Voice, Soundboard, Community, Studio, Mic Setup, Profile et Settings.
- Quatre thèmes, accent personnalisable, mouvements réduits et zoom jusqu’à 150 %.
- Voice Designer : pitch, égalisation, saturation, écho et mélange ; presets locaux exportables.
- Favoris Soundboard, import/export de packages vérifiés et profil local avec avatar recadré.
- Mic Setup regroupe les périphériques et le traitement audio, avec aides et réinitialisations ciblées.
- Canal Beta volontaire, source officielle fixe, téléchargement vérifié et conservation des bibliothèques existantes.
- Site officiel GitHub Pages et backend communautaire préparé avec tests de permissions.

Les moteurs IA Direct, Conversation et Expressive sont conservés : cette Beta ne modifie pas leurs algorithmes. Les anciennes voix, références, sons et paramètres restent locaux. La migration ajoute un fichier de préférences et sauvegarde les métadonnées existantes.

Validation : 115 contrôles C#, 19 scénarios SQL locaux, huit pages Windows rendues, recadrage rond/carré, réglages persistants, raccourcis et zone de notification. Site vérifié sur formats ordinateur et mobile. Les tests SQL ne remplacent pas un essai sur un projet Supabase réel.

## Limites de cette Beta

- **Comptes et Community non activés** : aucun projet Supabase n’est encore connecté. L’audio local fonctionne sans ce service. Le backend et sa procédure de déploiement sont dans le dépôt.
- L’installation des mises à jour reste **manuelle** après téléchargement vérifié. L’application ne se remplace pas et ne se relance pas automatiquement.
- Les packages partagent Soundboards et Voice Presets. Les voix IA dépendant de références privées ne sont pas exportées automatiquement.
- Modération complète, suppression de compte, édition des publications et aperçu audio communautaire restent à compléter avant ouverture générale.
- Installateur non signé sous Windows ; VB-CABLE reste une installation depuis son site officiel si nécessaire. La désinstallation conserve les données utilisateur.

Les sauvegardes existantes doivent être conservées pendant l’essai. Merci de signaler les problèmes reproductibles avec la version et les étapes concernées, sans joindre vos enregistrements privés.
