# 🐟 Maquereaux

Auto-presser de touches pour Windows : envoie une séquence de touches en boucle, à intervalle réglable
(jusqu'à 10 ms réels), avec hotkeys globaux, profils et un labo de mesure du timing.

> 🚧 En construction — jalon J0 (socle). Voir `CHANGELOG.md`.

## 📥 Installation
Onglet **Releases** → télécharge `Maquereaux.exe` → double-clic. Aucun .NET à installer.

## 🛠️ Build
```bash
dotnet build
dotnet test --filter "Category!=Benchmark"
dotnet publish src/Maquereaux.App -c Release -p:PublishSingleFile=true -o publish
```
