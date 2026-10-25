# 🧪 Tests manuels — Maquereaux

Tout ce qui est automatisable l'est déjà (tests unitaires, tests d'intégration Win32 sur runner Windows, benchmarks,
smoke test de l'exe avec captures d'écran). Restent les points qui demandent **un humain devant un vrai PC** :
ressenti UI, vraies applis, vrais claviers. Compte ~30 minutes pour tout faire.

> Prérequis : télécharge `Maquereaux.exe` depuis l'onglet **Releases** et lance-le (SmartScreen → *Informations
> complémentaires* → *Exécuter quand même*, l'exe n'est pas signé). Ouvre aussi le **Bloc-notes**.

Légende : ☐ à faire · ✅ OK · ❌ KO (note ce que tu vois)

---

## 0. Démarrage
| # | Action | Attendu |
|---|---|---|
| 0.1 | Double-clic sur `Maquereaux.exe` (1ʳᵉ fois) | La fenêtre s'ouvre en < 3 s avec la **visite guidée** (3 étapes) ; pastille « À l'arrêt » en bas de la navigation ; nouvelle icône (tuile marine + maquereau) dans la barre des tâches et près de l'horloge |
| 0.2 | Relancer l'exe une 2ᵉ fois | Pas de 2ᵉ fenêtre : la fenêtre existante revient au premier plan |
| 0.3 | Écran haute résolution (125/150 %) ou déplacer la fenêtre sur un autre écran | Texte net, pas flou |

## 1. Moteur de base (J1)
| # | Action | Attendu |
|---|---|---|
| 1.1 | Profil par défaut (a, z, e — 100 ms). Clique dans le Bloc-notes, appuie sur **F6** | `azeazeaze…` s'écrit en boucle ; carte héros et pastille passent au turquoise « En marche » avec halo pulsé |
| 1.2 | Appuie à nouveau sur **F6** | Ça s'arrête, retour à « Prêt à lancer » / « À l'arrêt » |
| 1.3 | Relance (F6) puis **F9** | Arrêt immédiat (« Arrêt d'urgence ») ; si tu avais Ctrl+Alt+Pause (ancien défaut), il a été remplacé par F9 au premier lancement de la v1.6 |
| 1.4 | Relance puis ferme Maquereaux (croix) pendant l'exécution | Plus rien ne s'écrit, **aucune touche bloquée** (tape au clavier : tout est normal) |
| 1.5 | Pendant l'exécution, redimensionne/déplace la fenêtre, change d'onglet | UI fluide, la cadence ne bouge pas (stats en bas) |
| 1.6 | Écran Macro → tuile **+** → appuie sur **Flèche gauche** puis lance dans le Bloc-notes | Le curseur recule (touche étendue OK, pas un « 4 » du pavé numérique) |
| 1.7 | Change le mode en **Burst**, intervalle 200 ms | `aze` apparaît par paquets de 3 toutes les 200 ms |
| 1.8 | Répétitions = 2, mode Cycle | `azeaze` puis arrêt tout seul (« Terminé ») |
| 1.9 | Clique sur le champ « Raccourci » (carte Comportement) puis appuie sur **F6** | Le raccourci est capturé (la macro ne démarre PAS) |
| 1.10 | Dans une autre appli qui utilise déjà un raccourci (ex. **Ctrl+Alt+Suppr** impossible, essaye un raccourci pris par Discord/OBS), choisis-le | Bandeau orange : « … est déjà utilisé par une autre application » |
| 1.11 | Saisis un intervalle de `-5` ou `abc` | Le champ clignote en rouge 1 s et garde l'ancienne valeur ; `16 ms` est accepté ; jamais de crash |

## 2. Précision du timer (J2)
| # | Action | Attendu |
|---|---|---|
| 2.1 | Intervalle **10 ms**, touche F24 (ou une lettre dans le Bloc-notes), Paramètres → *Haute résolution*, lance 10 s | Carte héros : moyenne ≈ 10,0 ms, p95 ≤ 11 ms ; la mini-courbe colle à la ligne « cible » |
| 2.2 | Même chose en **Standard** | Rythme haché : p95 ~16–20 ms, beaucoup de « manqués » (c'est normal, c'est la référence) |
| 2.3 | Même chose en **Hybride** | p95 ≈ 10,0 ms, jitter très bas ; l'avertissement CPU est affiché dans les Paramètres |
| 2.4 | Gestionnaire des tâches pendant 2.1 puis 2.3 | Hybride consomme visiblement plus de CPU que Haute résolution |

## 3. Profils et activation (J3)
| # | Action | Attendu |
|---|---|---|
| 3.1 | Onglet Profils → **+ Nouveau** ×2, donne F7 et F8 aux nouveaux profils (onglet Macro) | 3 profils, chacun lance SA séquence avec son raccourci ; un seul tourne à la fois |
| 3.2 | Renomme (clic sur le nom), duplique, supprime un profil | Fonctionne ; la suppression est immédiate avec un toast **Annuler** qui le restaure ; impossible de supprimer le dernier |
| 3.3 | Mets un profil en **Maintenir** (F7). Bloc-notes : maintiens F7 | La macro tourne tant que F7 est enfoncé, s'arrête **net** au relâchement (< 20 ms, aucune lettre en trop visible) |
| 3.4 | En mode Maintenir, ajoute F7 dans la séquence | Avertissement orange « la touche du raccourci ne peut pas faire partie de la séquence » |
| 3.5 | Fenêtre cible = Bloc-notes (sélecteur avec icônes), lance dans le Bloc-notes, puis clique sur une autre fenêtre | Carte héros ambre « En pause » immédiat, rien n'est tapé ailleurs ; retour au Bloc-notes → reprise |
| 3.6 | Exporter un profil, le supprimer, l'importer | Il revient à l'identique (sans écraser un profil existant) |
| 3.7 | Ferme et relance Maquereaux | Profils, profil actif et réglages conservés |
| 3.8 | Maquereaux fermé : remplace le contenu de `%AppData%\Maquereaux\profiles.json` par `nimporte quoi`, relance | Pas de crash ; notification « illisible », un fichier `.corrupt-…bak` créé, profil par défaut |
| 3.9 | Lance le Bloc-notes **en administrateur**, cible-le avec Maquereaux non-admin | Notification UIPI ; Paramètres → « Relancer en administrateur » règle le problème |
| 3.10 | Bouton **Compact** (barre de titre) | Pastille flottante en haut à droite, déplaçable, bouton rond Start/Stop, double-clic ou ⤢ revient à la fenêtre complète |
| 3.11 | Icône près de l'horloge : clic droit | Menu profil / Démarrer-Arrêter / Arrêt d'urgence / Afficher / Quitter ; pastille turquoise en marche, ambre en pause, grise à l'arrêt |
| 3.12 | Paramètres → « Réduire dans la zone de notification », puis réduire | La fenêtre disparaît de la barre des tâches ; double-clic sur l'icône la ramène |
| 3.13 | Paramètres → Apparence → Clair (ou bouton soleil/lune de la barre de titre) | Tout passe en « écume » instantanément, graphiques compris |
| 3.14 | Paramètres → arrêt automatique = 1 min, lance une macro infinie | Arrêt tout seul au bout d'1 min avec message « Arrêt de sécurité » |

## 4. Labo (J4)
| # | Action | Attendu |
|---|---|---|
| 4.1 | Labo, durée fixe 20 s, compte à rebours activé → **Enregistrer** | 3-2-1 géant en surimpression puis point **REC** pulsé (dans le Labo ET dans la barre de titre) |
| 4.2 | Tape dans le Bloc-notes pendant 20 s | Fin automatique à 20 s ; session « Manuel » : inputs/s, intervalles, durée d'appui cohérents avec ta frappe |
| 4.3 | Nouvelle session de 20 s en lançant une macro à 10 ms | Session « Maquereaux » ≈ 100 inputs/s, intervalle moyen ≈ 10 ms (cohérent avec le panneau J2) |
| 4.4 | Section Comparer | A = manuel, B = Maquereaux remplis automatiquement, **verdict en une phrase**, écarts colorés, deux graphiques côte à côte |
| 4.5 | Écris une note (« mannequin… ») puis **CSV** et ouvre le fichier dans **Excel** | Colonnes bien séparées, décimales correctes (virgule en FR), accents OK |
| 4.6 | Disquette (sauvegarder) puis supprime la session et rouvre le fichier (icône à côté de « Sessions ») | La session revient avec sa note |
| 4.7 | Hors session : tape au clavier | Rien n'est capturé (le compteur ne bouge pas, pas de REC) |
| 4.8 | Maintiens une touche 2 s pendant une session | Compte comme **1** input (les répétitions automatiques du clavier sont ignorées) |

## 5. Refonte UI (v1.1 → v1.5)
| # | Action | Attendu |
|---|---|---|
| 5.1 | Windows 11 : survole le bouton Agrandir | Le menu **Snap Layouts** apparaît ; fond **Mica** (légère teinte du fond d'écran), coins arrondis |
| 5.2 | Passe l'affichage Windows à 150 % puis 200 % | Tout reste net (texte, icônes, keycaps, graphique), rien de tronqué ; à 880 × 600 px la page Macro s'empile proprement |
| 5.3 | Séquence : glisse une touche sur une autre | Barre d'insertion turquoise, les touches glissent en douceur (200 ms) |
| 5.4 | Clic sur une touche puis **Suppr**, **Ctrl+D**, **Alt+→**, **Entrée** | Supprime (lien « Annuler la suppression »), duplique, déplace, passe en capture (« Appuie sur une touche… ») |
| 5.5 | Clic sur une puce « instantané » / « → 100 ms » | Mini-éditeur : molette (avec le focus), ↑↓, Maj ×10 ; vide = intervalle global pour le délai |
| 5.6 | Curseur d'intervalle | Échelle logarithmique, s'aimante sur 10/16/33/50/100 ; presets surlignés ; « = N inputs/s » à jour (×N touches en Burst) |
| 5.7 | Fenêtre cible = un jeu (ou tout exe hors Bloc-notes/navigateurs…) avec des touches à 0 ms | Bandeau « Certains jeux ratent les appuis instantanés » ; **Appliquer 16 ms** met toutes les puces à 16 ms |
| 5.8 | Sélecteur de fenêtre cible : tape « note » | Liste filtrée avec icônes + titres ; ↑↓ Entrée choisit ; un nom inconnu + Entrée cible « nom.exe » |
| 5.9 | Labo, graphique : survole un point, **Ctrl+molette**, glisse, double-clic, onglet Histogramme | Info-bulle (valeur, source, instant), zoom autour du curseur, défilement, retour vue entière, barres + ligne cible |
| 5.10 | Pastille compacte : clic droit → Opacité 40 % ; survole-la | Elle devient translucide puis opaque au survol ; réglage aussi dans Paramètres |
| 5.11 | Paramètres → Général → Visite guidée « Revoir » ; Échap | La visite s'ouvre ; Échap ou « Passer » la ferme et elle ne revient plus au démarrage |
| 5.12 | Paramètres Windows → Accessibilité → Effets visuels → **Effets d'animation désactivés** | Plus aucune animation (halo, transitions, 3-2-1, glisser) ; tout reste fonctionnel |
| 5.13 | Macro en marche, fenêtre **réduite** 1 min (Gestionnaire des tâches) | CPU de Maquereaux au niveau d'avant la refonte (aucune animation ne tourne en arrière-plan) |
| 5.14 | Navigation 100 % clavier : Tab / Maj+Tab / Espace / Entrée sur chaque écran | Anneau de focus turquoise visible partout ; Narrateur lit le nom des boutons icône |
| 5.15b | **Ctrl+1…4**, **Ctrl+N**, et **Ctrl+Z** après avoir supprimé une touche (hors champ texte) | Change de page, crée un profil, la touche revient ; dans un champ texte, Ctrl+Z annule la saisie comme d'habitude |
| 5.15 | Thème clair : relis chaque écran | Contraste confortable, aucune couleur « illisible » (accent assombri en clair) |

## 6. Maintien sur un bouton de souris (v1.6)
| # | Action | Attendu |
|---|---|---|
| 6.1 | Profil → champ Raccourci : clique dedans, puis **clic droit** | Le raccourci devient « Clic droit », le mode passe seul sur **Maintenir** (ON/OFF grisé) |
| 6.2 | Fenêtre cible = Bloc-notes. Dans le Bloc-notes, **maintiens le clic droit** 2 s puis relâche | `aze…` démarre à l'instant de l'appui, s'arrête net au relâchement (aucune lettre en trop) ; le menu contextuel du Bloc-notes s'ouvre quand même (le clic n'est pas avalé) |
| 6.3 | Même chose avec clic gauche, clic molette, boutons latéraux 4/5 | Idem |
| 6.4 | Clique dans la fenêtre de Maquereaux avec le bouton choisi | Rien ne démarre (les clics sur Maquereaux sont ignorés) |
| 6.5 | Sans fenêtre cible | Avertissement orange « chaque clic … lancera la macro » |
| 6.6 | Clic droit maintenu dans le jeu, puis **F9** sans relâcher | Arrêt immédiat ; relâcher ensuite ne relance rien |
| 6.7 | Ne garde que des profils clavier | Le hook souris est retiré (log « Hook souris retiré ») |

## 7. Limites connues à vérifier (comportement attendu, pas des bugs)
- Certains jeux/anti-cheat ignorent les entrées injectées (flag `LLKHF_INJECTED`) : c'est voulu, Maquereaux ne le contourne pas.
- Impossible d'envoyer des touches sur l'écran de verrouillage / UAC (bureau sécurisé).
- Win+L, Ctrl+Alt+Suppr ne peuvent pas être simulés (protégés par Windows).
