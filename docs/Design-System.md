# MyVoice / Product beta

La marque MV utilise deux tracés sans symbole IA. Le fichier SVG est la source vectorielle ; ICO contient des variantes 16, 24, 32, 48, 64, 128 et 256 px. La version monochrome utilise `currentColor`.

Palette Midnight : fond #101117, surface #191B25, relief #242735, bord #353948, texte #F3F1FA, secondaire #A1A5B8, accent #B7A5FF. Graphite, Deep Ocean et Warm Dark changent les couches du fond ; l’accent est personnalisable. Le texte sur accent s’adapte à sa luminosité.

Espacements : 4 / 8 / 12 / 18 / 24 / 32. Rayon : 8 pour contrôles compacts, 12 pour actions, 16 pour cartes, 24 pour dock et panneaux d’accueil. Taille d’action minimale habituelle : 36–44 px. Typographie : Segoe UI / Segoe UI Variable Display sur Windows, system-ui sur le site. Corps 13–15, titre de section 21–24, titre de page 30–38.

Les transitions de navigation et survol durent 160–180 ms, sans travail sur le fil audio. Full respecte le réglage Windows ; Reduced et Off désactivent les transitions de déplacement. Le site respecte `prefers-reduced-motion`. Les focus clavier restent visibles.

La fenêtre utilise WindowChrome, resize natif, zone de glissement et double-clic pour maximiser. La croix conserve la préférence fermer-vers-tray. Les comportements DPI et Snap doivent aussi être vérifiés sur un vrai poste multi-écran avant une stable.

Les covers sont chargées en arrière-plan et décodées à 384 px, avec cache borné. Sons : carrés arrondis. Voix et avatar : cercles. Les images source et recadrages précédents restent conservés.
