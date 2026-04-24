# Plateformes

## Windows (supporté)

**Magical Chalk Studio** est une application **WPF** (`.NET 8`, `net8.0-windows`). Elle s’exécute **uniquement sur Windows**.

Des paquets prêts à partager sont générés pour :

| Dossier / cible   | Description                          |
|-------------------|--------------------------------------|
| `win-x64`         | Windows 64 bits (recommandé)         |
| `win-x86`         | Windows 32 bits (machines anciennes) |
| `win-arm64`       | Windows sur ARM (Surface, etc.)      |

Utilisez les scripts dans `build\` pour publier en autonome (self-contained) ou voir `README-INSTALLER.md` pour l’installeur.

## macOS et Linux (non supportés tels quels)

WPF n’existe **pas** sur macOS ni sur Linux. Il n’existe **pas** d’installeur .exe / .msix qui transforme ce projet en application native Mac ou Linux.

Pour viser d’autres OS, il faudrait **réécrire l’interface** avec un framework cross‑platform, par exemple :

- **Avalonia UI** (XAML proche de WPF)
- **.NET MAUI** (surtout mobile + desktop)
- **interface web** (Blazor, etc.)

C’est un **nouveau projet** (portage), pas un simple module d’installation.

## Résumé

| OS      | Avec le code actuel (WPF) |
|---------|---------------------------|
| Windows | Oui                       |
| macOS   | Non                       |
| Linux   | Non                       |
