MyVoice 2.7.0-beta.6

Correction des menus clic droit : suppression de la colonne blanche du template Windows, icônes vectorielles de taille fixe, sous-menus lisibles, couleurs du thème et actions de retrait distinctes. Les commandes existantes sont conservées.

Ressources : pages créées à la demande, cache de pages avec expiration, lectures de durée audio différées, listes de voix virtualisées et cache LRU de miniatures limité à 64 Mo. Les décodages en attente sont annulés quand la carte disparaît. Les champs de profil non envoyés restent conservés.

Arrière-plan : les animations GIF, waveforms et rafraîchissements visuels inutiles sont suspendus. Le microphone, le mixeur, les raccourcis, les sons et le moteur IA actif continuent. Aucun modèle actif n'est déchargé et aucun GC forcé n'est ajouté.

Transport audio IA : suppression de deux tableaux temporaires par échange et réutilisation du tampon d'entrée Conversation. Le test ciblé mesure 35 % d'allocations en moins pour l'encodage/décodage float32, avec un signal identique bit pour bit. Cela ne signifie pas 35 % de RAM totale en moins.

Validation : 200 tests C#, navigation WPF, 600 sons et 600 voix, cache borné, formulaires conservés, menus rendus dans trois thèmes et quatre échelles (100/125/150/200 %). Le rapport mémoire donne les mesures brutes et leurs limites ; la mémoire totale reste dominée par les modèles IA. Le passage physique entre écrans DPI différents n'a pas été testé.

Audio en arrière-plan : essai final de trois minutes réussi sans perte, latence mesurée environ 1,25 s jusqu'à la soumission virtuelle. Un essai précédent a écarté un bloc ; la stabilité sous charge reste dépendante de la machine et du modèle. Le rapport conserve ces résultats.
