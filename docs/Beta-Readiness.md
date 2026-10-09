# État vérifié de la Beta 2.3

## Disponible localement

Nouvelle navigation, identité MV et dock ; thèmes, accents et mouvements ; profil local ; recadrage ; Mic Setup ; Voice Designer ; favoris Soundboard ; packages Soundboard et presets avec validation ; sélection Stable/Beta ; site GitHub Pages. Les trois moteurs IA restent ceux de 2.2.0.

Les données utilisateur ne sont pas livrées avec les binaires. `config/product.json` est additif ; une copie des anciens catalogues précède sa création. Les packages installés sont désormais inclus dans les sauvegardes de version.

## Préparé, sans service connecté

Authentification, session Windows DPAPI, profils privés, suivi/amis/blocage, rôles et badges serveur, visibilité des contenus et autorisation de téléchargement, publication de packages et signalements. Les règles PostgreSQL ont été testées dans PGlite ; email, Auth, Storage et Edge Function doivent encore être testés sur Supabase réel.

## À terminer avant ouverture générale

- Configuration Supabase, SMTP, confirmations/récupération et tests de bout en bout avec plusieurs comptes.
- Tableau de modération complet, suppression de compte et gestion des sessions.
- Modification/versionnement d’une publication existante, previews et métadonnées avancées.
- Format autonome de voix IA partageable sans dataset ni références d’origine.
- Favoris/recherche unifiés de toutes les catégories de voix, collections officielles illustrées et traduction intégrale des nouveaux écrans.
- Tests Windows sur plusieurs écrans/DPI, Narrateur et Snap Layouts ; codecs de covers WEBP à vérifier.
- Signature des binaires et mise à jour avec remplacement automatique (non livrée ; l’installation actuelle reste manuelle).

Ce fichier distingue une Beta distribuable d’un produit communautaire totalement ouvert. Aucun faux compte ni faux contenu n’est présenté comme réel.
