# État de la Beta 2.7.0-beta.11

## Implémenté et vérifié localement

- Découverte des releases GitHub complètes, tri SemVer, isolation Stable/Beta, vérification SHA256 et mise à jour au démarrage.
- Catalogue de modèles épinglés et vérifiés, provision automatique, réparation, correction du calcul CPU et déchargement du moteur avec Default Clean.
- Nettoyage du microphone RNNoise embarqué, Off/Light/Balanced/Strong. Le son des soundboards reste indépendant. Le nettoyage spectral classique est évité quand RNNoise fonctionne.
- Dix thèmes, aperçu, flou Acrylic natif compatible Windows 11 et repli opaque. Animation et flou suspendus en arrière-plan.
- Cinq catégories de paramètres, capture de raccourcis 1–3 touches, protection des menus fermés contre la molette, libellés des objets correctement affichés.
- Réinitialisation du mélangeur conservant les éléments personnalisés, réglages de sons clavier, état Community « ajouté » synchronisé.
- OAuth Discord PKCE pour l’application et le site ; présence Discord facultative, sans noms de voix, fichiers ou données de compte.
- Bannières recadrables, disponibilité des identifiants, sessions renouvelées et déconnexion du site corrigée.
- Modification des métadonnées, couverture et aperçu des publications ; audio définitivement verrouillé après partage non privé. Remplacement possible seulement pour un contenu privé jamais partagé.
- Images Community chargées à l’écran, cache limité et réessai. Quatre packs illustrés, 37 bruitages originaux redistribuables, ajout sans écrasement de la bibliothèque.
- Synthèse réelle avec marqueurs d’expression et pause exacte. Sur la machine de développement, un test de 6,92 s a demandé 54,38 s de synthèse GPU ; ce résultat ne garantit pas une latence de 5–10 s.

## Serveur

Le fournisseur Discord et les migrations de profil/publication sont actifs. Les contrôles SQL locaux couvrent les droits, la confidentialité, les verrous audio et les changements concurrents. Les points d’entrée refusent les requêtes non authentifiées quand un compte est nécessaire.

## Limites restantes

- Le vrai pilote propriétaire My Voice nécessite la filière Microsoft et une signature valide. VB-CABLE reste le backend opérationnel ; aucun périphérique n’est simplement renommé pour simuler un pilote.
- Le consentement Discord complet avec le compte du propriétaire et les parcours multi-comptes sur plusieurs PC ne sont pas encore validés.
- L’installation de tous les modèles sur un PC Windows entièrement vierge reste à vérifier. Les contrôles de fichiers et les essais actuels utilisent la machine de développement.
- La ressemblance, l’expressivité et les respirations naturelles dépendent des modèles et références. Les tests techniques ne prouvent pas une identité vocale parfaite ; le benchmark perceptif comparatif reste à compléter.
- Windows 10, plusieurs cartes graphiques, 200 % DPI, Narrateur et les différentes conditions réseau nécessitent encore des essais matériels. Les exécutables ne sont pas signés Authenticode.

Les sons personnels, voix, références et identifiants privés ne font pas partie de la distribution.
