# CLAUDE.md — Contexte projet Maquereaux 🐟

## En bref
Auto-presser de touches Windows (WPF, .NET 8) : séquences de touches en boucle jusqu'à ~1 ms, hotkeys globaux,
profils, timer haute précision, labo de mesure manuel vs auto. Cahier des charges : `docs/CAHIER_DES_CHARGES.md`.
**Hors périmètre non négociable** : anti-détection/humanisation, anti-cheat, lecture écran/OCR, farm AFK, réseau.

## Structure
- `src/Maquereaux.Core` — logique pure (net8.0), aucune dépendance WPF/Win32. Testable sous Linux.
- `src/Maquereaux.Win32` — P/Invoke (SendInput, hotkeys, hooks, timers). net8.0-windows.
- `src/Maquereaux.App` — WPF MVVM (CommunityToolkit.Mvvm). Exe : `Maquereaux.exe`.
- `tests/Maquereaux.Core.Tests` — xUnit, fakes (FakeClock, FakeInputSender…).
- `tests/Maquereaux.Win32.Tests` — intégration Win32, `[WindowsFact]` (skip hors Windows). Benchmarks : `[Trait("Category","Benchmark")]`.
- `build/` — scripts PowerShell CI (smoke test, notes de release).

## Commandes
```bash
dotnet build                                   # marche aussi sous Linux (EnableWindowsTargeting)
dotnet test --filter "Category!=Benchmark"     # tests (Win32 skippés hors Windows)
dotnet test tests/Maquereaux.Win32.Tests --filter "Category=Benchmark"   # benchmarks timers (Windows)
dotnet publish src/Maquereaux.App -c Release -p:PublishSingleFile=true -o publish
publish/Maquereaux.exe --smoke-test            # démarre puis se ferme (code 0 = OK)
```
Sous le conteneur Claude : `export PATH=/root/.dotnet:$PATH` (SDK installé via dotnet-install.sh).

## Conventions
- Commentaires en **français**, identifiants en **anglais**. `TreatWarningsAsErrors=true`.
- Commits `feat(Jx): ...`, tag `vX.Y.Z` par jalon → la CI publie la Release avec l'exe.
- Toute décision → `docs/DECISIONS.md` ; tout bonus → `docs/AMELIORATIONS.md` ; tests manuels → `docs/TESTS_MANUELS.md`.
- Variable d'env `MAQUEREAUX_DATA_DIR` : remplace `%AppData%\Maquereaux` (tests, smoke test).

## État des jalons
- [x] J0 — Socle (v0.0.1)
- [x] J1 — Moteur de base (v0.1.0)
- [ ] J2 — Timer haute précision (v0.2.0)
- [ ] J3 — Profils & activation (v0.3.0)
- [ ] J4 — Labo de mesure (v0.4.0) → v1.0.0
