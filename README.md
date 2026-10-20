<p align="center">
  <img src="assets/logo.png" alt="Logo PingMonitor" width="100">
</p>

# PingMonitor

[![Build and check](https://github.com/FoxySnakes/PingMonitor/actions/workflows/build.yml/badge.svg)](https://github.com/FoxySnakes/PingMonitor/actions/workflows/build.yml)

Petit moniteur réseau pour Windows : le ping reste visible près de l’horloge, et le survol affiche le trafic reçu et envoyé. Aucune fenêtre principale, aucun historique, aucun compte à créer.

## Fonctionnalités

- Ping Internet actualisé toutes les deux secondes.
- Débits en **ko/s**, puis **Mo/s dès 0,1 Mo/s** ; choix alternatif **kb/s / Mb/s**, sauvegardé automatiquement.
- Modes sans couleur, couleur du texte ou couleur de fond. Les deux premiers ont un fond transparent.
- Pause/reprise, protection contre les doubles lancements et démarrage avec Windows activable dans le menu.
- Logo Écho intégré à l’exécutable et au raccourci.

L’icône peut être rangée dans les icônes masquées de Windows. Survolez le chiffre pour lire les mesures ; faites un clic droit pour ouvrir le menu.

## Installation depuis les sources

Prérequis : **Windows x64** et le [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

Dans un terminal PowerShell, à la racine du dépôt :

```powershell
.\scripts\Install.ps1
```

Le script compile une version autonome, l’installe dans `%LOCALAPPDATA%\Programs\PingMonitor`, crée le raccourci du menu Démarrer et lance l’application. Il ne nécessite pas de droits administrateur. Pour demander aussi le démarrage avec Windows :

```powershell
.\scripts\Install.ps1 -StartWithWindows
```

Pour une mise à jour, fermez l’application ou relancez le même script : il arrête uniquement la copie installée dans ce dossier avant de la remplacer. `-NoLaunch` permet de ne pas lancer l’application après l’installation.

Le démarrage automatique n’est pas activé par défaut lors d’une première installation. Le menu « Démarrer avec Windows » permet de l’activer ou de le désactiver ensuite. Cette option utilise la valeur `PingMonitor` de `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` et lance l’application à l’ouverture de la session de l’utilisateur courant.

Pour désinstaller : désactivez cette option, quittez PingMonitor, puis supprimez son dossier d’installation et son raccourci du menu Démarrer.

## Version portable / compilation

Les exécutions réussies du workflow [Build and check](https://github.com/FoxySnakes/PingMonitor/actions/workflows/build.yml) produisent une archive `PingMonitor-win-x64`. Après extraction, lancez `PingMonitor.exe` en conservant `PingMonitor.dll.config` à côté.

Pour produire cette version localement :

```powershell
dotnet publish PingMonitor/PingMonitor.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish -p:PublishSingleFile=true
```

.NET est embarqué dans la version autonome ; son utilisateur n’a pas à installer le runtime séparément. Une nouvelle compilation avec un SDK .NET à jour est nécessaire pour embarquer les correctifs du runtime. Les exécutables produits ne sont pas signés.

## Mesures et limites

- Le ping vise `1.1.1.1`, avec repli sur `8.8.8.8`. La dernière cible qui a répondu est essayée en premier. Un délai d’attente de 1 seconde s’applique à chaque essai.
- `N/A` signifie qu’aucune cible ne répond à ICMP. Un pare-feu peut bloquer ICMP alors qu’Internet fonctionne.
- Les débits mesurent **le trafic actuel**, pas la vitesse maximale de la connexion. Une valeur proche de zéro est normale au repos.
- Les interfaces actives disposant d’une passerelle sont additionnées ; les interfaces de bouclage et de tunnel sont exclues. Avec certains VPN ou adaptateurs virtuels, le total peut différer du trafic physique réel.
- Les unités sont décimales : 1 ko = 1000 octets, 1 Mo = 1000 ko, 1 octet = 8 bits. Réception et émission choisissent leur unité indépendamment. En bits, le passage à Mb/s intervient dès 0,1 Mb/s.
- Les couleurs suivent les seuils de ping : vert jusqu’à 50 ms, jaune jusqu’à 100 ms, orange jusqu’à 200 ms, rouge au-delà. Le mode « Aucune » utilise un texte blanc neutre sur fond transparent.

## Données et confidentialité

L’application lit les compteurs réseau de Windows et envoie des requêtes ICMP aux deux adresses indiquées ci-dessus. Elle ne collecte ni contenu des connexions, ni historique de navigation, ni télémétrie applicative. Elle ne stocke pas d’historique du ping. Les préférences de couleur et d’unité sont enregistrées localement par les paramètres utilisateur .NET.

## Vérifications

```powershell
dotnet build PingMonitor.sln -c Release --warnaserror
dotnet run --project tests/PingMonitor.Checks.csproj -c Release --no-build
```

Par défaut, les vérifications utilisent une sonde de ping simulée et ne nécessitent pas de réponse Internet. Elles contrôlent les conversions, le seuil de 0,1 Mo/s, les menus, la sauvegarde des unités, la pause/reprise, l’icône et ses ressources graphiques. Le démarrage automatique est testé dans une clé de registre isolée, sans toucher à la configuration de démarrage réelle.

Pour ajouter les vérifications réseau réelles :

```powershell
dotnet run --project tests/PingMonitor.Checks.csproj -c Release -- --network
```

Ce mode nécessite une réponse ICMP de `1.1.1.1` et un adaptateur réseau avec passerelle. Les tests affichent temporairement une icône supplémentaire et restaurent leurs préférences initiales à la fin.

## Développement

L’application utilise Windows Forms et .NET 10 LTS. Le workflow GitHub Actions compile avec les avertissements traités comme erreurs, exécute les vérifications sans sonde externe et produit la version Windows x64. Les actions sont fixées à des commits précis ; Dependabot propose leurs mises à jour.

Pour un bug, indiquez la version de Windows, le type de connexion et les étapes de reproduction dans une issue. N’incluez pas de mot de passe, jeton ou donnée personnelle dans les rapports publics.

Aucune licence n’est fournie pour le moment.
