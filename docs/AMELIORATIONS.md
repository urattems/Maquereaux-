# 🚀 Améliorations au-delà du cahier des charges

Chaque ajout reste dans l'esprit du projet et respecte la section « Hors périmètre ».

| # | Amélioration | Justification |
|---|---|---|
| A-01 | Mode `--smoke-test` + smoke test automatique de l'exe publié en CI | Garantit à chaque push/tag que l'exe téléchargeable démarre sans crash. |
| A-02 | Empreinte SHA-256 jointe à chaque release | Permet de vérifier l'intégrité du `.exe` téléchargé. |
| A-03 | Test d'intégration Windows bout-en-bout (SendInput → RegisterHotKey / hook) | Prouve que les vraies touches partent, pas seulement les fakes. |
| A-04 | Captures d'écran automatiques à chaque CI (smoke test) | Vérifier visuellement l'UI sans PC Windows ; alimente le README. |
| A-05 | Signature `dwExtraInfo` sur chaque touche envoyée | Le Labo distingue « Maquereaux » de « autre logiciel injecté » (identification, pas masquage). |
| A-06 | Recalage après un gros retard | Pas de rafale de rattrapage après un gel du PC/une veille. |
| A-07 | Résolution du scan code selon la disposition de la fenêtre cible | Un profil créé en AZERTY tape les bonnes lettres en QWERTY. |
| A-08 | Raccourcis des profils suspendus pendant une capture de touche | F6 peut être capturé sans lancer la macro. |
| A-09 | Stratégie de timer affichée en direct + benchmarks à 1 et 5 ms | Comprendre jusqu'où descendre et à quel coût CPU. |
| A-10 | Garde-fou : arrêt automatique après N minutes | Respect du « Hors périmètre » (pas de farm AFK) + protection contre l'oubli. |
| A-11 | Thème clair, icône de notification à pastille d'état, mode compact déplaçable | Confort ; l'état reste visible même fenêtre réduite. |
| A-12 | Instance unique (relancer = ramener la fenêtre) | Évite les conflits de raccourcis entre deux instances. |
| A-13 | Validation en direct des profils (conflits, maintien dans la séquence, cadence intenable) | Les erreurs sont visibles avant de lancer la macro. |
| A-14 | Import qui n'écrase jamais (Ids + raccourcis en conflit neutralisés) | Importer un profil partagé ne casse rien. |
| A-15 | Logs fichier (14 jours) + bouton « Ouvrir le dossier » | Debug facile, données 100 % locales. |
| A-16 | Notification si Windows refuse des événements (SendInput partiel) | Diagnostic UIPI/bureau sécurisé. |
