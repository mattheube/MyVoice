# Mises à jour automatiques — 2.6 Beta

Activer « Vérifier au démarrage » et « Mises à jour automatiques au démarrage ». La sélection Stable/Beta est conservée. Une vérification sans nouvelle version peut être réutilisée pendant six heures ; le bouton de recherche vérifie immédiatement. « Installer et redémarrer » applique une version préparée.

## Première installation

Les versions 2.5 et antérieures ne contiennent pas le programme de remplacement. Installer une première fois MyVoiceSetup.exe 2.6, ou extraire le ZIP complet dans un nouveau dossier. Les mises à jour suivantes au format officiel pourront être installées automatiquement. La stable 2.2.0 reste indépendante du canal Beta.

## Séquence

1. Lecture du manifeste officiel ; téléchargement HTTPS du ZIP avec progression en octets, délais bornés et contrôle SHA-256. L’application actuelle continue en cas d’échec réseau.
2. Copie de MyVoice.UpdateHost hors du dossier programme, dans `%LOCALAPPDATA%/MyVoice/Updates/transactions`. Un verrou empêche deux installations simultanées.
3. Vérification du processus d’origine et relecture du manifeste de la release GitHub exacte. Le programme séparé confirme la prise en charge avant que MyVoice se ferme.
4. Préparation dans un dossier voisin : copie des fichiers actuels et extraction du ZIP vérifié. Liens, traversées de dossiers, chemins ambigus et entrées dupliquées sont refusés. La version de l’assembly doit correspondre au manifeste.
5. Renommage du dossier actuel en sauvegarde, activation du dossier préparé et redémarrage.
6. La nouvelle interface confirme sa version et son démarrage par un jeton propre à la transaction. Sans confirmation sous deux minutes, le programme séparé arrête uniquement le processus qu’il vient de lancer, restaure l’ancienne version et la relance.

Une version dont l’installation a échoué n’est plus réessayée automatiquement ; le bouton d’installation permet un nouvel essai explicite. Journaux, résultat et chemins de sauvegarde sont conservés dans la transaction. Les sauvegardes programme sont des dossiers voisins `.myvoice-backup-*` ; elles ne sont pas supprimées automatiquement dans cette Beta.

## Données et limites

Le remplacement concerne le dossier programme. `%APPDATA%/MyVoice` (réglages, bibliothèques), `%LOCALAPPDATA%/MyVoice/AI` (moteur et modèles), datasets et médias externes restent en place. La sauvegarde des données par version reste active au démarrage normal. Les fichiers supplémentaires du dossier programme, notamment le désinstalleur, sont conservés.

Les installations par utilisateur et dossiers portables accessibles en écriture sont pris en charge, sans élévation. Un dossier protégé ou verrouillé peut empêcher l’installation. Le numéro affiché par MyVoice est actualisé ; l’entrée Windows « Applications installées » conserve le numéro du dernier installateur exécuté.

Il faut de l’espace pour le téléchargement, la préparation et la sauvegarde. Une coupure de courant pendant le renommage peut nécessiter une restauration manuelle depuis la sauvegarde indiquée par le journal. Le retour arrière automatique couvre les erreurs observées et l’échec de démarrage, pas tous les incidents système. Les binaires ne sont pas encore signés Authenticode ; les publications GitHub disposent d’attestations et les paquets sont contrôlés par SHA-256.

## Validation

Tests : téléchargement interrompu/tronqué, annulation, empreintes incorrectes, archives malveillantes, fichiers verrouillés, remplacement, restauration et préservation des fichiers locaux. `--smoke-test <dossier> --exercise-update` permet un essai explicite du parcours GitHub dans une copie isolée de l’application, avec profil temporaire ; ne pas le lancer dans l’installation utilisée au quotidien.
