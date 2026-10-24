# 🧪 Tests manuels — Maquereaux

Tout ce qui est automatisable l'est déjà (tests unitaires, tests d'intégration Win32 sur runner Windows, benchmarks,
smoke test de l'exe avec captures d'écran). Restent les points qui demandent **un humain devant un vrai PC** :
ressenti UI, vraies applis, vrais claviers. Compte ~20 minutes pour tout faire.

> Prérequis : télécharge `Maquereaux.exe` depuis l'onglet **Releases** et lance-le (SmartScreen → *Informations
> complémentaires* → *Exécuter quand même*, l'exe n'est pas signé). Ouvre aussi le **Bloc-notes**.

Légende : ☐ à faire · ✅ OK · ❌ KO (note ce que tu vois)

---

## 0. Démarrage
| # | Action | Attendu |
|---|---|---|
| 0.1 | Double-clic sur `Maquereaux.exe` | La fenêtre s'ouvre en < 3 s, bandeau rouge **STOPPED**, icône 🐟 dans la barre des tâches et près de l'horloge |
| 0.2 | Relancer l'exe une 2ᵉ fois | Pas de 2ᵉ fenêtre : la fenêtre existante revient au premier plan |
| 0.3 | Écran haute résolution (125/150 %) ou déplacer la fenêtre sur un autre écran | Texte net, pas flou |

## 1. Moteur de base (J1)
| # | Action | Attendu |
|---|---|---|
| 1.1 | Profil par défaut (a, z, e — 100 ms). Clique dans le Bloc-notes, appuie sur **F6** | `azeazeaze…` s'écrit en boucle, bandeau vert **RUNNING** |
| 1.2 | Appuie à nouveau sur **F6** | Ça s'arrête, bandeau **STOPPED** |
| 1.3 | Relance (F6) puis **Ctrl+Alt+Pause** | Arrêt immédiat (« Arrêt d'urgence ») |
| 1.4 | Relance puis ferme Maquereaux (croix) pendant l'exécution | Plus rien ne s'écrit, **aucune touche bloquée** (tape au clavier : tout est normal) |
| 1.5 | Pendant l'exécution, redimensionne/déplace la fenêtre, change d'onglet | UI fluide, la cadence ne bouge pas (stats en bas) |
| 1.6 | Onglet Macro → « + Ajouter une touche » → appuie sur **Flèche gauche** puis lance dans le Bloc-notes | Le curseur recule (touche étendue OK, pas un « 4 » du pavé numérique) |
| 1.7 | Change le mode en **Burst**, intervalle 200 ms | `aze` apparaît par paquets de 3 toutes les 200 ms |
| 1.8 | Répétitions = 2, mode Cycle | `azeaze` puis arrêt tout seul (« Terminé ») |
| 1.9 | Clique sur le champ « Raccourci ON/OFF » puis appuie sur **F6** | Le raccourci est capturé (la macro ne démarre PAS) |
| 1.10 | Dans une autre appli qui utilise déjà un raccourci (ex. **Ctrl+Alt+Suppr** impossible, essaye un raccourci pris par Discord/OBS), choisis-le | Bandeau orange : « … est déjà utilisé par une autre application » |
| 1.11 | Saisis un intervalle de `-5` ou `abc` | Champ refusé (bordure rouge) ou ramené à 1 ; jamais de crash |

## 2. Précision du timer (J2)
| # | Action | Attendu |
|---|---|---|
| 2.1 | Intervalle **10 ms**, touche F24 (ou une lettre dans le Bloc-notes), Paramètres → *Haute résolution*, lance 10 s | Panneau du bas : moyenne ≈ 10,0 ms, p95 ≤ 11 ms, manqués ≈ 0 |
| 2.2 | Même chose en **Standard** | Rythme haché : p95 ~16–20 ms, beaucoup de « manqués » (c'est normal, c'est la référence) |
| 2.3 | Même chose en **Hybride** | p95 ≈ 10,0 ms, jitter très bas ; l'avertissement CPU est affiché dans les Paramètres |
| 2.4 | Gestionnaire des tâches pendant 2.1 puis 2.3 | Hybride consomme visiblement plus de CPU que Haute résolution |

## 3. Profils et activation (J3)
| # | Action | Attendu |
|---|---|---|
| 3.1 | Onglet Profils → **+ Nouveau** ×2, donne F7 et F8 aux nouveaux profils (onglet Macro) | 3 profils, chacun lance SA séquence avec son raccourci ; un seul tourne à la fois |
| 3.2 | Renomme (clic sur le nom), duplique, supprime un profil | Fonctionne ; suppression demande confirmation ; impossible de supprimer le dernier |
| 3.3 | Mets un profil en **Maintenir** (F7). Bloc-notes : maintiens F7 | La macro tourne tant que F7 est enfoncé, s'arrête **net** au relâchement (< 20 ms, aucune lettre en trop visible) |
| 3.4 | En mode Maintenir, ajoute F7 dans la séquence | Avertissement orange « la touche du raccourci ne peut pas faire partie de la séquence » |
| 3.5 | Fenêtre cible = `notepad.exe`, lance dans le Bloc-notes, puis clique sur une autre fenêtre | Bandeau orange **PAUSED** immédiat, rien n'est tapé ailleurs ; retour au Bloc-notes → reprise |
| 3.6 | Exporter un profil, le supprimer, l'importer | Il revient à l'identique (sans écraser un profil existant) |
| 3.7 | Ferme et relance Maquereaux | Profils, profil actif et réglages conservés |
| 3.8 | Maquereaux fermé : remplace le contenu de `%AppData%\Maquereaux\profiles.json` par `nimporte quoi`, relance | Pas de crash ; notification « illisible », un fichier `.corrupt-…bak` créé, profil par défaut |
| 3.9 | Lance le Bloc-notes **en administrateur**, cible-le avec Maquereaux non-admin | Notification UIPI ; Paramètres → « Relancer en administrateur » règle le problème |
| 3.10 | Bouton **Compact** | Mini-fenêtre en haut à droite, déplaçable, START/STOP fonctionnel, ⤢ revient à la fenêtre complète |
| 3.11 | Icône près de l'horloge : clic droit | Menu profil / Démarrer-Arrêter / Arrêt d'urgence / Afficher / Quitter ; pastille verte pendant l'exécution |
| 3.12 | Paramètres → « Réduire dans la zone de notification », puis réduire | La fenêtre disparaît de la barre des tâches ; double-clic sur l'icône la ramène |
| 3.13 | Paramètres → Thème clair | Tout passe en clair instantanément (bandeau compris) |
| 3.14 | Paramètres → arrêt automatique = 1 min, lance une macro infinie | Arrêt tout seul au bout d'1 min avec message « Arrêt de sécurité » |

## 4. Labo (J4)
| # | Action | Attendu |
|---|---|---|
| 4.1 | Onglet Labo, durée fixe 20 s, compte à rebours coché → **Enregistrer** | 3-2-1 puis **● REC** visible (dans le Labo ET dans le bandeau du haut) |
| 4.2 | Tape dans le Bloc-notes pendant 20 s | Fin automatique à 20 s ; session « Manuel » : inputs/s, intervalles, durée d'appui cohérents avec ta frappe |
| 4.3 | Nouvelle session de 20 s en lançant une macro à 10 ms | Session « Maquereaux » ≈ 100 inputs/s, intervalle moyen ≈ 10 ms (cohérent avec le panneau J2) |
| 4.4 | Section Comparer | A = manuel, B = Maquereaux remplis automatiquement, écarts colorés, deux graphiques côte à côte |
| 4.5 | Écris une note (« mannequin… ») puis **Exporter CSV…** et ouvre le fichier dans **Excel** | Colonnes bien séparées, décimales correctes (virgule en FR), accents OK |
| 4.6 | **Sauvegarder…** puis supprime la session et **Ouvrir…** le fichier | La session revient avec sa note |
| 4.7 | Hors session : tape au clavier | Rien n'est capturé (le compteur ne bouge pas, pas de REC) |
| 4.8 | Maintiens une touche 2 s pendant une session | Compte comme **1** input (les répétitions automatiques du clavier sont ignorées) |

## 5. Limites connues à vérifier (comportement attendu, pas des bugs)
- Certains jeux/anti-cheat ignorent les entrées injectées (flag `LLKHF_INJECTED`) : c'est voulu, Maquereaux ne le contourne pas.
- Impossible d'envoyer des touches sur l'écran de verrouillage / UAC (bureau sécurisé).
- Win+L, Ctrl+Alt+Suppr ne peuvent pas être simulés (protégés par Windows).
