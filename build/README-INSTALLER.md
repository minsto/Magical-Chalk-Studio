# Installeur et publication — Magical Chalk Studio

## Prérequis

- [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) installé
- Pour générer l’ **installeur EXE** : [Inno Setup 6](https://jrsoftware.org/isdl.php) (gratuit)

## 1. Publier les binaires Windows (toutes architectures)

Dans PowerShell, à la racine du dépôt ou depuis `build\` :

```powershell
cd "chemin\vers\mm\build"
.\publish-windows.ps1
```

Sortie par défaut : `dist\` contient un sous-dossier par runtime (`win-x64`, `win-x86`, `win-arm64`) avec l’exécutable autonome (pas besoin d’installer le runtime .NET sur le PC cible).

Options (variables) :

- `-Configuration Release` (défaut)
- `-Version 1.0.0` (défaut) — utilisé pour le nom des ZIP
- `-SingleFile` : publie en un seul `.exe` (démarrage un peu plus lent)

## 2. Créer l’installeur Windows (Inno Setup)

1. Exécuter `.\publish-windows.ps1` (remplit `dist\win-x64`, `dist\win-x86`, `dist\win-arm64`).
2. Ouvrir **Inno Setup Compiler** et compiler le script adapté :
   - `MagicalChalkStudio.iss` — **64 bits** (PC classique) ;
   - `MagicalChalkStudio-x86.iss` — **32 bits** ;
   - `MagicalChalkStudio-arm64.iss` — **ARM64** (Surface, etc.).

Ou en ligne de commande, si `ISCC.exe` est installé :

```powershell
.\compile-installer.ps1
```

(Compile `MagicalChalkStudio.iss` par défaut — ouvrez `compile-installer.ps1` pour ajouter les autres `.iss` si besoin.)

Les installeurs sortent dans `build\output\` (ex. `MagicalChalkStudio-Setup-1.0.0-x64.exe`).

## 3. Partager sans installeur

Envoyer le ZIP généré dans `dist\` (ex. `MagicalChalkStudio-1.0.0-win-x64.zip`) : l’utilisateur décompresse et lance `MagicalChalkStudio.exe`.

## macOS / Linux

Voir `PLATFORMES.md` : l’app actuelle est WPF, donc **Windows seulement**.
