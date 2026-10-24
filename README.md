# 🐟 Maquereaux

Auto-presser de touches pour Windows : envoie une séquence de touches en boucle, à intervalle réglable
(jusqu'à 10 ms réels), avec hotkeys globaux, profils et un labo de mesure du timing.

> 🚧 En construction — jalons J0 → J2 livrés. Voir `CHANGELOG.md`.

## 📥 Installation
Onglet **Releases** → télécharge `Maquereaux.exe` → double-clic. Aucun .NET à installer.

## 🛠️ Build
```bash
dotnet build
dotnet test --filter "Category!=Benchmark"
dotnet publish src/Maquereaux.App -c Release -p:PublishSingleFile=true -o publish
```

## ⏱️ Précision du timer (J2)

Par défaut, Windows ne réveille un thread qu'à chaque « tic » d'horloge (~15,6 ms) : un `Thread.Sleep(10)` dure souvent ~15 ms.
Maquereaux propose 3 stratégies (Paramètres → Précision du timer). Mesures réelles, 1 000 ticks par ligne, runner GitHub
Windows (VM 4 cœurs, Windows 10.0.26100), via le vrai moteur :

| Stratégie | Intervalle | Moyenne | Min | Max | p95 | p99 | Jitter (σ) | Manqués | CPU (1 cœur) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Standard | 10 ms | 10.003 | 0.000 | 25.883 | 19.184 | 23.707 | 7.320 | 467 | 2.2 % |
| **HighResolution** (défaut) | 10 ms | **10.000** | 9.320 | 10.627 | **10.338** | 10.592 | 0.251 | 0 | 0.5 % |
| Hybrid | 10 ms | **10.000** | 7.858 | 13.041 | **10.001** | 10.008 | 0.124 | 0 | 4.7 % |
| HighResolution | 5 ms | 5.011 | 4.535 | 14.832 | 5.562 | 5.576 | 0.405 | 1 | 0.3 % |
| Hybrid | 5 ms | 5.000 | 4.984 | 5.016 | 5.001 | 5.003 | 0.001 | 0 | 9.4 % |
| HighResolution | 1 ms | 1.000 | 0.523 | 1.589 | 1.058 | 1.551 | 0.182 | 25 | 1.6 % |
| Hybrid | 1 ms | 1.000 | 0.982 | 1.018 | 1.000 | 1.002 | 0.001 | 0 | 100.0 % |

Comment lire ce tableau :
- **Standard** a une moyenne « parfaite » uniquement parce que le moteur vise des échéances absolues : il rattrape son retard
  (min 0 ms = deux ticks collés). En réalité le rythme est haché : p95 à 19 ms, 47 % d'échéances manquées.
- **HighResolution** tient 10 ms à ±0,3 ms pour un CPU quasi nul → c'est le défaut.
- **Hybrid** est d'une précision chirurgicale (jitter de l'ordre de la microseconde à 5 ms) mais brûle du CPU : à 1 ms il occupe un cœur entier.

Relancer les benchmarks : `dotnet test tests/Maquereaux.Win32.Tests -c Release --filter "Category=Benchmark"`
(ou voir le résumé du job « Benchmarks timers » de chaque run CI).
