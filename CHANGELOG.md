# Changelog

Toutes les évolutions notables de Maquereaux. Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/),
versions selon [SemVer](https://semver.org/lang/fr/).

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
