# MyVoice 2.7.0 Beta 1

Corrections du routage VB-CABLE et des menus du mixeur, nouveau Soundboard et préparation des publications.

- Bouton Rétablir VB-CABLE : restaure les routes micro/sons et affiche le périphérique à sélectionner dans les autres applications.
- Menus déroulants : sélection conservée pendant un rafraîchissement ; la molette ne change plus un périphérique au passage.
- Cartes de sons jouables, menu contextuel, préécoute casque isolée du micro virtuel, collections mixtes et favoris indépendants.
- Éditeur de sons : découpage, fondus, normalisation, description, catégorie et tags ; fichier original conservé.
- GIF et WebP, recadrage animé, chargement différé et cache borné. Limites : 20 Mo, 16 mégapixels et 120 images par animation.
- Recherche Ctrl+K, historique des notifications, accès compte/aide/Discord, nouveaux formulaires français et anglais.
- Brouillon de publication persistant ; provenance conservée dans les packages. Backend préparé avec contrôles d’accès et détection des fichiers audio identiques appartenant à un autre compte.
- Chargement des voix sérialisé : un choix obsolète ne doit plus remplacer la dernière voix demandée. Aucun nouveau modèle IA ajouté.
- Mises à jour automatiques officielles conservées, avec sauvegarde et retour à l’ancienne version en cas d’échec.

Community et les comptes en ligne restent indisponibles sans projet Supabase connecté. Les formulaires permettent de préparer le profil et les brouillons sur le PC. Les contrôles de propriété par empreinte ne détectent pas un audio réencodé ou modifié.

Cette Beta livre les corrections et les phases décrites dans docs/Master-Progress.txt ; elle ne prétend pas terminer tous les points du master prompt. Les éléments encore partiels figurent dans docs/Master-Audit.txt.

Tests locaux : routage CABLE Input → CABLE Output réel, préécoute isolée, conservation des originaux, collections, menus ouverts pendant rafraîchissement, images GIF/WebP, 600 cartes supplémentaires et lancement des neuf pages. Installation automatique à valider avec le paquet GitHub construit pour cette release.
