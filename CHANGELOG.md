# Changelog

Toutes les évolutions notables de Maquereaux. Format inspiré de [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/),
versions selon [SemVer](https://semver.org/lang/fr/).

## [0.0.1] - 2026-10-24

### Ajouté
- Socle du projet (J0) : solution `Maquereaux.sln` avec `Maquereaux.Core`, `Maquereaux.Win32`, `Maquereaux.App` (WPF)
  et deux projets de tests (`Maquereaux.Core.Tests`, `Maquereaux.Win32.Tests`).
- Fenêtre WPF « Maquereaux », icône 🐟, manifest DPI Per-Monitor V2.
- Mode `--smoke-test` (démarre puis se ferme seul) utilisé par la CI.
- CI GitHub Actions (Windows) : build, tests, publication single-file, smoke test de l'exe.
- Workflow de release : chaque tag `v*` publie `Maquereaux.exe` (autonome, win-x64) dans les Releases GitHub.
