# Refonte UI/UX v1.x — « la carrosserie » 🐟✨

De la v1.0 (fonctionnelle mais brute) à la v1.5 (identité « maquereau », design system complet, Windows 11 natif).
**Le moteur n'a pas bougé** : `Maquereaux.Core` et `Maquereaux.Win32` sont identiques fonctionnellement (seuls des champs de réglages
UI ont été ajoutés), les benchmarks le confirment (en bas de page).

Toutes les captures « après » sont produites automatiquement par le smoke test de la CI sur un runner Windows.

## Avant / après

| Avant (v1.0) | Après (v1.5) |
|---|---|
| ![Macro avant](ui/before/macro-running.png) | ![Macro après](ui/after/0-running.png) |
| **Macro en marche** : bandeau plat, tableau d'étapes, stats en ligne | Carte héros cliquable (halo turquoise, stats monospace, mini-courbe), séquence en keycaps |
| ![Profils avant](ui/before/profils.png) | ![Profils après](ui/after/4-profils-multiples.png) |
| **Profils** : liste texte | Cartes : raccourci en keycap, aperçu des touches, mode / intervalle / cible, actions au survol |
| ![Labo avant](ui/before/labo.png) | ![Labo après](ui/after/7a-labo-graphique.png) |
| **Labo** : tableau + nuage de points basique | Cartes par source, graphique avec cible, bande p5–p95, info-bulle, zoom, histogramme |
| ![Thème clair avant](ui/before/theme-clair.png) | ![Thème clair après](ui/after/0-running-light.png) |
| **Thème clair** : inversion simple | Thème « écume » à parité, contrastes AA calculés |
| ![Paramètres avant](ui/before/parametres.png) | ![Paramètres après](ui/after/dark-4-paramètres.png) |
| **Paramètres** : cases à cocher | Sections, interrupteurs, choix du timer en cartes avec jauges précision / CPU |
| ![Compact avant](ui/before/compact.png) | ![Compact après](ui/after/6-compact.png) |
| **Compact** : mini-fenêtre | Pastille flottante (opacité réglable, opaque au survol, double-clic pour revenir) |

### États clés

| | |
|---|---|
| ![Pause](ui/after/macro-pause.png) | ![Conseil](ui/after/macro-conseil-appui.png) |
| Pause automatique (fenêtre cible inactive) : tout passe à l'ambre | Conseil « Appliquer 16 ms à toute la séquence » quand la cible est probablement un jeu |
| ![Comparaison](ui/after/7d-labo-comparaison.png) | ![Histogramme](ui/after/7b-labo-histogramme.png) |
| Comparaison A/B avec verdict en une phrase | Histogramme des intervalles + ligne cible |
| ![3-2-1](ui/after/labo-compte-a-rebours.png) | ![Visite guidée](ui/after/onboarding-1.png) |
| Compte à rebours géant | Visite guidée (3 étapes, passable, rejouable) |
| ![Toast Annuler](ui/after/4b-profil-supprime-toast.png) | ![Ajout de touche](ui/after/macro-ajout-touche.png) |
| Suppression immédiate + toast « Annuler » | Tuile « + » en mode capture |

Design system complet (chaque contrôle dans chaque état, deux thèmes, 150 %) : [`DESIGN_SYSTEM.md`](DESIGN_SYSTEM.md).

## Jalons

| Jalon | Version | Contenu |
|---|---|---|
| U0 | — | Audit de la v1.0 ([`UI_AUDIT.md`](UI_AUDIT.md)) |
| U1 | 1.1.0 | Design system (tokens, polices Inter + JetBrains Mono, icônes Fluent, contrôles), coque Windows 11 (barre de titre, Mica, Snap Layouts, navigation latérale, pastille d'état), 1er jet de l'écran Macro |
| U2 + U3 | 1.2.0 | Fin de l'écran Macro (sélecteur de fenêtre cible), Profils en cartes, suppression annulable, Paramètres en sections |
| U4 | 1.3.0 | Labo : barre REC, cartes par source, nouveau graphique, verdict A/B, 3-2-1 géant |
| U5a | 1.4.0 | Pastille compacte, icône de notification aux couleurs d'état, visite guidée, nouvelle icône, contrastes AA |
| U5b | 1.5.0 | Passe accessibilité, documentation, galerie avant/après, captures du README |

## Boucle de validation visuelle

WPF ne tourne pas sous Linux (conteneur de dev) : chaque push sur `ui-revamp` déclenche la CI Windows, dont le smoke test
**capture chaque écran** (sombre et clair, 100 % et 150 %, taille minimale, sidebar repliée) et **chaque état clé** (en marche,
pause, conseil d'appui, capture de touche, toast, 3-2-1, histogramme, comparaison, visite guidée, compact) + la galerie du
design system. Les captures sont relues à chaque itération, critiquées sur une grille (alignements, espacements, contraste,
troncatures, cohérence des états, hiérarchie, parité des thèmes) puis corrigées. Exemples de défauts trouvés et corrigés ainsi :

- collision de clé `Text.Secondary` (style vs pinceau) qui faisait planter l'appli au premier affichage → détectée ensuite par `tools/check_xaml.py` ;
- icône Play invisible (un `Style` local écrasait le template) → `c:Icon` se dessine désormais sans template ;
- puces des keycaps tronquées, tuile « + » mal alignée, états en anglais dans la pastille ;
- profil actif perdu après une suppression (la liste liée remettait la sélection à null) ;
- voile du 3-2-1 trop transparent, tableau A/B trompeur quand une session est vide, accent du thème clair sous le seuil AA.

## Timing : identique à la v1.0

Benchmark CI (1 000 ticks par ligne, même runner `windows-latest`, vrai moteur) :

| Stratégie / intervalle | v1.0 — moyenne · p95 · σ | v1.x — moyenne · p95 · σ |
|---|---|---|
| Haute résolution, 10 ms | 10,000 · 10,343 · 0,251 | 10,000 · 10,306 · 0,229 |
| Hybride, 10 ms | 10,000 · 10,001 · 0,029 | 10,000 · 10,000 · 0,021 |
| Haute résolution, 5 ms | 5,000 · 5,550 · 0,247 | 5,001 · 5,437 · 0,236 |
| Hybride, 1 ms | 1,000 · 1,001 · 0,001 | 1,000 · 1,001 · 0,001 |
| Standard (référence), 10 ms | 10,004 · 23,357 · 7,267 | 10,004 · 21,186 · 7,240 |

Les écarts sont du bruit de mesure de la VM. La nouvelle interface ne touche pas au thread moteur : stats rafraîchies à
10 Hz, animations décoratives stoppées dès que la fenêtre est réduite/cachée ou que Windows désactive les animations.
