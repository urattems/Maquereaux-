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

### D-008 — Release v0.1.0 construite depuis une branche dédiée
- **Décision** : J1 et J2 ont été développés en parallèle (la CI Windows sert de banc de test). Pour garder
  « 1 jalon = 1 release », la v0.1.0 a été construite depuis `release/v0.1.0` (commit J1 + 2 correctifs), publiée via
  le workflow manuel, puis fusionnée dans `main`.
- **Pourquoi** : chaque jalon reste téléchargeable tel qu'il était à la fin de son jalon.

### D-009 — Sleeper créé et détruit à chaque exécution
- **Décision** : le moteur reçoit une *fabrique* de sleeper (`Func<ISleeper>`), appelée à chaque Start ; le sleeper est disposé
  dans le `finally` du thread moteur.
- **Pourquoi** : garantit `timeEndPeriod(1)` (repli) et la fermeture du handle de timer même après une exception ; permet de
  changer de stratégie entre deux exécutions sans rien redémarrer.
- **Écarté** : un sleeper unique pour toute la vie de l'appli (aurait gardé la résolution système à 1 ms en permanence).

### D-010 — Stratégie par défaut : HighResolution
- **Décision** : `HighResolution` est le défaut ; `Hybrid` est proposé avec un avertissement CPU.
- **Pourquoi** : les benchmarks montrent que HighRes atteint déjà le critère J2 (p95 10,34 ms à 10 ms) pour 0,5 % de CPU,
  alors que Hybrid coûte ~10× plus de CPU (100 % d'un cœur à 1 ms) pour un gain que l'utilisateur ne percevra que sous ~5 ms.

### D-011 — Mesure via le moteur réel avec un envoi factice
- **Décision** : les benchmarks pilotent le vrai `MacroEngine` (même boucle que l'appli) avec un `IInputSender` qui n'envoie rien.
- **Pourquoi** : mesure exactement ce que l'utilisateur obtient, sans taper de touches sur la machine de CI.

### D-012 — Percentiles par interpolation linéaire
- **Décision** : p95/p99 calculés comme `PERCENTILE.INC` d'Excel (interpolation entre rangs).
- **Pourquoi** : les chiffres affichés correspondent à ce qu'on obtient en recalculant l'export CSV dans Excel.
- **Écarté** : « nearest rank » (sauts brusques sur petits échantillons).

### D-013 — Écart-type « population » pour le jitter
- **Décision** : jitter = écart-type population (division par N).
- **Pourquoi** : on décrit la série mesurée elle-même, pas une estimation d'une population plus large ; l'écart avec N−1 est
  négligeable dès quelques centaines d'intervalles.

### D-014 — Hotkeys : RegisterHotKey pour ON/OFF, hook bas niveau seulement pour « Maintenir »
- **Décision** : les profils Toggle et l'arrêt d'urgence passent par `RegisterHotKey` ; les profils Hold par le hook `WH_KEYBOARD_LL`.
  Le hook n'est installé que s'il existe au moins un profil Hold (ou pendant une session du Labo), via un compteur de références.
- **Pourquoi** : `RegisterHotKey` détecte les conflits avec les autres applis et ne voit pas toutes les frappes (vie privée, perfs) ;
  seul le hook sait détecter le relâchement d'une touche.
- **Écarté** : tout faire au hook (perte de la détection de conflit, hook permanent).

### D-015 — La touche de maintien est « avalée »
- **Décision** : en mode Maintenir, l'appui et le relâchement de la touche du raccourci ne sont pas transmis aux applis.
- **Pourquoi** : même comportement que RegisterHotKey pour les profils ON/OFF ; évite qu'un F7 maintenu déclenche autre chose
  dans l'appli cible.

### D-016 — Boucle de messages Win32 sur un thread dédié
- **Décision** : hotkeys et hook vivent sur un thread à part (`MessageLoopThread`, priorité Highest) qui ne fait que pomper des messages ;
  les ordres Start/Stop partent sur une file de commandes (`SerialExecutor`).
- **Pourquoi** : si l'UI WPF rame, le hook répond quand même instantanément (pas de retrait du hook par Windows, Hold < 20 ms).

### D-017 — Instantanés de profils thread-safe (`ProfileRegistry`)
- **Décision** : l'UI publie une copie de chaque profil à chaque modification ; les threads hotkeys/moteur ne lisent que ces copies.
- **Pourquoi** : aucun risque de « collection modifiée » si on édite un profil pendant qu'un raccourci le démarre.

### D-018 — NotifyIcon WinForms pour la zone de notification
- **Décision** : `UseWindowsForms=true` uniquement pour `NotifyIcon` (framework inclus dans .NET, aucun NuGet), usings WinForms retirés.
- **Pourquoi** : WPF n'a pas d'icône de notification ; ~100 lignes d'interop Shell_NotifyIcon pour refaire la même chose = « magie » inutile.

### D-019 — Instance unique par dossier de données
- **Décision** : mutex nommé dérivé du dossier de données ; une 2ᵉ instance signale la 1ʳᵉ (EventWaitHandle) qui se remet au premier plan.
- **Pourquoi** : deux instances = conflits de raccourcis garantis. Le smoke test (autre dossier de données) reste lançable en parallèle.

### D-020 — Garde-fou de durée (arrêt automatique)
- **Décision** : toute exécution s'arrête après N minutes (60 par défaut, max 240), non désactivable.
- **Pourquoi** : cohérent avec le « Hors périmètre » (pas d'automatisation longue durée sans présence) ; protège aussi d'un oubli.

### D-021 — Couleurs d'état par DataTriggers + DynamicResource
- **Décision** : bandeau, pastille et texte d'état utilisent des styles à DataTriggers dont les setters sont en `DynamicResource`.
- **Pourquoi** : un convertisseur qui renvoie une brosse n'est pas réévalué au changement de thème (bug vu sur capture en thème clair).

### D-022 — Graphique du Labo fait maison
- **Décision** : `IntervalChart`, un `FrameworkElement` qui dessine directement via `DrawingContext` (StreamGeometry gelée par source).
- **Pourquoi** : quelques milliers de points, un seul type de graphe ; ScottPlot/LiveCharts2 ajoutent SkiaSharp et plusieurs Mo
  à l'exe single-file pour un besoin couvert en ~150 lignes. Règle « pas de magie » du cahier des charges.
- **Écarté** : ScottPlot.WPF, LiveCharts2, OxyPlot.

### D-023 — Horodatage dans le callback du hook
- **Décision** : chaque événement est horodaté avec le `Stopwatch` dès l'entrée dans le callback `WH_KEYBOARD_LL`
  (et non avec `KBDLLHOOKSTRUCT.time`, en millisecondes `GetTickCount`).
- **Pourquoi** : résolution ~100 ns et même base de temps que le moteur → stats du Labo directement comparables au panneau J2.
  La latence de livraison du hook (quelques µs) est la même pour toutes les sources.

### D-024 — CSV selon la culture Windows + BOM
- **Décision** : séparateur = séparateur de liste de la culture, décimales de la culture, UTF-8 avec BOM.
- **Pourquoi** : double-clic → Excel FR ouvre correctement (`;` et `,`), Excel EN aussi (`,` et `.`), accents intacts.
- **Écarté** : CSV invariant (`,` + `.`) qui s'ouvre « en une colonne » dans Excel FR.

### D-025 — Répétitions automatiques exclues des inputs
- **Décision** : un key-down reçu alors que la touche est déjà enfoncée est marqué `IsRepeat` et exclu des stats (gardé dans le CSV).
- **Pourquoi** : maintenir une touche n'est pas « appuyer 30 fois » ; sinon la comparaison manuel/auto serait faussée.

### D-026 — Sessions du Labo jamais écrites automatiquement
- **Décision** : pas d'historique sur disque ; export CSV et sauvegarde `.mqsession.json` uniquement sur clic.
- **Pourquoi** : contrainte de confidentialité du cahier des charges (les frappes peuvent contenir du texte sensible).

### D-027 — Tests concurrents : attendre l'événement, pas l'état
- **Décision** : les tests qui observent la fin d'une exécution attendent l'événement `Stopped`, jamais `IsRunning == false`.
- **Pourquoi** : le moteur passe l'état à Stopped juste avant de lever l'événement ; sous forte charge CPU, un test qui
  sondait l'état lisait la liste des événements trop tôt (échec intermittent repéré puis reproduit sous stress, corrigé, 0/10 sous stress).

---

## Refonte UI/UX v1.x (« la carrosserie »)

### D-028 — Polices embarquées : Inter + JetBrains Mono (SIL OFL 1.1)
- **Décision** : Inter (Regular/Medium/SemiBold/Bold) pour l'interface, JetBrains Mono pour **tous** les nombres qui bougent,
  embarquées en `Resource` (pack URI), licences copiées dans `Assets/Fonts/`.
- **Pourquoi** : Segoe UI a des chiffres proportionnels → les stats « dansent » à 10 Hz. Une mono tabulaire garde chaque chiffre
  à sa place. Polices libres, lisibles à 100 % comme à 200 %, poids raisonnable (~1,5 Mo dans l'exe).
- **Écarté** : Cascadia Mono (pas garantie hors Windows 11), polices variables (support WPF partiel).

### D-029 — Icônes : Fluent UI System Icons (MIT), converties en `Geometry`
- **Décision** : une seule famille (Fluent, 20 px regular + quelques filled), générée par `tools/gen_icons.py` en `Themes/Icons.xaml`.
- **Pourquoi** : cohérente avec Windows 11, licence MIT, vectorielle (net à tous les DPI), recolorable par `Foreground`.
  Le script normalise les drapeaux d'arcs SVG compacts (`a1 1 0 011 1`) que le mini-langage WPF ne lit pas.
- **Écarté** : police d'icônes Segoe Fluent (absente de Windows 10), emojis (interdits par la DA).

### D-030 — Tokens + DynamicResource partout ; StaticResource seulement intra-fichier
- **Décision** : couleurs, espacements (grille 4/8), rayons, tailles de police, durées et ombres sont des clés de `Tokens.xaml`
  et des dictionnaires de thème. Dans les dictionnaires de thème, toute référence à une clé d'un **autre** dictionnaire fusionné
  est en `DynamicResource` ; `tools/check_xaml.py` le vérifie avant chaque push.
- **Pourquoi** : une `StaticResource` vers un dictionnaire frère fait planter le chargement des ressources de l'App
  (constaté en CI : appli figée, aucune capture). Le thème se change à chaud en remplaçant le dictionnaire de couleurs.

### D-031 — Coque Windows 11 : WindowChrome + Mica + Snap Layouts
- **Décision** : `WindowChrome` (barre de titre maison de 44 px), Mica via `DwmSetWindowAttribute` (build ≥ 22621, sinon fond uni),
  coins arrondis DWM, et `WM_NCHITTEST` → `HTMAXBUTTON` au-dessus du bouton Agrandir pour garder les Snap Layouts.
- **Pourquoi** : rendu natif Windows 11 sans dépendance (pas de WPF-UI / ModernWpf), dégradation propre sur Windows 10.

### D-032 — Navigation latérale + onglets « headless »
- **Décision** : sidebar repliable (232 / 56 px, état persistant) qui pilote un `TabControl` sans en-têtes ; transition fondu + glissé 8 px.
- **Pourquoi** : garde le cycle de vie des vues existantes (aucune régression fonctionnelle) tout en changeant totalement la navigation.

### D-033 — Projet `Maquereaux.App.Tests` (net8.0-windows)
- **Décision** : la logique d'écran testable (presets, équivalences, conseils, parsing du NumericBox, édition de séquence du
  ViewModel) vit dans des classes sans dépendance visuelle, testées par xUnit sur Windows (CI). Sous Linux le projet compile
  mais n'est pas exécuté (`IsTestProject=false`).
- **Pourquoi** : les ViewModels référencent WPF (ObservableObject + types WPF) → impossible à exécuter sous Linux.

### D-034 — Curseur d'intervalle logarithmique (1 ms – 1 s), aimanté sur les presets
- **Décision** : position = log₁₀(ms) / 3 ; aimantation à ±1 % de course autour de 10/16/33/50/100 ms ; au-delà de 1 s,
  saisie clavier seulement (le curseur ne réécrit jamais un intervalle long saisi à la main).
- **Pourquoi** : en linéaire, 1–20 ms (là où tout se joue) occuperait 2 % de la piste.

### D-035 — Animations décoratives coupées quand la fenêtre est cachée
- **Décision** : singleton `Motion` = `SystemParameters.ClientAreaAnimation` ET fenêtre visible/non réduite. Halos et pulsations
  s'arrêtent (storyboard stoppé, pas juste masqué) dès que `Enabled` passe à false. Les stats restent rafraîchies à 10 Hz.
- **Pourquoi** : exigence « performance non négociable » : zéro coût de rendu pendant qu'une macro tourne en arrière-plan.

### D-036 — Carte héros = un seul gros bouton
- **Décision** : toute la carte héros est un `Button` (Start/Stop), son contenu n'a aucun contrôle interactif.
- **Pourquoi** : cible de clic énorme, accessible au clavier (Tab + Espace), lu « Démarrer ou arrêter la macro » par le lecteur d'écran.

### D-037 — Numérotation des versions de la refonte
- **Décision** : le premier jet de l'écran Macro est sorti dès la 1.1.0 avec la coque (il servait à valider le design system
  en conditions réelles). Les versions suivantes gardent la numérotation prévue mais chacune publie le jalon suivant :
  1.2.0 = Profils + Paramètres + sélecteur de fenêtre (U3, avec la fin de U2), 1.3.0 = Labo (U4),
  1.4.0 = pastille compacte, icône de notification, visite guidée, nouvelle icône (U5a), 1.5.0 = accessibilité, documentation,
  galerie avant/après (U5b).
- **Pourquoi** : une release par jalon, sans jamais publier un écran à moitié refondu ; chaque release est construite depuis le
  commit du jalon (worktree) puis `main` est refusionnée dans `ui-revamp`.

### D-038 — Contraste AA vérifié par calcul, accent clair assombri
- **Décision** : un script calcule les ratios WCAG de chaque couleur de texte sur chaque surface ; en thème clair, l'accent
  passe de `#0B9C90` (3,4:1) à `#077A70` (5,2:1), la pause de `#A86C05` à `#9A5B00`, les tertiaires sont rapprochés du secondaire.
- **Pourquoi** : exigence d'accessibilité du cahier des charges ; le turquoise vif reste dans les dégradés décoratifs.

### D-039 — Sélecteur de fenêtre cible côté App (Win32 sanctuarisé)
- **Décision** : `Services/ProcessCatalog` (App) énumère les applis fenêtrées et extrait leur icône (`ExtractAssociatedIcon`),
  à la demande (ouverture du sélecteur), sur un thread de fond, avec un cache d'icônes.
- **Pourquoi** : `Maquereaux.Win32` ne change pas ; aucune surveillance permanente des process (zéro coût en tâche de fond).

### D-040 — Suppression de profil sans confirmation, avec « Annuler »
- **Décision** : la boîte de dialogue « Supprimer définitivement ? » disparaît au profit d'un toast « Annuler » (8 s).
- **Pourquoi** : plus rapide au quotidien et rien n'est perdu ; le profil revient à sa place (sans son raccourci s'il a été repris).

### D-041 — Graphique du Labo : fond en cache, interactions sans dépendance
- **Décision** : le nuage de points est dessiné une fois dans un `DrawingGroup` gelé ; le survol ne redessine que l'info-bulle.
  Zoom par Ctrl+molette (la molette seule fait défiler la page), glisser pour défiler, double-clic pour tout revoir.
- **Pourquoi** : fluide même avec plusieurs milliers de points, sans bibliothèque de graphiques (single-file léger).

### D-042 — Icône d'application générée depuis le logo
- **Décision** : `tools/gen_icon.py` produit l'ICO 16 → 256 px depuis les tracés du logo (rendu Chromium, réduction Lanczos),
  avec une variante simplifiée pour 16 et 24 px (sans rayures, œil agrandi).
- **Pourquoi** : une seule source de vérité pour le logo ; lisible dans la barre des tâches comme dans l'explorateur.

---

## v1.6 — Maintien sur un bouton de souris, arrêt d'urgence F9

### D-043 — Boutons de souris = « touches » VK 0x01–0x06, mode Maintenir uniquement
- **Décision** : un raccourci peut être un bouton de souris (clic gauche/droit/molette, boutons 4/5), stocké comme un VK
  (0x01, 0x02, 0x04, 0x05, 0x06) dans le même modèle `Hotkey`. Détection par un hook `WH_MOUSE_LL` qui traduit les boutons
  en `RawKeyEvent` et réutilise `HoldHotkeyTracker`. Mode Maintenir obligatoire (RegisterHotKey ne connaît pas la souris) :
  choisir un bouton bascule le profil en Maintenir, ON/OFF est grisé.
- **Pourquoi** : besoin réel en jeu (spammer seulement pendant le clic) ; zéro nouveau chemin dans le moteur, logique déjà testée.
- **Garde-fous** : le clic n'est **jamais avalé** (le jeu le reçoit) ; les clics injectés sont ignorés ; un appui sur une fenêtre
  de Maquereaux (sous le curseur) est ignoré ; les relâchements passent toujours (même pendant une capture dans l'UI) ;
  déplacements et molette sont écartés dès la première ligne du callback ; le hook n'est installé que si un profil l'utilise ;
  avertissement si aucune fenêtre cible n'est choisie.
- **Hors périmètre respecté** : simple déclenchement par l'utilisateur, aucune humanisation ni lecture d'écran.

### D-044 — Arrêt d'urgence par défaut : F9 (migration de Ctrl+Alt+Pause)
- **Décision** : nouveau défaut F9 ; champ `SettingsVersion` dans les réglages ; au premier chargement d'un fichier antérieur,
  **seul l'ancien défaut** Ctrl+Alt+Pause est remplacé par F9 (un raccourci personnalisé est conservé). Un bouton de souris est
  refusé comme arrêt d'urgence (RegisterHotKey).
- **Pourquoi** : une seule touche, atteignable en jeu sans lâcher la souris ; demandé par l'utilisateur.
- **Limite** : F9 étant enregistré globalement, les autres applis ne le reçoivent plus pendant que Maquereaux tourne.

### D-045 — Compatibilité jeux : mode jeu + méthode d'envoi, et rien au-delà
- **Constat (CSV d'un utilisateur)** : séquence A/T à 10 ms avec des appuis de ~1,4 ms. Tout part (Bloc-notes OK), le jeu ne
  lance pas les sorts : appuis invisibles pour une lecture par image, cadence qui écrase les sorts.
- **Décision** : (1) « mode jeu » = appui 30 ms + intervalle ≥ 100 ms, proposé par un bandeau quand la cible ressemble à un jeu ;
  (2) méthode d'envoi au choix, scan code (défaut) ou code virtuel (wVk + wScan sans KEYEVENTF_SCANCODE), réglage global.
- **Refusé** : pilote / périphérique virtuel, masquage du drapeau « injecté », contournement d'anti-cheat — c'est ce que font
  les macros matérielles (Logitech, Razer : la souris elle-même ou un pilote émet les touches), et c'est hors périmètre.
  Si un jeu filtre les entrées simulées, Maquereaux ne peut pas et ne doit pas le contourner.
