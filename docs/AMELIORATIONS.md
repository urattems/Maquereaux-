# 🚀 Améliorations au-delà du cahier des charges

Chaque ajout reste dans l'esprit du projet et respecte la section « Hors périmètre ».

| # | Amélioration | Justification |
|---|---|---|
| A-01 | Mode `--smoke-test` + smoke test automatique de l'exe publié en CI | Garantit à chaque push/tag que l'exe téléchargeable démarre sans crash. |
| A-02 | Empreinte SHA-256 jointe à chaque release | Permet de vérifier l'intégrité du `.exe` téléchargé. |
| A-03 | Test d'intégration Windows bout-en-bout (SendInput → RegisterHotKey / hook) | Prouve que les vraies touches partent, pas seulement les fakes. |
| A-04 | Captures d'écran automatiques à chaque CI (smoke test) | Vérifier visuellement l'UI sans PC Windows ; alimente le README. |
| A-05 | Signature `dwExtraInfo` sur chaque touche envoyée | Le Labo distingue « Maquereaux » de « autre logiciel injecté » (identification, pas masquage). |
| A-06 | Recalage après un gros retard | Pas de rafale de rattrapage après un gel du PC/une veille. |
| A-07 | Résolution du scan code selon la disposition de la fenêtre cible | Un profil créé en AZERTY tape les bonnes lettres en QWERTY. |
| A-08 | Raccourcis des profils suspendus pendant une capture de touche | F6 peut être capturé sans lancer la macro. |
| A-09 | Stratégie de timer affichée en direct + benchmarks à 1 et 5 ms | Comprendre jusqu'où descendre et à quel coût CPU. |
| A-10 | Garde-fou : arrêt automatique après N minutes | Respect du « Hors périmètre » (pas de farm AFK) + protection contre l'oubli. |
| A-11 | Thème clair, icône de notification à pastille d'état, mode compact déplaçable | Confort ; l'état reste visible même fenêtre réduite. |
| A-12 | Instance unique (relancer = ramener la fenêtre) | Évite les conflits de raccourcis entre deux instances. |
| A-13 | Validation en direct des profils (conflits, maintien dans la séquence, cadence intenable) | Les erreurs sont visibles avant de lancer la macro. |
| A-14 | Import qui n'écrase jamais (Ids + raccourcis en conflit neutralisés) | Importer un profil partagé ne casse rien. |
| A-15 | Logs fichier (14 jours) + bouton « Ouvrir le dossier » | Debug facile, données 100 % locales. |
| A-16 | Notification si Windows refuse des événements (SendInput partiel) | Diagnostic UIPI/bureau sécurisé. |
| A-17 | Durée d'appui (down → up) mesurée par source dans le Labo | Explique pourquoi un jeu peut rater des appuis de 0 ms. |
| A-18 | Comparaison A/B auto-remplie + écarts colorés + deux graphiques | « Manuel puis Maquereaux » → comparaison immédiate, sans clic. |
| A-19 | Sauvegarde/rechargement de sessions `.mqsession.json` | Comparer avec une session d'un autre jour. |
| A-20 | Indicateur ● REC aussi dans le bandeau principal | On sait qu'on est enregistré quel que soit l'onglet. |
| A-21 | Nommage automatique des sessions (Manuel / Maquereaux / Mixte / Vide) | Comparaisons lisibles sans renommer. |
| A-22 | Modifications du profil en cours appliquées à chaud | Plus besoin d'arrêter/relancer pour ajuster l'intervalle pendant un test. |
| A-23 | Position/taille de fenêtre mémorisées (avec garde-fou multi-écran) | Confort ; pas de fenêtre « perdue » hors écran. |
| A-24 | Contrôles (cases, radios, scrollbars) thémés | Cohérence visuelle en sombre comme en clair. |

### Refonte UI v1.x

| # | Bonus | Pourquoi |
|---|---|---|
| A-25 | Linter `tools/check_xaml.py` (StaticResource inter-dictionnaires) | Le crash le plus sournois de WPF détecté avant la CI. |
| A-26 | Smoke test qui sort immédiatement (code 4/5) et écrit la pile en cas de crash au démarrage | Plus de CI figée 120 s sur une boîte de dialogue Windows invisible. |
| A-27 | Galerie du design system capturée en CI (sombre, clair, 150 %) | Chaque contrôle dans chaque état, vérifiable à chaque push. |
| A-28 | Curseur d'intervalle logarithmique aimanté sur les presets | 1–20 ms occupe un tiers de la piste au lieu de 2 %. |
| A-29 | Annuler la dernière suppression de touche (lien « Annuler la suppression » + Ctrl+Z) | Une suppression par erreur se rattrape en un clic. |
| A-30 | Carte héros cliquable en entier, avec mini courbe et repère de la cible | Le bouton le plus important est aussi le plus gros. |
| A-31 | Halo pulsé coupé quand la fenêtre est réduite ou les animations Windows désactivées | Zéro coût de rendu en arrière-plan. |
| A-32 | Sélecteur de fenêtre cible avec icônes, titres de fenêtre et recherche | Fini les noms d'exe tapés de mémoire. |
| A-33 | Verdict A/B en une phrase dans le Labo | La conclusion sans lire le tableau. |
| A-34 | Zoom / défilement / info-bulle sur le graphique, mode histogramme | On voit exactement où un appui a dérapé. |
| A-35 | Pastille compacte : opacité réglable (clic droit / Paramètres), opaque au survol, double-clic pour revenir | Reste visible sans gêner. |
| A-36 | Visite guidée en 3 étapes, rejouable | Prise en main en 30 s. |
| A-37 | Contrastes AA calculés par script | Lisible pour tout le monde, dans les deux thèmes. |
| A-38 | Générateur d'icône multi-résolution depuis le logo | Icône nette de 16 à 256 px. |
| A-39 | Raccourcis Ctrl+1…4 (pages), Ctrl+N (nouveau profil), Ctrl+Z (annuler la suppression d'une touche), affichés dans les info-bulles de la navigation | Tout se pilote au clavier. |
| A-40 | Bascule automatique en « Maintenir » quand on choisit un bouton de souris, ON/OFF grisé | Impossible de créer une combinaison qui ne marcherait pas. |
| A-41 | Clics sur Maquereaux ignorés par le hook souris, relâchement toujours pris en compte | Pas de démarrage intempestif, jamais de macro « coincée ». |
