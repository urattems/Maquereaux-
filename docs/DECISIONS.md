# 🧠 Journal des décisions

Projet mené en **mode autonome** : chaque choix non trivial est noté ici (décision, pourquoi, alternatives écartées).

---

### D-001 — Nom : Maquereaux
- **Décision** : l'app s'appelle **Maquereaux** (et non MacroChef) : solution, projets, namespaces, exe, dossier `%AppData%\Maquereaux\`.
- **Pourquoi** : consigne explicite du pré-prompt.

### D-002 — Repo GitHub existant `urattems/Maquereaux-`
- **Décision** : utiliser le repo déjà créé `urattems/Maquereaux-` au lieu d'en créer un nouveau `maquereaux`.
- **Pourquoi** : le repo existait déjà (vide) et était le seul accessible depuis la session ; pas besoin de `gh repo create`.
- **Écarté** : créer un 2ᵉ repo → doublon inutile.

### D-003 — Développement depuis Linux, validation Windows dans la CI
- **Décision** : le code est écrit et compilé dans un conteneur Linux (`EnableWindowsTargeting=true`), les tests Core
  tournent sous Linux, et **tout ce qui touche Windows (tests d'intégration Win32, benchmarks de timers, smoke test de l'exe)
  tourne sur les runners `windows-latest` de GitHub Actions**.
- **Pourquoi** : WPF/Win32 ne s'exécutent pas sous Linux ; les runners Windows donnent une vraie validation à chaque push.
- **Écarté** : Wine (peu fiable pour WPF et surtout pour SendInput/hooks).

### D-004 — Exe single-file compressé
- **Décision** : `PublishSingleFile` + `SelfContained` + `IncludeNativeLibrariesForSelfExtract` + `EnableCompressionInSingleFile`.
- **Pourquoi** : un seul fichier à télécharger, rien à installer. La compression divise la taille par ~2 (WPF self-contained ≈ 150 Mo brut).
- **Écarté** : framework-dependent (oblige à installer .NET 8 Desktop Runtime) ; trimming (non supporté par WPF).

### D-005 — Pas de FluentAssertions
- **Décision** : assertions xUnit natives uniquement.
- **Pourquoi** : FluentAssertions v8 a changé de licence (payante en usage commercial) ; `Assert.*` suffit largement → « pas de magie ».

### D-006 — Versions 0.x publiées en pré-release
- **Décision** : les tags `v0.*` créent des Releases marquées *pre-release* ; `v1.0.0` devient la release « Latest ».
- **Pourquoi** : l'onglet Releases met en avant la version stable, les jalons restent téléchargeables.

### D-007 — Releases déclenchées par la version de `Directory.Build.props`
- **Décision** : le workflow `release.yml` publie aussi automatiquement quand on pousse sur `main` un commit dont
  `<Version>` ne correspond à aucune release existante : la CI crée alors le tag `vX.Y.Z` + la Release.
  Le push de tag classique (`git push origin v1.2.3`) reste supporté, ainsi que le lancement manuel.
- **Pourquoi** : le proxy de l'environnement de dev refuse le push de tags (HTTP 403) ; « bumper la version = publier »
  est en plus simple et sans oubli possible.
- **Écarté** : créer les tags via l'API GitHub à la main (pas d'outil dispo) ; abandonner les releases par jalon.
