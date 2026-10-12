# MyVoice 2.6.0 Beta 1

Cette version ajoute l’installation automatique des mises à jour officielles au démarrage, suivie du redémarrage de MyVoice. Le canal Stable/Beta reste réglable. La stable 2.2.0 reste le téléchargement par défaut.

- Téléchargement avec progression réelle et vérification SHA-256 du paquet officiel.
- Programme séparé pour préparer et remplacer les fichiers après la fermeture de MyVoice.
- Sauvegarde de l’ancienne version ; restauration et relance si la nouvelle application ne confirme pas son démarrage.
- Refus des archives incomplètes, des liens et chemins sortant du dossier prévu ; verrou contre deux installations simultanées.
- Échec réseau récupérable et nouvelle tentative manuelle dans les réglages.
- Réglages, sons, voix et moteur IA conservés dans leurs dossiers actuels.

**Une première installation de cette 2.6 est nécessaire depuis les versions précédentes**, qui ne possèdent pas le nouveau composant. Ensuite, les futures mises à jour compatibles s’installent au démarrage lorsque les options de recherche et d’installation automatique sont activées.

Validation : 177 tests C# réussis avant publication. Le programme conserve les sauvegardes et journaux pour diagnostic. Voir [fonctionnement et limites](https://github.com/mattheube/MyVoice/blob/v2.6.0-beta.1/docs/Automatic-Updates.md).

Les binaires ne sont pas encore signés Windows. Les dossiers protégés peuvent nécessiter une installation manuelle. Une coupure de courant peut nécessiter une restauration manuelle depuis la sauvegarde. Community reste hors ligne sans projet Supabase connecté. Les moteurs IA et le routage VB-CABLE de 2.5 sont conservés.
