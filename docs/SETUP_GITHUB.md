# ⚙️ GitHub : repo, CI et releases

Tout est déjà en place sur `urattems/Maquereaux-` : rien à faire pour que les releases fonctionnent.
Ce document explique comment ça marche et les quelques commandes utiles.

## Comment une release est publiée
Le workflow [`.github/workflows/release.yml`](../.github/workflows/release.yml) se déclenche :

1. **Automatiquement** à chaque push sur `main` : il lit `<Version>` dans `Directory.Build.props` ; si la release
   `vX.Y.Z` n'existe pas encore, il build, teste, publie `Maquereaux.exe` (single-file, self-contained, win-x64),
   lance le smoke test, crée le tag + la Release avec les notes tirées de `CHANGELOG.md` et l'empreinte SHA-256.
2. Sur **push d'un tag** `vX.Y.Z` (méthode classique).
3. **À la main** : onglet *Actions* → *Release* → *Run workflow* (option : version à republier).

Les versions `0.x` sont marquées *pre-release*, les versions ≥ 1.0 deviennent « Latest ».

### Publier une nouvelle version (ex. 1.1.0)
```bash
# 1. Ajouter une section "## [1.1.0] - AAAA-MM-JJ" en haut de CHANGELOG.md
# 2. Changer <Version>1.0.0</Version> → <Version>1.1.0</Version> dans Directory.Build.props
git commit -am "chore: release 1.1.0"
git push origin main          # la CI crée le tag v1.1.0 et la Release toute seule
```
Variante avec tag :
```bash
git tag v1.1.0 && git push origin v1.1.0
```

## Rendre le repo public (optionnel)
Le repo est privé par défaut. Pour partager les releases sans compte GitHub :
```bash
gh repo edit urattems/Maquereaux- --visibility public --accept-visibility-change-consequences
```
(ou *Settings* → *General* → *Danger Zone* → *Change visibility*).

## CI
[`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — à chaque push / PR, sur `windows-latest` :
- build Release (warnings = erreurs), tests unitaires + intégration Win32, résumé de couverture ;
- publication single-file + **smoke test** de l'exe (parcours de tous les onglets, moteur, labo) + captures d'écran
  (artefact `screenshots`) ;
- job **Benchmarks timers** (1 000 ticks par stratégie), tableau publié dans le résumé du run (non bloquant).
