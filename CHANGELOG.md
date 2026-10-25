# Changelog

Toutes les évolutions notables de Maquereaux. Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/),
versions selon [SemVer](https://semver.org/lang/fr/).

## [1.5.0] - 2026-10-25

✨ « La carrosserie », étape finale : clavier, documentation et galerie. Moteur strictement inchangé (benchmarks identiques à la v1.0).

### Ajouté
- **Raccourcis clavier** : Ctrl+1…4 pour changer de page (affichés dans les info-bulles de la navigation), Ctrl+N pour un
  nouveau profil, Ctrl+Z pour annuler la suppression d'une touche (les champs texte gardent leur Ctrl+Z).
- **Galerie avant/après** de la refonte (`docs/UI_REVAMP.md`) avec la comparaison chiffrée des benchmarks v1.0 / v1.x,
  README illustré avec les nouvelles captures, `docs/TESTS_MANUELS.md` enrichi d'une section « Refonte UI » (15 points).

## [1.4.0] - 2026-10-25

🫧 « La carrosserie », étape 4 : pastille compacte, zone de notification, visite guidée, nouvelle icône. Moteur strictement inchangé.

### Ajouté
- **Pastille compacte** flottante : bouton rond Start/Stop, état + profil + raccourci, déplaçable, toujours au premier plan,
  **opacité réglable** (clic droit ou Paramètres, 30 → 100 %), redevient opaque au survol, **double-clic** pour revenir.
- **Icône de la zone de notification** aux couleurs d'état : turquoise en marche, ambre en pause, grise à l'arrêt ; textes en français.
- **Visite guidée** en 3 étapes au premier lancement (passable, Échap, rejouable depuis Paramètres → Général).
- **Nouvelle icône d'application** multi-résolution (16 → 256 px, version simplifiée en petit format), générée depuis le logo
  par `tools/gen_icon.py`.

### Accessibilité
- Contrastes **AA** vérifiés par calcul dans les deux thèmes : accent du thème clair assombri (`#077A70`), pause et textes
  tertiaires ajustés.

### Technique
- Suppression des anciennes clés de style v1.0 (toutes les vues sont refondues).
- Deux tests temps réel fiabilisés (médiane de 5 réveils au lieu d'une mesure unique, sensible aux préemptions du runner CI).

## [1.3.0] - 2026-10-25

🔬 « La carrosserie », étape 3 : le Labo. Moteur strictement inchangé.

### Ajouté
- **Barre d'enregistrement** : gros bouton Enregistrer / Arrêter / Annuler, point REC pulsé + chrono + compteur d'événements,
  options en interrupteurs (3-2-1, durée fixe avec champ numérique), rappel de confidentialité.
- **Compte à rebours géant** 3-2-1 en surimpression, chaque chiffre « tombe » (animation coupée si Windows désactive les animations).
- **Cartes de stats par source** (physique / Maquereaux / autre / toutes) : inputs/s en grand, appuis, intervalle moyen, p95, jitter, appui moyen.
- **Nouveau graphique** : ligne de l'intervalle cible, bande p5–p95, info-bulle au survol (valeur, source, instant),
  zoom Ctrl+molette autour du curseur, glisser pour défiler, double-clic pour tout revoir, mode histogramme.
- **Comparaison A/B** avec écarts colorés (turquoise = mieux, corail = moins bien) et **verdict en une phrase**
  (« Maquereaux est 23× plus régulier que Manuel (σ 0,25 ms contre 5,8 ms) et 12× plus rapide. »). Tableau masqué si une session est vide.
- États vides dessinés (aucune session, rien de sélectionné).

## [1.2.0] - 2026-10-25

🗂️ « La carrosserie », étape 2 : Profils, Paramètres et finitions de l'écran Macro. Moteur strictement inchangé.

### Ajouté
- **Profils en cartes** : nom renommable, raccourci en keycap, aperçu des touches en mini-keycaps (+N au-delà de 6), mode,
  intervalle et fenêtre cible en un coup d'œil, alertes intégrées, actions Éditer / Dupliquer / Exporter / Supprimer.
- **Suppression sans boîte de dialogue** : le profil disparaît tout de suite et un toast « Annuler » le remet à sa place
  (sans son raccourci s'il a été repris entre-temps).
- **Sélecteur de fenêtre cible** : liste des applis ouvertes avec leur icône et le titre de leur fenêtre, recherche instantanée,
  ↑↓ + Entrée, saisie libre d'un nom d'exe pour une appli pas encore lancée, « Toutes les fenêtres ».
- **Paramètres en sections** (Général, Raccourcis, Timing, Sécurité, Apparence, Avancé) avec interrupteurs ; choix du timer
  en cartes avec jauges précision / coût CPU.
- Écran Macro : curseur d'intervalle logarithmique aimanté sur les presets ; puces des keycaps sur deux lignes si besoin.

### Corrigé
- Après la suppression du profil actif, un autre profil redevient bien actif.

## [1.1.0] - 2026-10-25

🎨 « La carrosserie », étape 1 : nouveau design system et nouvelle coque Windows 11. Moteur strictement inchangé.

### Ajouté
- **Design system** : tokens (couleurs, grille 4/8 px, rayons, ombres, durées), thème sombre « eaux profondes » et thème
  clair « écume » à parité, dégradé signature bleu-vert → argent → turquoise, couleurs d'état (marche turquoise, pause ambre,
  erreur corail). Galerie complète capturée en CI (`docs/DESIGN_SYSTEM.md`).
- **Polices embarquées** Inter (interface) et JetBrains Mono (tous les chiffres qui bougent), licence SIL OFL.
- **Icônes** Fluent UI System Icons (MIT) en vectoriel, une seule famille, zéro emoji.
- **Contrôles restylés dans tous leurs états** : boutons (primaire, secondaire, fantôme, danger, icône, puce), champs avec
  placeholder et état d'erreur, `NumericBox` maison (molette, ↑↓, Maj ×10, saisie tolérante « 16 ms »), interrupteur,
  contrôle segmenté, curseur, listes, menus, info-bulles, barres de défilement fines.
- **Coque Windows 11** : barre de titre maison, Mica (Windows 11 22H2+), coins arrondis, Snap Layouts conservés,
  navigation latérale repliable (Macro · Profils · Labo · Paramètres), pastille d'état globale cliquable, transitions de page.
- **Premier jet du nouvel écran Macro** : carte héros cliquable (Start/Stop, halo pulsé, stats en direct en monospace),
  séquence en « keycaps » (glisser-déposer animé, Suppr, Ctrl+D, Alt+←/→, double-clic pour changer la touche, tuile « + »
  en mode capture), puces durée d'appui / délai avec mini-éditeur, conseil « Appliquer 16 ms à toute la séquence »,
  cadence avec curseur logarithmique + presets 10·16·33·50·100 ms + « = N inputs/s », contrôles segmentés expliqués.
- Notifications en toasts empilés (bas droite) avec action optionnelle et fermeture automatique.
- Nouveau projet de tests `Maquereaux.App.Tests` (logique d'écran et ViewModels).

### Technique
- Animations décoratives coupées quand la fenêtre est réduite/cachée ou que Windows désactive les animations.
- Smoke test : crash au démarrage rapporté immédiatement (pile complète) au lieu d'un blocage de 120 s.
- `tools/check_xaml.py` : détecte les `StaticResource` inter-dictionnaires qui font planter WPF au chargement.

## [1.0.0] - 2026-10-24

🐟 Première version stable : les 4 jalons du cahier des charges (J1 moteur, J2 timer haute précision, J3 profils,
J4 labo) sont livrés, testés en CI sur Windows et documentés.

### Ajouté
- Les modifications du profil **en cours d'exécution** sont appliquées à chaud (relance transparente du moteur, anti-rebond 300 ms).
- Position et taille de la fenêtre mémorisées (ignorées si l'écran correspondant a disparu).
- Cases à cocher, boutons radio et barres de défilement aux couleurs du thème.
- README complet (installation, utilisation, captures, tableau des stratégies de timer, labo, limites connues),
  `docs/TESTS_MANUELS.md`, `docs/SETUP_GITHUB.md`.

### Qualité
- ~200 tests unitaires sur `Maquereaux.Core` (couverture CI : **98 % des lignes, 91 % des branches**) et ~20 tests
  d'intégration Windows sur `Maquereaux.Win32` (93 % des lignes) exécutés sur un vrai runner.
- Smoke test de l'exe publié à chaque push : tous les onglets, moteur réel, édition à chaud, profils + persistance,
  thème clair, mode compact, session du Labo à 10 ms, export CSV, captures d'écran.

## [0.4.0] - 2026-10-24

### Ajouté (J4 — labo de mesure)
- **Onglet Labo** : sessions d'enregistrement Start/Stop, compte à rebours 3-2-1 optionnel, durée fixe optionnelle (20 s par défaut).
- Capture via le hook clavier : horodatage haute résolution (`Stopwatch`) de chaque down/up, touche, **source**
  (physique / Maquereaux via la signature `dwExtraInfo` / autre logiciel injecté via `LLKHF_INJECTED`),
  répétitions automatiques du clavier détectées et exclues des « inputs ».
- **Résultats** : inputs total et par touche, inputs/s, intervalle moyen/min/max/p95/jitter et **durée d'appui** par source.
- **Graphique** des intervalles dans le temps (contrôle WPF maison, aucune dépendance), une couleur par source.
- **Comparaison de 2 sessions** côte à côte (tableau A/B + écarts colorés + deux graphiques), remplie automatiquement
  avec la session précédente.
- **Export CSV** lisible dans Excel (séparateur et décimale selon la langue de Windows, UTF-8 avec BOM).
- **Note libre** par session ; sauvegarde/rechargement explicites d'une session (`.mqsession.json`).
- **Confidentialité** : capture uniquement pendant une session lancée à la main, indicateur **● REC** dans le Labo et dans
  le bandeau principal, hook retiré à la fin de la session, données en mémoire, aucun envoi réseau.
- Tests : stats par source, CSV (en-têtes, format FR/invariant, BOM, échappement), arrêt à la fin de la durée fixe,
  répétitions, comparaison, sauvegarde/rechargement ; smoke test CI : session réelle à 10 ms → 200 inputs en 2 s (100/s),
  intervalle moyen 9,998 ms (cohérent avec le panneau J2).

### Corrigé
- Thème clair : le bandeau d'état restait sombre (couleurs d'état désormais en DataTriggers + DynamicResource).

## [0.3.0] - 2026-10-24

### Ajouté (J3 — profils et modes d'activation)
- **Onglet Profils** : créer, renommer (clic sur le nom), dupliquer, supprimer (avec confirmation), choisir le profil actif ;
  pastille verte sur le profil en cours ; problèmes de configuration affichés sous chaque profil.
- **Raccourci par profil** (F6 = profil 1, F7 = profil 2…), un seul profil actif à la fois ; un raccourci lance son profil et le sélectionne.
- **Mode « Maintenir »** : la macro tourne uniquement tant que la touche est enfoncée, via un hook `WH_KEYBOARD_LL`
  ultra léger (callback = horodatage + décision + file ; traitement sur un autre thread). Événements injectés ignorés
  (pas d'auto-déclenchement), répétition automatique ignorée, touche de maintien avalée.
- **Filtre de fenêtre cible** : liste des applis ouvertes (ou saisie libre), la macro passe en PAUSE dès que l'appli cible perd le focus
  et reprend dès qu'elle le retrouve (sans rafale de rattrapage).
- **Import / export JSON** (un profil ou tous) ; l'import n'écrase jamais (Ids et raccourcis en conflit régénérés/retirés).
- **Sauvegarde automatique** dans `%AppData%\Maquereaux\profiles.json` + `settings.json`, écriture **atomique**
  (fichier temporaire puis remplacement), anti-rebond 600 ms, sauvegarde à la fermeture.
- **Validation au chargement** : JSON corrompu → copie `.corrupt-….bak` + profil/réglages par défaut, sans crash, avec notification.
- Validation en direct : séquence vide, raccourci en double ou égal à l'arrêt d'urgence, touche de maintien dans la séquence,
  durées d'appui incompatibles avec la cadence.
- **Détection UIPI** : avertissement si l'appli au premier plan tourne en administrateur ; bouton « Relancer en administrateur ».
- **Zone de notification** : icône avec pastille d'état (vert/orange/rouge), menu profil / Démarrer-Arrêter / urgence / Quitter.
- **Mode compact** : mini-fenêtre déplaçable, toujours au premier plan (état + profil + START/STOP).
- **Thème clair** optionnel (changement à chaud), option « réduire dans la zone de notification ».
- **Garde-fou** : arrêt automatique après N minutes (60 par défaut, 1–240).
- **Instance unique** : relancer l'exe ramène simplement la fenêtre existante au premier plan.
- Tests : persistance aller-retour, JSON corrompu → backup, import/export, validation, logique Hold (fake + vrai hook),
  filtre process (focus perdu → pause → reprise), hook WH_KEYBOARD_LL réel (flags injecté/signature, avaler une touche),
  mesure 10 ms moteur → hook.

## [0.2.0] - 2026-10-24

### Ajouté (J2 — timer haute précision)
- Abstraction `ISleeper` avec 3 stratégies sélectionnables dans **Paramètres → Précision du timer** :
  - `Standard` (Thread.Sleep, référence imprécise),
  - `HighResolution` : waitable timer `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` (repli `timeBeginPeriod(1)` + `timeEndPeriod(1)` garanti),
  - `Hybrid` : haute résolution jusqu'à 1 ms de l'échéance puis spin-wait sur le Stopwatch.
- Le sleeper est créé à chaque démarrage et **toujours disposé à l'arrêt** (try/finally) → aucune résolution système « oubliée ».
- `TimingStats` (moyenne, min, max, p50/p95/p99 par interpolation type Excel, jitter = écart-type, échéances manquées > 1,5×)
  et `TimingRecorder` (tampon circulaire 10 000 intervalles, thread-safe).
- **Panneau de stats en direct** sous l'éditeur : ticks, fréquence, moyenne, min, max, p95, p99, jitter, manqués, timer utilisé.
- Avertissement CPU quand le mode Hybride est choisi.
- Benchmarks (`Category=Benchmark`) : 1 000 ticks par stratégie via le vrai moteur, + mesures à 5 ms et 1 ms,
  exécutés dans un job CI Windows dédié qui publie le tableau (voir README).

### Résultats (runner GitHub Windows, 1 000 ticks à 10 ms)
- HighResolution : moyenne **10,000 ms**, p95 **10,338 ms**, jitter 0,25 ms, CPU 0,5 %.
- Hybrid : moyenne **10,000 ms**, p95 **10,001 ms**, jitter 0,12 ms, CPU 4,7 %.
- Standard : moyenne 10,003 ms mais p95 19,2 ms et 467 échéances manquées (rattrapage permanent).

## [0.1.0] - 2026-10-24

### Ajouté (J1 — moteur de base « ça spamme »)
- `MacroEngine` : thread dédié (AboveNormal), cadence **absolue** (t0 + Σ délais, aucune dérive cumulée),
  recalage après un gros retard (pas de rafale de rattrapage), annulation par `CancellationToken`.
- Modes **Cycle** (une touche par tick) et **Burst** (toute la séquence par tick, en un seul appel `SendInput`),
  `RepeatCount` (0 = infini), durée d'appui et délai par étape.
- Anti « touche bloquée » : key-up garanti dans un `finally` + filets `AppDomain.UnhandledException` / `DispatcherUnhandledException`.
- `SendInputSender` : envoi en **scan codes** résolus selon la disposition clavier de la fenêtre cible
  (AZERTY/QWERTY), touches étendues (flèches, Inser…), repli VK pour Pause ; signature dans `dwExtraInfo`.
- Hotkeys globaux (`RegisterHotKey`) sur un thread Win32 dédié : **F6** ON/OFF, **Ctrl+Alt+Pause** arrêt d'urgence
  toujours actif (variante VK_CANCEL gérée), message clair en cas de conflit.
- UI Macro : bandeau d'état géant RUNNING/STOPPED, éditeur de séquence (capture « clique puis appuie »,
  réordonner, supprimer), intervalle, mode, répétitions, raccourci ; thème sombre ; « toujours au premier plan ».
- Raccourcis des profils suspendus pendant la capture d'une touche (F6 capturable sans lancer la macro).
- Logs fichier dans `%AppData%\Maquereaux\logs` (14 jours).
- Tests : moteur (Cycle AZEAZEA, Burst, RepeatCount, key-up à l'arrêt, cadence absolue, erreurs…),
  activation, et **test d'intégration Windows bout-en-bout** (SendInput → RegisterHotKey reçu).
- Smoke test CI : parcours des onglets, captures d'écran, démarrage réel du moteur.

## [0.0.1] - 2026-10-24

### Ajouté
- Socle du projet (J0) : solution `Maquereaux.sln` avec `Maquereaux.Core`, `Maquereaux.Win32`, `Maquereaux.App` (WPF)
  et deux projets de tests (`Maquereaux.Core.Tests`, `Maquereaux.Win32.Tests`).
- Fenêtre WPF « Maquereaux », icône 🐟, manifest DPI Per-Monitor V2.
- Mode `--smoke-test` (démarre puis se ferme seul) utilisé par la CI.
- CI GitHub Actions (Windows) : build, tests, publication single-file, smoke test de l'exe.
- Workflow de release : chaque tag `v*` publie `Maquereaux.exe` (autonome, win-x64) dans les Releases GitHub.
