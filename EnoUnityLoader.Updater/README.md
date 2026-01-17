# EnoUnityLoader.Updater

Point d'entrée Doorstop qui gère les mises à jour automatiques avant de charger EnoUnityLoader.

## Vue d'ensemble

L'Updater est le premier assembly chargé par Doorstop. Il :
1. Lance l'UI (splash screen)
2. Vérifie s'il y a une mise à jour d'EnoUnityLoader disponible sur GitHub
3. Télécharge et applique la mise à jour si nécessaire
4. Vérifie et met à jour les mods configurés dans `mods.yaml`
5. Charge dynamiquement `EnoUnityLoader.dll` et appelle son point d'entrée

## Flux d'exécution

```
Doorstop
    │
    ▼
Entrypoint.Start()
    │
    ├── EnvVars.Load()           # Charge les variables d'environnement Doorstop
    ├── Mutex                     # Empêche les instances multiples
    │
    ▼
UpdateOrchestrator.RunAsync()
    │
    ├── LaunchAndConnectUiAsync() # Lance EnoUnityLoader.Ui.exe et connecte via IPC
    │
    ├── CheckAndApplyUpdatesAsync()
    │   ├── UpdateChecker.CheckForUpdateAsync()     # Vérifie GitHub API
    │   └── UpdateDownloader.DownloadAndApplyAsync() # Si mise à jour disponible
    │
    ├── CheckAndUpdateModsAsync()
    │   └── ModManager.UpdateModsAsync()  # Met à jour les mods depuis mods.yaml
    │
    └── LoadAndRunLoader()
        ├── InitializeLoaderEnvVars()  # Initialise EnvVars d'EnoUnityLoader
        └── PreloaderMain()            # Appelle le point d'entrée d'EnoUnityLoader
```

## Vérification des mises à jour

### Détection de la version actuelle

La version actuelle est lue depuis l'assembly `EnoUnityLoader.dll` :

```csharp
// UpdateChecker.cs
var assemblyName = AssemblyName.GetAssemblyName(loaderPath);
return assemblyName.Version;
```

### Récupération de la dernière version

L'Updater interroge l'API GitHub pour récupérer la dernière release :

```
GET https://api.github.com/repos/EnoPM/EnoUnityLoader/releases/latest
```

La version est extraite du tag de la release (ex: `v1.2.3` → `1.2.3`).

## Application des mises à jour

### Processus actuel

1. **Téléchargement** : Le fichier ZIP de la release est téléchargé dans un dossier temporaire
   ```
   {ModLoaderRoot}/update-temp/update.zip
   ```

2. **Extraction** : Le contenu est extrait vers `{ModLoaderRoot}`
   - La structure du ZIP est : `EnoUnityLoader/core/...`, `EnoUnityLoader/patchers/...`, etc.
   - Le préfixe `EnoUnityLoader/` est supprimé lors de l'extraction
   - **`EnoUnityLoader.Updater.dll` est ignoré** pour éviter les erreurs "fichier en cours d'utilisation"

3. **Nettoyage** : Le dossier temporaire est supprimé

### Structure du ZIP attendue

```
EnoUnityLoader-v1.2.3.zip
└── EnoUnityLoader/
    ├── core/
    │   ├── EnoUnityLoader.dll
    │   ├── EnoUnityLoader.Updater.dll  ← Ignoré lors de l'extraction
    │   ├── EnoUnityLoader.Ipc.dll
    │   └── ui/
    │       └── EnoUnityLoader.Ui.exe
    ├── patchers/
    │   └── plugins/
    │       └── EnoUnityLoader.AutoInterop.dll
    └── mods/
```

### Fichiers ignorés lors de l'extraction

| Fichier                      | Raison                                  |
|------------------------------|-----------------------------------------|
| `EnoUnityLoader.Updater.dll` | En cours d'exécution (processus actuel) |
| `EnoUnityLoader.Ipc.dll`     | Chargé par l'Updater                    |
| `EnoUnityLoader.Ui.exe`      | En cours d'exécution (processus séparé) |
| `YamlDotNet.dll`             | Chargé par l'Updater                    |

## Gestion des mods

L'Updater peut automatiquement télécharger et mettre à jour des mods depuis GitHub.

### Configuration

Créer un fichier `mods.yaml` à la racine du dossier EnoUnityLoader :

```yaml
mods:
  - name: BetterVanilla
    repo: EnoPM/BetterVanilla
    mainAssembly: BetterVanilla.dll
    enabled: true

  - name: AnotherMod
    repo: owner/repo-name
    mainAssembly: AnotherMod.dll
    enabled: false
```

### Propriétés

| Propriété      | Description                                         |
|----------------|-----------------------------------------------------|
| `name`         | Nom du mod (utilisé comme nom de dossier)           |
| `repo`         | Repository GitHub au format `owner/repo`            |
| `mainAssembly` | Nom de la DLL principale (pour vérifier la version) |
| `enabled`      | Active/désactive le téléchargement du mod           |

### Fonctionnement

1. L'Updater lit `mods.yaml`
2. Pour chaque mod activé :
   - Récupère la dernière release via l'API GitHub
   - Compare la version de la release avec la version de `mainAssembly` installée
   - Si une mise à jour est disponible, télécharge tous les assets `.dll`
3. Les DLLs sont placées dans `EnoUnityLoader/mods/{name}/`

### Structure des dossiers

```
EnoUnityLoader/
├── mods.yaml
├── core/
└── mods/
    ├── BetterVanilla/
    │   └── BetterVanilla.dll
    └── AnotherMod/
        ├── AnotherMod.dll
        └── AnotherMod.Dependency.dll
```

### Prérequis pour les releases GitHub

Les releases des mods doivent :
- Avoir un tag au format `vX.Y.Z` (ex: `v1.0.0`)
- Contenir les fichiers `.dll` directement en tant qu'assets (pas dans un ZIP)

## Personnalisation de l'UI

### Logo et icône personnalisés

Pour personnaliser l'apparence de l'interface de chargement, créez un dossier `icons` dans le dossier `ui/` :

```
EnoUnityLoader/
├── core/
│   └── ui/
│       ├── EnoUnityLoader.Ui.exe
│       └── icons/
│           ├── logo.png    ← Logo affiché dans la fenêtre
│           └── icon.png    ← Icône de la barre des tâches (ou icon.ico)
└── ...
```

| Fichier    | Description                                        |
|------------|----------------------------------------------------|
| `logo.png` | Logo affiché au centre de la fenêtre de chargement |
| `icon.png` | Icône affichée dans la barre des tâches            |
| `icon.ico` | Alternative au format ICO (prioritaire sur PNG)    |

Si ces fichiers n'existent pas, le logo par défaut d'EnoUnityLoader sera utilisé.

**Recommandations :**
- **Logo** : PNG avec transparence, hauteur recommandée 320px ou plus
- **Icône** : PNG ou ICO, taille recommandée 256x256 pixels
- Le fond de l'UI est sombre (#202020), privilégiez donc des couleurs claires

## Limitations actuelles

1. **L'Updater ne peut pas se mettre à jour lui-même** : Puisque `EnoUnityLoader.Updater.dll` est en cours d'exécution, il est impossible de le remplacer.

2. **Pas de rollback** : Si une mise à jour échoue partiellement, il n'y a pas de mécanisme de restauration.

3. **Pas de vérification d'intégrité** : Le fichier téléchargé n'est pas vérifié (hash/signature).

## Communication IPC

L'Updater communique avec l'UI via des named pipes pour afficher la progression :

```csharp
SendProgress("Checking for updates", "Contacting GitHub...");
SendProgress("Downloading update", "1.5 MB / 3.2 MB", 0.47);
SendProgress("Loading", "Starting mod loader...");
```

## Variables d'environnement Doorstop utilisées

| Variable                      | Description                              |
|-------------------------------|------------------------------------------|
| `DOORSTOP_INVOKE_DLL_PATH`    | Chemin vers `EnoUnityLoader.Updater.dll` |
| `DOORSTOP_PROCESS_PATH`       | Chemin vers l'exécutable du jeu          |
| `DOORSTOP_MANAGED_FOLDER_DIR` | Dossier "Managed" du jeu                 |

## Chemins importants

```csharp
// Racine du mod loader (ex: C:\Game\EnoUnityLoader)
EnvVars.GetModLoaderRoot()

// Dossier core (ex: C:\Game\EnoUnityLoader\core)
EnvVars.GetCoreDirectory()
```
