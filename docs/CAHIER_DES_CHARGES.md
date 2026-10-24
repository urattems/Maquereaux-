> 🐟 **Note** : l'application a été renommée **Maquereaux** (voir `docs/DECISIONS.md`, D-001).
> Ce document est conservé tel quel ; partout où il est écrit « MacroChef », lire « Maquereaux ».
> Le projet est mené en **mode autonome** (pas de validation intermédiaire), seule la section
> « Hors périmètre » reste non négociable.

# 🧑‍🍳 MASTER PROMPT — MacroChef v1.0 (auto-presser Windows)

> **À coller dans Claude Code au démarrage du projet.**
> Ce document est à la fois le **cahier des charges** et le **mode d'emploi** pour Claude Code.
> Copie-le aussi à la racine du repo sous `docs/CAHIER_DES_CHARGES.md`.

---

## 0. 🎭 Ton rôle et ta façon de travailler (Claude Code)

Tu es un **développeur senior C#/.NET spécialisé Windows desktop (WPF, Win32 interop)**.
Tu construis **MacroChef**, une application Windows d'envoi automatique de séquences de touches, pour un développeur passionné (intermédiaire en tech, à l'aise avec C#/WPF grâce à un projet précédent, pas développeur pro).

### Règles de travail (OBLIGATOIRES)
1. **Un jalon à la fois.** Tu ne commences JAMAIS le jalon N+1 tant que le jalon N n'a pas été validé explicitement par l'utilisateur.
2. **Plan d'abord.** Au début de chaque jalon : présente un plan court (fichiers créés/modifiés, choix techniques, risques), puis attends le feu vert.
3. **Fin de jalon :**
   - tous les critères d'acceptation cochés ✅
   - `dotnet build` sans warning bloquant, `dotnet test` vert
   - une **checklist de test manuel** donnée à l'utilisateur (ce qu'il doit cliquer/vérifier lui-même)
   - un commit git propre (`feat(J1): ...`) + un tag (`v0.1.0`, etc.)
   - mise à jour de `CHANGELOG.md` et de `CLAUDE.md`
4. **Commentaires du code en français**, noms de classes/méthodes/variables en anglais.
5. **Explique le pourquoi.** Pour chaque choix non trivial (P/Invoke, timer, threading), explique en 2-3 phrases pourquoi, comme à un pote qui apprend.
6. **Pas de magie.** Pas de dépendance exotique si 20 lignes de code suffisent. Chaque package NuGet ajouté doit être justifié.
7. **Si tu bloques ou hésites** entre deux approches, tu demandes au lieu d'inventer.
8. Crée et maintiens un fichier `CLAUDE.md` à la racine (contexte projet, commandes, conventions, état des jalons) pour les sessions suivantes.

---

## 1. 🎯 Contexte et objectif

**MacroChef** = un **auto-presser de touches** configurable :
- envoie une **séquence de touches** (ex. `A → Z → E`) en boucle
- à un **intervalle réglable**, jusqu'à **10 ms réels**
- s'active/se désactive via un **raccourci global**
- gère plusieurs **profils**
- intègre un **outil de mesure** pour vérifier le timing réel et comparer manuel vs automatique

Usage prévu : apprentissage (Win32, timers, threading, MVVM) + tests personnels de timing d'inputs.

---

## 2. 🚫 Hors périmètre (NE PAS IMPLÉMENTER)

Ces fonctionnalités sont **explicitement exclues**. Si l'utilisateur ou une suggestion les demande, tu refuses poliment et tu rappelles cette section :

- ❌ Toute fonction **anti-détection** : randomisation/« humanisation » des délais pour paraître humain, masquage du flag d'injection, pilote noyau, émulation matérielle (Interception, Arduino HID, etc.)
- ❌ Contournement d'anti-cheat, injection dans un processus, lecture/écriture mémoire d'un jeu
- ❌ Lecture de l'écran / pixel detection / OCR pour décider des actions (= bot)
- ❌ Automatisation longue durée sans présence (farm AFK)
- ❌ Toute communication réseau (l'app est 100 % locale, aucune télémétrie)

> Note : afficher le **jitter mesuré** (J2/J4) est OK, c'est de la mesure. **Ajouter** volontairement du jitter ne l'est pas.

---

## 3. 🏗️ Stack et architecture

| Élément | Choix | Pourquoi |
|---|---|---|
| Runtime | **.NET 8** (LTS) | Moderne, support long |
| UI | **WPF** | Natif Windows, déjà connu de l'utilisateur |
| Pattern | **MVVM** avec `CommunityToolkit.Mvvm` | Sépare UI et logique, testable |
| Envoi des touches | **`SendInput`** (user32) en **scan codes** (`KEYEVENTF_SCANCODE`) | Méthode standard Windows ; beaucoup d'applis (jeux notamment) lisent les scan codes plutôt que les virtual keys |
| Hotkeys globaux | `RegisterHotKey` (J1), puis hook clavier bas niveau `WH_KEYBOARD_LL` (J3 pour le mode « maintenir ») | `RegisterHotKey` est simple et fiable ; le hook est nécessaire pour détecter le relâchement d'une touche |
| Timer | J1 : `Stopwatch` + thread dédié ; J2 : timer haute résolution (voir J2) | `System.Timers.Timer`/`DispatcherTimer` sont limités à ~15,6 ms → interdits pour le moteur |
| Persistance | JSON (`System.Text.Json`) dans `%AppData%\MacroChef\` | Lisible, exportable |
| Logs | `Microsoft.Extensions.Logging` + fichier simple (ou Serilog si justifié) | Debug |
| Tests | **xUnit** (+ FluentAssertions optionnel) | Standard .NET |

### Arborescence cible
```text
MacroChef/
├─ CLAUDE.md
├─ CHANGELOG.md
├─ README.md
├─ docs/
│  └─ CAHIER_DES_CHARGES.md
├─ src/
│  ├─ MacroChef.Core/          # Logique pure, AUCUNE dépendance WPF
│  │  ├─ Models/               # Profile, MacroStep, RunMode, Settings…
│  │  ├─ Engine/               # MacroEngine, IScheduler, ISleeper
│  │  ├─ Input/                # IInputSender (abstraction)
│  │  └─ Stats/                # TimingStats (moyenne, p95, jitter…)
│  ├─ MacroChef.Win32/         # P/Invoke : SendInput, RegisterHotKey, hooks, timers
│  └─ MacroChef.App/           # WPF : Views, ViewModels, ressources
└─ tests/
   └─ MacroChef.Core.Tests/
```

**Règle d'or :** `MacroChef.Core` ne référence ni WPF ni Win32 → tout le moteur est testable avec des fakes (`FakeInputSender`, `FakeClock`).

---

## 4. 📦 Modèle de données

```text
MacroStep
  - Key            : identifiant de touche (VK + scan code)
  - HoldMs         : durée appui (down → up), défaut 0–5 ms
  - DelayAfterMs   : délai avant l'étape suivante (défaut = intervalle global)

Profile
  - Id, Name
  - Steps          : liste ordonnée de MacroStep
  - IntervalMs     : intervalle global (min 1, défaut 10)
  - SequenceMode   : Cycle (A→Z→E→A…) | Burst (A,Z,E envoyés à chaque tick)
  - RepeatCount    : 0 = infini, sinon N cycles
  - ActivationMode : Toggle | Hold      (Hold = J3)
  - Hotkey         : combinaison (ex. F6)
  - TargetProcess  : nom de process optionnel (J3)

AppSettings
  - EmergencyStopHotkey (défaut : Ctrl+Alt+Pause)
  - ActiveProfileId
  - HighPrecisionTimer (bool, J2)
  - Theme (Dark par défaut)
```

---

## 5. 🗺️ Jalons

### 🔹 J0 — Socle du projet (`v0.0.1`)
**Objectif :** un squelette propre qui compile.
- Solution + 3 projets + projet de tests (arborescence §3)
- `CLAUDE.md`, `README.md`, `CHANGELOG.md`, `.gitignore` .NET
- Fenêtre WPF vide avec le titre « MacroChef »
- `dotnet build` et `dotnet test` (1 test bidon) verts

✅ **Acceptation :** l'appli s'ouvre, les tests passent, premier commit fait.

---

### 🔹 J1 — Moteur de base « ça spamme » (`v0.1.0`)
**Objectif :** envoyer une séquence de touches en boucle à intervalle réglable, ON/OFF par hotkey.

**Fonctionnalités**
- Éditeur de séquence dans l'UI : ajouter / supprimer / réordonner des touches (capture : « clique puis appuie sur la touche »)
- Champ **intervalle (ms)**, min 1, défaut 10
- Choix du mode **Cycle** ou **Burst**
- **RepeatCount** (0 = infini)
- **Hotkey ON/OFF** global (défaut F6), modifiable
- **Arrêt d'urgence** global (Ctrl+Alt+Pause) qui coupe tout, toujours actif
- Indicateur d'état visible : 🟢 RUNNING / 🔴 STOPPED

**Spécifications techniques**
- `MacroEngine` tourne sur un **thread dédié** (`Thread`, `IsBackground = true`, priorité `AboveNormal`), jamais sur le thread UI
- Boucle cadencée par `Stopwatch` (cadence absolue : on vise `t0 + n × intervalle`, on ne cumule PAS les dérives)
- À l'arrêt (normal, urgence, fermeture appli, exception) : **envoyer un key-up pour toute touche restée enfoncée** (anti « touche bloquée »)
- `IInputSender` dans Core, implémentation `SendInputSender` dans Win32 (scan codes, gestion des touches étendues : flèches, Insert, etc.)
- Annulation propre via `CancellationToken`
- Si `RegisterHotKey` échoue (conflit), message clair à l'utilisateur

**Tests unitaires (avec fakes)**
- Mode Cycle : la séquence A,Z,E sur 7 ticks donne A,Z,E,A,Z,E,A
- Mode Burst : chaque tick envoie A,Z,E dans l'ordre
- RepeatCount = 2 → s'arrête après 2 cycles
- Stop → tout key-down a son key-up

✅ **Acceptation**
- Dans le Bloc-notes, la séquence `a z e` à 100 ms s'écrit correctement en boucle
- F6 lance/arrête, Ctrl+Alt+Pause arrête toujours
- Fermer l'appli pendant l'exécution ne laisse aucune touche bloquée
- L'UI reste fluide pendant l'exécution

> ⚠️ Attendu et normal à ce stade : à 10 ms demandés, le timing réel peut tourner autour de 15 ms. C'est le sujet de J2.

---

### 🔹 J2 — Timer haute précision (`v0.2.0`)
**Objectif :** tenir réellement 10 ms (et descendre à quelques ms) de façon stable, et le **prouver par la mesure**.

**Pourquoi :** par défaut, l'horloge système de Windows « tique » environ toutes les 15,6 ms, donc un `Thread.Sleep(10)` dure souvent ~15 ms.

**Fonctionnalités**
- Abstraction `ISleeper` / `IScheduler` avec **3 stratégies** sélectionnables dans les paramètres :
  1. `StandardSleeper` : `Thread.Sleep` (référence, imprécis)
  2. `HighResWaitableTimer` : `CreateWaitableTimerExW` avec `CREATE_WAITABLE_TIMER_HIGH_RESOLUTION` (Windows 10 1803+), fallback sur `timeBeginPeriod(1)` si indisponible
  3. `HybridSleeper` : attente haute résolution jusqu'à ~1 ms de l'échéance, puis **spin-wait** court (`SpinWait`/boucle sur `Stopwatch`) pour la fin
- Si `timeBeginPeriod(1)` est utilisé : **toujours** appeler `timeEndPeriod(1)` à l'arrêt (try/finally)
- **Panneau de stats en direct** pendant l'exécution :
  - intervalle moyen, min, max
  - **p95 / p99**
  - **jitter** (écart-type)
  - nombre d'échéances manquées (> intervalle × 1,5)
- Indication CPU : avertir dans l'UI que le mode Hybrid consomme plus de CPU

**Tests**
- `TimingStats` : calculs vérifiés sur des jeux de données connus
- Test de bench (catégorie `Benchmark`, exclu du CI par défaut) : 1 000 ticks à 10 ms → log des stats pour chaque stratégie

✅ **Acceptation**
- Avec `HighResWaitableTimer` ou `Hybrid` à 10 ms : **moyenne entre 9,8 et 10,2 ms, p95 ≤ 11 ms** sur 1 000 ticks (machine au repos)
- Le tableau comparatif des 3 stratégies est affiché dans le README

---

### 🔹 J3 — Profils et modes d'activation (`v0.3.0`)
**Objectif :** plusieurs configurations prêtes à l'emploi + activation « maintenir pour activer ».

**Fonctionnalités**
- **Gestion de profils** : créer, renommer, dupliquer, supprimer, choisir le profil actif
- **Hotkey par profil** (ex. F6 = profil 1, F7 = profil 2) ; un seul profil actif à la fois
- **ActivationMode** :
  - `Toggle` : appui = ON, nouvel appui = OFF
  - `Hold` : la macro tourne **uniquement tant que la touche est maintenue** → nécessite un hook `WH_KEYBOARD_LL`
- **Filtre de fenêtre cible (optionnel)** : la macro n'envoie des touches que si la fenêtre au premier plan appartient au process choisi (sélection dans une liste des process ouverts). Dès que le focus change → pause automatique. *Sécurité : évite de spammer dans Discord ou un navigateur par erreur.*
- **Import / export** de profils en JSON
- Sauvegarde auto dans `%AppData%\MacroChef\profiles.json` (écriture atomique : fichier temporaire puis remplacement)

**Spécifications techniques**
- Le hook `WH_KEYBOARD_LL` doit être **ultra léger** : il ne fait qu'enregistrer l'événement et le pousser dans une file ; aucun traitement lourd dans le callback (sinon Windows retire le hook après ~1 s de timeout)
- Le hook doit **ignorer les événements injectés** (flag `LLKHF_INJECTED`) pour la détection des hotkeys, sinon la macro peut se déclencher elle-même
- La touche de hotkey en mode Hold ne doit pas faire partie de la séquence (validation dans l'UI)
- Validation des profils au chargement (JSON corrompu → backup + profil par défaut, sans crash)

**Tests**
- Sérialisation/désérialisation aller-retour des profils
- JSON corrompu → pas de crash, backup créé
- Logique Hold : down → start, up → stop (avec fake hook)
- Filtre process : focus perdu → pause, focus revenu → reprise

✅ **Acceptation**
- 3 profils différents fonctionnent chacun avec leur hotkey
- Mode Hold : relâcher la touche arrête la macro en < 20 ms
- Avec filtre sur `notepad.exe`, passer sur une autre fenêtre met en pause immédiatement

---

### 🔹 J4 — Labo de mesure (`v0.4.0` → `v1.0.0`)
**Objectif :** **mesurer** pour comparer objectivement **manuel vs automatique** (timing réel des inputs).

**Fonctionnalités**
- Onglet **« Labo »** avec une **session d'enregistrement** : bouton Start/Stop (+ compte à rebours 3-2-1 optionnel, durée fixe optionnelle, ex. 20 s)
- Pendant la session, capture via hook clavier :
  - horodatage haute résolution (`Stopwatch`) de chaque key down/up
  - touche
  - **source** : physique (toi) ou injectée (MacroChef), via `LLKHF_INJECTED`
- **Résultats de session** :
  - nombre d'inputs total et par touche
  - inputs/seconde
  - intervalle moyen, min, max, p95, jitter (par source)
  - **graphique** des intervalles dans le temps (lib légère type `ScottPlot.WPF` ou `LiveCharts2`, à justifier)
- **Comparaison de 2 sessions** côte à côte (ex. « Manuel 20 s » vs « MacroChef 20 s »)
- **Export CSV** d'une session
- Champ **note libre** par session (ex. « mannequin, macro officielle, 20 s → X dégâts »), pour reporter à la main un résultat externe

**Contraintes de confidentialité (OBLIGATOIRES)**
- La capture n'est active **que pendant une session lancée manuellement**, avec un indicateur 🔴 REC bien visible
- Données en mémoire, sauvegardées **uniquement** sur export/sauvegarde explicite
- Aucune capture en arrière-plan hors session, aucun envoi réseau

**Tests**
- Calculs de stats par source
- Export CSV : en-têtes et format vérifiés
- La capture s'arrête bien à la fin de la durée fixe

✅ **Acceptation**
- Session manuelle de 20 s en tapant dans le Bloc-notes → stats cohérentes
- Session MacroChef à 10 ms → stats cohérentes avec le panneau J2
- Comparaison de 2 sessions affichée correctement, export CSV lisible dans Excel

### 🏁 Release v1.0.0
- README complet : installation, utilisation, captures d'écran, tableau des stratégies de timer, limites connues
- Publication `dotnet publish` en **single-file, self-contained, win-x64**
- Tag `v1.0.0`

---

## 6. 🎨 UI / UX

- **Thème sombre** par défaut, look sobre et lisible (type outil gamer, sans surcharge)
- Fenêtre principale avec onglets : **Macro** | **Profils** | **Labo** | **Paramètres**
- État RUNNING/STOPPED **énorme et coloré**, visible d'un coup d'œil
- Option **« Toujours au premier plan »** et **mode compact** (mini-fenêtre affichant seulement l'état + le profil actif)
- Icône dans la **zone de notification** (system tray) avec menu : profil actif, Start/Stop, Quitter
- Tous les champs numériques validés (pas de valeurs négatives, min 1 ms)
- Gestion du DPI (Per-Monitor V2) pour éviter le flou sur écrans haute résolution

---

## 7. ⚠️ Pièges connus à gérer

| Piège | Ce qu'il faut faire |
|---|---|
| **Touches bloquées** après crash/arrêt | key-up systématique dans un `finally` + handler `AppDomain.UnhandledException` |
| **UIPI** : `SendInput` ne peut pas envoyer vers une appli lancée en **administrateur** si MacroChef ne l'est pas | Détecter le cas, afficher un message explicatif, proposer de relancer MacroChef en admin (manifest optionnel, PAS forcé par défaut) |
| Résolution du timer | `timeEndPeriod` toujours appelé ; jamais de `timeBeginPeriod` « oublié » |
| Hook qui ralentit le PC | Callback minimal + file d'événements traitée sur un autre thread |
| Hotkey déjà prise par une autre appli | Message clair + proposition d'en choisir une autre |
| Auto-déclenchement | Ignorer `LLKHF_INJECTED` pour la logique des hotkeys |
| Thread UI bloqué | Le moteur ne touche jamais directement l'UI : passer par `Dispatcher` / propriétés observables |
| Certaines applis ignorent les inputs injectés | Documenter la limite dans le README. **Ne pas** chercher à la contourner (voir §2) |

---

## 8. ✅ Definition of Done (pour chaque jalon)

- [ ] Tous les critères d'acceptation du jalon validés
- [ ] `dotnet build` OK, `dotnet test` vert
- [ ] Pas de logique métier dans le code-behind WPF
- [ ] Code commenté en français sur les parties non évidentes (P/Invoke, timing, threading)
- [ ] `CHANGELOG.md` + `CLAUDE.md` à jour
- [ ] Checklist de test manuel fournie à l'utilisateur
- [ ] Commit + tag de version
- [ ] Récap court : ce qui a été fait, ce qu'il a appris, ce qui reste

---

## 9. 🚀 Démarrage

**Ta première action :**
1. Résume ce cahier des charges en 10 lignes max pour confirmer ta compréhension.
2. Pose les questions bloquantes éventuelles (une seule série, regroupée).
3. Présente le plan du **J0** et attends le feu vert.

Ne code rien avant validation du plan J0.
