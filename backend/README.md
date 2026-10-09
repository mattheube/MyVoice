# Déploiement communautaire

Cette partie est préparée mais n'est pas encore déployée : aucun projet Supabase n'a été fourni. Les tests SQL locaux ne remplacent pas les essais Auth et Storage sur un projet réel.

1. Créer un projet Supabase. Activer la confirmation email, configurer SMTP, les limites Auth, une longueur minimale de mot de passe de 10 caractères et la protection contre les mots de passe compromis si disponible.
2. Appliquer les migrations de `supabase/migrations/` dans l’ordre de leur nom (001 puis 002) dans un projet neuf. Le schéma ne contient aucun mot de passe administrateur.
3. Déployer `community-package` avec la CLI Supabase. Le service-role reste uniquement dans les secrets de la fonction. Définir `MYVOICE_SITE_ORIGIN=https://mattheube.github.io`.
4. Configurer les URL Auth autorisées : `https://mattheube.github.io/MyVoice/`. Tester confirmation email et récupération avec le site avant ouverture.
5. Copier l'URL du projet et sa clé publique publishable dans `community.public.json` à la racine du projet et dans `website/community.public.json`. Ne jamais utiliser une clé service-role ou secrète dans ces fichiers.
6. Créer le compte officiel via Auth puis, dans la console SQL administrateur, attribuer explicitement au bon UUID le username `myvoice`, `verified=true` et le rôle `official` dans `myvoice_private.roles`. Aucun utilisateur ne peut se donner ce rôle via l'API.
7. Exécuter les tests de non-régression SQL puis les scénarios réels avec trois comptes : public, privé, ami et bloqué. Tester Storage en accès direct : le bucket doit refuser toute lecture directe. Les liens autorisés par la fonction expirent après 60 secondes.

L'authentification utilise Supabase Auth et ses règles de stockage des mots de passe. Les sessions Windows sont chiffrées par DPAPI. Le site ne persiste pas les tokens en stockage navigateur.

Les rôles et suspensions sont administrés côté serveur. `moderate_content` vérifie le rôle avant retrait ou mise en avant. Le tableau de modération complet et la suppression autonome de compte restent à développer avant distribution générale.

Les packages Soundboard et Voice Preset sont acceptés. Les AI Voices actuelles de Seed-VC dépendent de références audio locales : les exporter automatiquement violerait la consigne de ne pas publier les références/datasets. Cette version n'exporte donc pas ces voix comme s'il s'agissait de modèles autonomes. Un format de modèle partageable compatible devra être défini et testé séparément.

Sources : [Auth](https://supabase.com/docs/guides/auth), [Row Level Security](https://supabase.com/docs/guides/database/postgres/row-level-security), [Storage](https://supabase.com/docs/guides/storage/security/access-control), [fonctions SQL](https://supabase.com/docs/guides/database/functions).
