MyVoice 2.7.0-beta.5

Le démarrage du moteur vérifie maintenant Python, ses dépendances et le backend. S'ils manquent, MyVoice lance la réparation dans son environnement géré avant d'initialiser le modèle. Le bouton Installer / réparer utilise le même parcours. L'annulation arrête la réparation sans effacer les voix ni les modèles.

Correction du chemin de modules Windows PowerShell lors du lancement depuis l'application. Les archives uv et backend téléchargées sont vérifiées par SHA-256. Un environnement incomplet est sauvegardé avant reconstruction.

Validation : installation isolée depuis un dossier sans Python, auto-test VAD sur 50 trames silencieuses, deuxième démarrage sans réinstallation et modèle témoin préservé ; 197 tests C# ; compilation sans avertissement. Cela ne remplace pas une validation sur un PC Windows vierge ni une mesure de qualité des modèles.

Les améliorations Beta 4 du mixeur, de VB-CABLE, des menus et de Publish & Share restent incluses. Le prompt complet reste en cours : tableau de santé détaillé, pad XY, sessions et avatars en ligne, modèles IA partageables sans références brutes et pilote My Voice notamment ne sont pas terminés.
